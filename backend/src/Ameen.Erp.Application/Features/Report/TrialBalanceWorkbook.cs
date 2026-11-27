using System.Globalization;
using System.IO.Compression;
using System.Xml;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

// Minimal interoperable XLSX package; no formulas or external relationships.
// Inline text cells protect user-controlled values from formula execution.
internal static class TrialBalanceWorkbook
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal static byte[] Create(TrialBalancePrintDocument document)
    {
        var r = document.Report;
        // Excel row limit includes metadata, heading and totals.
        Check.That(r.Rows.Count <= 1_048_560, "report_export_too_large",
            "تجاوز التقرير سعة ملف Excel؛ ضيق المرشحات.");
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            void Part(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new System.Text.UTF8Encoding(false));
                writer.Write(content);
            }
            Part("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            Part("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Part("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="ميزان المراجعة" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Part("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            using var xml = XmlWriter.Create(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(),
                new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), CloseOutput = true });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", Ns);
            xml.WriteStartElement("sheetViews", Ns);
            xml.WriteStartElement("sheetView", Ns);
            xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteAttributeString("rightToLeft", "1");
            xml.WriteEndElement(); xml.WriteEndElement();
            xml.WriteStartElement("sheetData", Ns);
            int row = 0;
            void Row(params object[] values)
            {
                xml.WriteStartElement("row", Ns);
                xml.WriteAttributeString("r", (++row).ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < values.Length; i++)
                {
                    xml.WriteStartElement("c", Ns);
                    xml.WriteAttributeString("r", ((char)('A' + i)).ToString() + row.ToString(CultureInfo.InvariantCulture));
                    // Excel is limited to 15 significant digits. Store exact
                    // higher-precision amounts as text rather than silently rounding.
                    var value = values[i];
                    var number = value is decimal d ? d.ToString("0.############################", CultureInfo.InvariantCulture) : null;
                    var digits = number?.TrimStart('-').Replace(".", "").TrimStart('0').TrimEnd('0').Length;
                    if (number is not null && digits <= 15)
                    {
                        xml.WriteAttributeString("t", "n");
                        xml.WriteElementString("v", Ns, number);
                    }
                    else
                    {
                        xml.WriteAttributeString("t", "inlineStr");
                        xml.WriteStartElement("is", Ns); xml.WriteStartElement("t", Ns);
                        xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                        var text = number ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                        // OpenXML's escape syntax is itself escaped so values
                        // resembling _xHHHH_ remain literal in Excel.
                        text = System.Text.RegularExpressions.Regex.Replace(text, "_x[0-9a-fA-F]{4}_", m => "_x005F_" + m.Value[1..]);
                        xml.WriteString(text); xml.WriteEndElement(); xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            Row("الشركة", document.CompanyName, r.CompanyId.ToString());
            Row("الفروع", string.Join("؛ ", document.BranchNames));
            Row("من تاريخ", r.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Row("إلى تاريخ", r.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Row("عملة العرض", r.Currency);
            Row("طريقة العرض والتحويل", "العملة الوظيفية — المبالغ التاريخية المثبتة عند الترحيل");
            Row("حالة المستندات", "مرحلة والأصول المعكوسة ذات الأثر التاريخي");
            Row("مستوى التفصيل والدليل", r.AccountLevel?.ToString(CultureInfo.InvariantCulture) ?? "الحسابات التفصيلية المرحلة");
            Row("مركز التكلفة", document.CostCenterName ?? "جميع المراكز ضمن النطاق");
            Row("إظهار الصفري", r.ShowZero ? "نعم" : "لا");
            Row("وقت الاستخراج UTC", document.ExtractedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Row("ملاحظة الدقة", "المبالغ التي تتجاوز دقة Excel تحفظ كنص مطابق دون تقريب.");
            Row("رمز الحساب", "اسم الحساب", "أول المدة مدين", "أول المدة دائن", "الحركة مدين", "الحركة دائن", "آخر المدة مدين", "آخر المدة دائن");
            foreach (var x in r.Rows)
                Row(x.Code, x.Name, x.OpeningDebit, x.OpeningCredit, x.PeriodDebit, x.PeriodCredit, x.ClosingDebit, x.ClosingCredit);
            Row("الإجمالي", "", r.TotalOpeningDebit, r.TotalOpeningCredit, r.TotalPeriodDebit, r.TotalPeriodCredit, r.TotalClosingDebit, r.TotalClosingCredit);
            xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument();
        }
        return buffer.ToArray();
    }
}
