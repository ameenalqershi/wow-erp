using System.Globalization;
using System.Net;
using System.Text;
using Ameen.Erp.Application;

namespace Ameen.Erp.Api;

internal static class TrialBalancePrintHtml
{
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "—");
    private static string N(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string D(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string Render(TrialBalancePrintDocument document)
    {
        var r = document.Report;
        var html = new StringBuilder("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\"><title>ميزان المراجعة</title><style>body{font-family:system-ui,Tahoma,Arial,sans-serif;margin:2rem}table{width:100%;border-collapse:collapse}th,td{border:1px solid #aaa;padding:.4rem}th{background:#eee}td.num{direction:ltr;text-align:left}dt{font-weight:bold}dd{margin-bottom:.5rem}@media print{body{margin:8mm}thead{display:table-header-group}tr{break-inside:avoid}}</style></head><body><h1>ميزان المراجعة</h1><dl>");
        void Field(string name, string value) => html.Append("<dt>").Append(H(name)).Append("</dt><dd>").Append(H(value)).Append("</dd>");
        Field("الشركة", document.CompanyName + " (" + r.CompanyId + ")");
        Field("الفروع", document.BranchNames.Count == 0 ? "لا فروع" : string.Join("؛ ", document.BranchNames));
        Field("من تاريخ", D(r.From)); Field("إلى تاريخ", D(r.To));
        Field("عملة العرض", r.Currency);
        Field("طريقة العرض والتحويل", "العملة الوظيفية — المبالغ التاريخية المثبتة عند الترحيل");
        Field("حالة المستندات", "مرحلة والأصول المعكوسة ذات الأثر التاريخي");
        Field("مستوى التفصيل", r.AccountLevel is int level ? "مستوى الدليل " + level.ToString(CultureInfo.InvariantCulture) : "الحسابات التفصيلية المرحلة");
        Field("مركز التكلفة", document.CostCenterName ?? "جميع المراكز ضمن النطاق");
        Field("إظهار الصفري", r.ShowZero ? "نعم" : "لا");
        Field("وقت الاستخراج UTC", document.ExtractedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        html.Append("</dl><table><thead><tr><th rowspan=\"2\">رمز الحساب</th><th rowspan=\"2\">اسم الحساب</th><th colspan=\"2\">أول المدة</th><th colspan=\"2\">الحركة</th><th colspan=\"2\">آخر المدة</th></tr><tr><th>مدين</th><th>دائن</th><th>مدين</th><th>دائن</th><th>مدين</th><th>دائن</th></tr></thead><tbody>");
        void Amounts(params decimal[] values)
        {
            foreach (var value in values) html.Append("<td class=\"num\">").Append(N(value)).Append("</td>");
            html.Append("</tr>");
        }
        foreach (var row in r.Rows)
        {
            html.Append("<tr><td>").Append(H(row.Code)).Append("</td><td>").Append(H(row.Name)).Append("</td>");
            Amounts(row.OpeningDebit, row.OpeningCredit, row.PeriodDebit, row.PeriodCredit, row.ClosingDebit, row.ClosingCredit);
        }
        html.Append("</tbody><tfoot><tr><th colspan=\"2\">الإجمالي</th>");
        Amounts(r.TotalOpeningDebit, r.TotalOpeningCredit, r.TotalPeriodDebit, r.TotalPeriodCredit, r.TotalClosingDebit, r.TotalClosingCredit);
        html.Append("</tfoot></table><p>معاينة قراءة فقط. قد يختلف طرفا المرشح الجزئي لمركز التكلفة؛ لا تُضاف تسويات اصطناعية.</p></body></html>");
        return html.ToString();
    }
}
