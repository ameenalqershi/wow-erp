using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Ameen.Erp.Application;

namespace Ameen.Erp.Tests;

public static class TrialBalanceExportTests
{
    public static void AssertWorkbook(byte[] bytes, TrialBalanceResult expected)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml" })
            if (zip.GetEntry(name) is null) throw new Exception("RPT01 missing XLSX part " + name);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var doc = XDocument.Load(stream);
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = doc.Descendants(n + "row").Select(x => x.Elements(n + "c").ToArray()).ToArray();
        string Value(XElement cell) => cell.Element(n + "v")?.Value ?? cell.Element(n + "is")?.Element(n + "t")?.Value ?? "";
        decimal Amount(XElement cell) => decimal.Parse(Value(cell), CultureInfo.InvariantCulture);
        if (doc.Descendants(n + "f").Any() || rows.Length != expected.Rows.Count + 14 ||
            Value(rows[0][2]) != expected.CompanyId.ToString() ||
            Value(rows[2][1]) != expected.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ||
            Value(rows[3][1]) != expected.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ||
            Value(rows[4][1]) != expected.Currency || Value(rows[9][1]) != (expected.ShowZero ? "نعم" : "لا") ||
            !DateTimeOffset.TryParse(Value(rows[10][1]), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            throw new Exception("RPT01 XLSX metadata, row count or formula safety mismatch");
        void CheckAmounts(XElement[] cells, params decimal[] amounts)
        {
            if (cells.Length != 8 || !cells.Skip(2).Select(Amount).SequenceEqual(amounts))
                throw new Exception("RPT01 XLSX accounting amounts differ from JSON");
        }
        for (var i = 0; i < expected.Rows.Count; i++)
        {
            var e = expected.Rows[i]; var cells = rows[i + 13];
            if (Value(cells[0]) != e.Code || Value(cells[1]) != e.Name ||
                (string?)cells[0].Attribute("t") != "inlineStr" || (string?)cells[1].Attribute("t") != "inlineStr")
                throw new Exception("RPT01 XLSX account identity or safe text mismatch");
            CheckAmounts(cells, e.OpeningDebit, e.OpeningCredit, e.PeriodDebit, e.PeriodCredit, e.ClosingDebit, e.ClosingCredit);
        }
        CheckAmounts(rows[^1], expected.TotalOpeningDebit, expected.TotalOpeningCredit, expected.TotalPeriodDebit,
            expected.TotalPeriodCredit, expected.TotalClosingDebit, expected.TotalClosingCredit);
    }
}
