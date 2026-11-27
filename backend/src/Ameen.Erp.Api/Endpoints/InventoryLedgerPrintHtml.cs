using System.Globalization;
using System.Net;
using System.Text;
using Ameen.Erp.Application;
namespace Ameen.Erp.Api;
internal static class InventoryLedgerPrintHtml
{
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "—");
    private static string N(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string D(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string T(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    internal static string Render(InventoryLedgerPrintDocument document)
    {
        var r = document.Report;
        var html = new StringBuilder("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\"><title>بطاقة الصنف والحركة</title><style>body{font-family:system-ui,Tahoma,Arial,sans-serif;margin:1rem}table{width:100%;border-collapse:collapse;margin:1rem 0}th,td{border:1px solid #aaa;padding:.3rem;overflow-wrap:anywhere}th{background:#eee}td.num{direction:ltr;text-align:left}dt{font-weight:bold}dd{margin-bottom:.3rem}@media print{body{margin:8mm}thead{display:table-header-group}tr{break-inside:avoid}}</style></head><body><h1>بطاقة الصنف والحركة</h1><dl>");
        void Field(string name, string value) => html.Append("<dt>").Append(H(name)).Append("</dt><dd>").Append(H(value)).Append("</dd>");
        void Cell(string? value) => html.Append("<td>").Append(H(value)).Append("</td>");
        void Amounts(params decimal[] values) { foreach (var v in values) html.Append("<td class=\"num\">").Append(N(v)).Append("</td>"); }
        Field("الشركة", document.CompanyName + " (" + r.CompanyId + ")");
        Field("الفروع المصرح بها", document.BranchNames.Length == 0 ? "لا فروع" : string.Join("؛ ", document.BranchNames));
        Field("من تاريخ", D(r.From)); Field("إلى تاريخ", D(r.To));
        Field("الصنف", r.ItemCode + " — " + r.ItemName + " (" + r.ItemId + ")");
        Field("الوحدة الأساسية", document.BaseUnitCode + " (" + r.BaseUnitId + ")");
        Field("المخزن", r.WarehouseId is Guid id ? document.WarehouseName + " (" + id + ")" : "جميع المخازن ضمن النطاق");
        Field("الدفعة أو التسلسل", r.IdentityNumber ?? "الكل");
        Field("عملة العرض", r.Currency);
        Field("طريقة عرض العملات", "العملة الوظيفية والقيم التاريخية المثبتة");
        Field("حالة المستندات", "افتتاح المخزون المرحل وعكسه فقط");
        Field("مستوى التفصيل", "الحركة ووحدات سطور المصدر");
        Field("وقت الاستخراج UTC", T(document.ExtractedAt));
        Field("صفحة المعاينة", $"الإزاحة {r.Rows.Offset}؛ الحد {r.Rows.Limit}؛ المعروض {r.Rows.Items.Count} من {r.Rows.TotalCount}");
        html.Append("</dl><p>معاينة الصفحة المحددة فقط؛ الإجماليات تخص كامل نطاق المرشحات، لا الصفحة. لا تشمل الحركات التجارية غير المنفذة.</p><table><thead><tr><th>الفترة كاملة</th><th>كمية أساسية</th><th>قيمة</th></tr></thead><tbody>");
        foreach (var total in new[] { ("أول المدة", r.OpeningQuantity, r.OpeningValue), ("داخل", r.InQuantity, r.InValue), ("خارج", r.OutQuantity, r.OutValue), ("ختام", r.ClosingQuantity, r.ClosingValue) })
        {
            html.Append("<tr>"); Cell(total.Item1); Amounts(total.Item2, total.Item3); html.Append("</tr>");
        }
        html.Append("</tbody></table><table><thead><tr><th>المستند</th><th>تاريخ الأثر</th><th>وقت الترحيل UTC</th><th>الفرع</th><th>المخزن</th><th>الدفعة / التسلسل</th><th>داخل</th><th>خارج</th><th>قيمة الداخل</th><th>قيمة الخارج</th><th>الرصيد</th><th>قيمة الرصيد</th></tr></thead><tbody>");
        foreach (var row in r.Rows.Items)
        {
            html.Append("<tr>"); Cell(row.DocumentNumber); Cell(D(row.AccountingDate)); Cell(T(row.OccurredAt));
            Cell(row.BranchId.ToString()); Cell(row.WarehouseId.ToString()); Cell(row.LotNumber ?? row.SerialNumber);
            Amounts(row.InQuantity, row.OutQuantity, row.InValue, row.OutValue, row.RunningQuantity, row.RunningValue);
            html.Append("</tr><tr><td colspan=\"12\">");
            html.Append("المستند: ").Append(H(row.OpeningId.ToString())).Append("؛ القيد: ").Append(H(row.JournalId.ToString())).Append("؛ الحركة: ").Append(H(row.MovementId.ToString()));
            if (row.ReversesMovementId is Guid reverse) html.Append("؛ عكس الحركة: ").Append(H(reverse.ToString()));
            html.Append("<table><thead><tr><th>السطر</th><th>وحدة المستند</th><th>الكمية</th><th>معامل التحويل</th><th>تكلفة الوحدة</th><th>القيمة</th></tr></thead><tbody>");
            foreach (var u in row.DocumentUnits)
            {
                html.Append("<tr>"); Cell(u.Position.ToString(CultureInfo.InvariantCulture)); Cell(u.UnitCode);
                Amounts(u.Quantity, u.FactorToBase, u.UnitCost, u.Value); html.Append("</tr>");
            }
            html.Append("</tbody></table></td></tr>");
        }
        html.Append("</tbody></table></body></html>");
        return html.ToString();
    }
}
