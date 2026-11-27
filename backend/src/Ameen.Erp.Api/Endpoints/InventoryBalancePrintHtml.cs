using System.Globalization;
using System.Net;
using System.Text;
using Ameen.Erp.Application;
namespace Ameen.Erp.Api;
internal static class InventoryBalancePrintHtml
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "—");
    private static string N(decimal n) => n.ToString("0.############################", CultureInfo.InvariantCulture);
    internal static string Render(InventoryBalancePrintDocument document)
    {
        var r = document.Report;
        var html = new StringBuilder("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\"><title>أرصدة وتقييم المخزون</title><style>body{font-family:system-ui,Tahoma,Arial,sans-serif;margin:1rem}table{border-collapse:collapse;width:100%;margin:1rem 0}th,td{border:1px solid #aaa;padding:.3rem;overflow-wrap:anywhere}th{background:#eee}.num{direction:ltr;text-align:left}dt{font-weight:bold}dd{margin-bottom:.3rem}@media print{thead{display:table-header-group}tr{break-inside:avoid}}</style></head><body><h1>أرصدة وتقييم المخزون</h1><dl>");
        void Field(string name, string value) => html.Append("<dt>").Append(H(name)).Append("</dt><dd>").Append(H(value)).Append("</dd>");
        void Cell(string? value) => html.Append("<td>").Append(H(value)).Append("</td>");
        void Numbers(params decimal[] values) { foreach (var value in values) html.Append("<td class=\"num\">").Append(N(value)).Append("</td>"); }
        Field("الشركة", document.CompanyName + " (" + r.CompanyId + ")");
        Field("الفروع المصرح بها", string.Join("؛ ", document.BranchNames));
        Field("من تاريخ", r.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field("إلى تاريخ", r.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field("حتى تاريخ", r.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " — نفس نهاية الفترة");
        Field("عملة العرض", r.Currency); Field("طريقة عرض العملات", "العملة الوظيفية");
        Field("حالة المستندات", "افتتاح المخزون المرحل وعكسه فقط");
        Field("مستوى التفصيل", "صنف / فرع / مخزن / هوية / حساب رقابي");
        Field("المخزن", document.WarehouseName ?? "جميع المخازن ضمن النطاق");
        Field("المجموعة", document.GroupName ?? "الكل"); Field("الصنف", document.ItemName ?? "الكل");
        Field("تضمين الطريق والعزل", "غير مدعوم حاليًا");
        Field("إظهار الأرصدة الصفرية", r.ShowZero ? "نعم" : "لا");
        Field("وقت الاستخراج UTC", document.ExtractedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Field("صفحة المعاينة", $"الإزاحة {r.Rows.Offset}؛ الحد {r.Rows.Limit}؛ المعروض {r.Rows.Items.Count} من {r.Rows.TotalCount}");
        html.Append("</dl><p>معاينة افتتاح وعكس فقط، وليست تقييمًا شاملًا أو مطابقة رقابية عامة. الموجود والمحجوز والمتاح والطريق والعزل والتكلفة الشاملة غير مكتملة. لا تعرض المكونات غير المدعومة كأصفار. إجماليات القيم تخص كامل النطاق بعد المرشحات؛ لا تجمع كميات أصناف ووحدات مختلفة.</p><table><thead><tr><th>أول المدة</th><th>قيمة الداخل</th><th>قيمة الخارج</th><th>قيمة الختام</th></tr></thead><tbody><tr>");
        Numbers(r.OpeningValue, r.InValue, r.OutValue, r.ClosingValue);
        html.Append("</tr></tbody></table><table><thead><tr><th>الصنف</th><th>الفرع</th><th>المخزن</th><th>الوحدة الأساسية</th><th>الهوية</th><th>الحساب الرقابي</th><th>كمية أول المدة</th><th>قيمة أول المدة</th><th>كمية الداخل</th><th>قيمة الداخل</th><th>كمية الخارج</th><th>قيمة الخارج</th><th>كمية الختام</th><th>قيمة الختام</th></tr></thead><tbody>");
        foreach (var row in r.Rows.Items)
        {
            html.Append("<tr>"); Cell(row.ItemCode + " — " + row.ItemName + " (" + row.ItemId + ")");
            Cell(row.BranchId.ToString()); Cell(row.WarehouseId.ToString()); Cell(row.BaseUnitId.ToString());
            Cell(row.IdentityId?.ToString()); Cell(row.InventoryAccountId.ToString());
            Numbers(row.OpeningQuantity, row.OpeningValue, row.InQuantity, row.InValue, row.OutQuantity, row.OutValue, row.ClosingQuantity, row.ClosingValue);
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></body></html>").ToString();
    }
}
