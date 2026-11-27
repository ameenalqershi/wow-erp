using System.Globalization;
using System.Net;
using System.Text;
using Ameen.Erp.Application;

namespace Ameen.Erp.Api;

internal static class AccountLedgerPrintHtml
{
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "—");
    private static string N(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string D(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string Render(AccountLedgerPrintDocument document)
    {
        var r = document.Report;
        var html = new StringBuilder("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\"><title>دفتر الأستاذ</title><style>body{font-family:system-ui,Tahoma,Arial,sans-serif;margin:2rem}table{width:100%;border-collapse:collapse}th,td{border:1px solid #aaa;padding:.4rem}th{background:#eee}td.num{direction:ltr;text-align:left}dt{font-weight:bold}dd{margin-bottom:.5rem}@media print{@page{size:landscape}body{margin:8mm}thead{display:table-header-group}tr{break-inside:avoid}}</style></head><body><h1>دفتر الأستاذ وكشف الحساب</h1><dl>");
        void Field(string name, string value) => html.Append("<dt>").Append(H(name)).Append("</dt><dd>").Append(H(value)).Append("</dd>");
        Field("الشركة", document.CompanyName + " (" + r.CompanyId + ")");
        Field("الفروع", document.BranchNames.Count == 0 ? "لا فروع" : string.Join("؛ ", document.BranchNames));
        Field("الحساب", document.AccountCode + " — " + document.AccountName);
        Field("من تاريخ", D(r.From)); Field("إلى تاريخ", D(r.To));
        Field("عملة العرض", r.Currency);
        Field("طريقة العرض والتحويل", "العملة الوظيفية — المبالغ التاريخية المثبتة عند الترحيل");
        Field("حالة المستندات", "مرحلة والأصول المعكوسة ذات الأثر التاريخي");
        Field("مستوى التفصيل", "حساب واحد — جميع سطور الحركة المطابقة");
        Field("مركز التكلفة", document.CostCenterName ?? "جميع المراكز ضمن النطاق");
        Field("مرجع خارجي", r.ExternalReference ?? "الكل");
        Field("وقت الاستخراج UTC", document.ExtractedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Field("الرصيد الافتتاحي", N(r.OpeningBalance));
        html.Append("</dl><table><thead><tr><th>تاريخ الأثر</th><th>رقم المستند</th><th>المصدر</th><th>السطر</th><th>البيان</th><th>مركز التكلفة</th><th>العملة الأصلية</th><th>مدين</th><th>دائن</th><th>الرصيد</th></tr></thead><tbody>");
        foreach (var row in document.Rows)
        {
            var e = row.Entry;
            html.Append("<tr data-journal-id=\"").Append(e.JournalId).Append("\"><td>").Append(D(e.AccountingDate))
                .Append("</td><td>").Append(H(e.Number)).Append("</td><td>").Append(H(row.Source))
                .Append("</td><td>").Append(e.Position.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                .Append(H(e.Description)).Append("</td><td>").Append(H(row.CostCenterId is Guid id ? document.CostCenters[id] : null))
                .Append("</td><td>").Append(H(e.CurrencyCode)).Append("</td>");
            foreach (var amount in new[] { e.Debit, e.Credit, e.RunningBalance })
                html.Append("<td class=\"num\">").Append(N(amount)).Append("</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody><tfoot><tr><th colspan=\"7\">إجمالي الحركة والرصيد الختامي</th>");
        foreach (var amount in new[] { r.PeriodDebit, r.PeriodCredit, r.ClosingBalance })
            html.Append("<td class=\"num\">").Append(N(amount)).Append("</td>");
        html.Append("</tr></tfoot></table><p>معاينة قراءة فقط؛ المبالغ بالعملة الوظيفية، والعملة الأصلية للسطر للرجوع إلى المصدر.</p></body></html>");
        return html.ToString();
    }
}
