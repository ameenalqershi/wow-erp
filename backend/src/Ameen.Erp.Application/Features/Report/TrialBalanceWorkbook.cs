using System.Globalization;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

// Minimal interoperable XLSX package; no formulas or external relationships.
// Inline text cells protect user-controlled values from formula execution.
internal static class TrialBalanceWorkbook
{
    internal static byte[] Create(TrialBalancePrintDocument document)
    {
        var r = document.Report;
        // Excel row limit includes metadata, heading and totals.
        Check.That(r.Rows.Count <= 1_048_560, "report_export_too_large",
            "تجاوز التقرير سعة ملف Excel؛ ضيق المرشحات.");
        return ReportWorkbook.Create("ميزان المراجعة", Row =>
        {
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
        });
    }
}
