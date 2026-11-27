using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Ameen.Erp.Application;

namespace Ameen.Erp.Tests;

public static class InventoryLedgerExportTests
{
    public static void AssertWorkbook(byte[] bytes, InventoryLedgerResult expected)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml" })
            if (zip.GetEntry(name) is null) throw new Exception("RPT09 missing XLSX part: " + name);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var doc = XDocument.Load(stream);
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = doc.Descendants(n + "row").Select(x => x.Elements(n + "c").ToArray()).ToArray();
        string Value(XElement cell) => cell.Element(n + "v")?.Value ?? cell.Element(n + "is")?.Element(n + "t")?.Value ?? "";
        void Check(bool value) { if (!value) throw new Exception("RPT09 XLSX differs from ledger"); }
        void Amounts(XElement[] row, int start, params decimal[] amounts)
            => Check(row.Skip(start).Select(c => decimal.Parse(Value(c), CultureInfo.InvariantCulture)).SequenceEqual(amounts));
        Check(!doc.Descendants(n + "f").Any() && rows.Length == 22 + expected.Rows.Items.Count + expected.Rows.Items.Sum(x => x.DocumentUnits.Length));
        Check(Value(rows[0][2]) == expected.CompanyId.ToString() && Value(rows[2][1]) == expected.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) &&
            Value(rows[3][1]) == expected.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) &&
            Value(rows[4][1]) == expected.Currency && Value(rows[8][1]) == expected.ItemCode &&
            Value(rows[8][3]) == expected.ItemId.ToString() && Value(rows[10][1]) == (expected.IdentityNumber ?? "الكل") &&
            Value(rows[11][2]) == expected.BaseUnitId.ToString() &&
            DateTimeOffset.TryParse(Value(rows[12][1]), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
        Amounts(rows[16], 1, expected.OpeningQuantity, expected.OpeningValue);
        Amounts(rows[17], 1, expected.InQuantity, expected.InValue);
        Amounts(rows[18], 1, expected.OutQuantity, expected.OutValue);
        Amounts(rows[19], 1, expected.ClosingQuantity, expected.ClosingValue);
        var i = 21;
        foreach (var row in expected.Rows.Items)
        {
            var cells = rows[i++];
            Check(Value(cells[0]) == row.MovementId.ToString() && Value(cells[1]) == row.DocumentNumber &&
                Value(cells[2]) == row.OpeningId.ToString() && Value(cells[3]) == row.JournalId.ToString() &&
                Value(cells[4]) == (row.ReversesMovementId?.ToString() ?? "") &&
                Value(cells[7]) == row.BranchId.ToString() && Value(cells[8]) == row.WarehouseId.ToString() &&
                Value(cells[10]) == (row.LotNumber ?? "") && Value(cells[11]) == (row.SerialNumber ?? "") &&
                (string?)cells[1].Attribute("t") == "inlineStr" && (string?)cells[11].Attribute("t") == "inlineStr");
            Amounts(cells, 12, row.InQuantity, row.OutQuantity, row.InValue, row.OutValue, row.RunningQuantity, row.RunningValue);
        }
        i++;
        foreach (var row in expected.Rows.Items)
            foreach (var unit in row.DocumentUnits)
            {
                var cells = rows[i++];
                Check(Value(cells[0]) == row.MovementId.ToString() && Value(cells[1]) == unit.Position.ToString(CultureInfo.InvariantCulture) &&
                    Value(cells[2]) == unit.UnitId.ToString() && Value(cells[3]) == unit.UnitCode);
                Amounts(cells, 4, unit.Quantity, unit.FactorToBase, unit.UnitCost, unit.Value);
            }
        Check(i == rows.Length);
    }
}
