using System.Globalization;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

internal static class InventoryLedgerWorkbook
{
    internal static byte[] Create(InventoryLedgerPrintDocument document)
    {
        var r = document.Report;
        Check.That(r.Rows.Items.Count == r.Rows.TotalCount && r.Rows.Items.Sum(x => (long)x.DocumentUnits.Length) <= 100_000,
            "report_export_too_large", "تجاوز التقرير حد التصدير؛ ضيق المرشحات.");
        return ReportWorkbook.Create("بطاقة الصنف والحركة", Row =>
        {
            string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string Time(DateTimeOffset d) => d.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            Row("الشركة", document.CompanyName, r.CompanyId.ToString());
            Row("الفروع المصرح بها", string.Join("؛ ", document.BranchNames));
            Row("من تاريخ", Date(r.From)); Row("إلى تاريخ", Date(r.To));
            Row("عملة العرض", r.Currency);
            Row("طريقة عرض العملات", "العملة الوظيفية والقيم التاريخية المثبتة");
            Row("حالة المستندات", "افتتاح المخزون المرحل وعكسه فقط");
            Row("مستوى التفصيل", "الحركة ووحدات سطور المصدر");
            Row("الصنف", r.ItemCode, r.ItemName, r.ItemId.ToString());
            Row("المخزن", document.WarehouseName ?? "جميع المخازن ضمن النطاق", r.WarehouseId?.ToString() ?? "");
            Row("الدفعة أو التسلسل", r.IdentityNumber ?? "الكل");
            Row("الوحدة الأساسية", document.BaseUnitCode, r.BaseUnitId.ToString());
            Row("وقت الاستخراج UTC", Time(document.ExtractedAt));
            Row("عدد الحركات", r.Rows.TotalCount.ToString(CultureInfo.InvariantCulture));
            Row("ملاحظة الدقة", "المبالغ التي تتجاوز دقة Excel تحفظ كنص مطابق دون تقريب.");
            Row("الفترة كاملة", "الكمية الأساسية", "القيمة");
            Row("أول المدة", r.OpeningQuantity, r.OpeningValue);
            Row("داخل", r.InQuantity, r.InValue); Row("خارج", r.OutQuantity, r.OutValue);
            Row("ختام", r.ClosingQuantity, r.ClosingValue);
            Row("الحركة", "المستند", "معرف المستند", "القيد", "عكس الحركة", "تاريخ الأثر", "وقت الترحيل UTC",
                "الفرع", "المخزن", "الهوية", "دفعة", "تسلسل", "داخل", "خارج", "قيمة الداخل", "قيمة الخارج", "الرصيد", "قيمة الرصيد");
            foreach (var x in r.Rows.Items)
                Row(x.MovementId.ToString(), x.DocumentNumber, x.OpeningId.ToString(), x.JournalId.ToString(),
                    x.ReversesMovementId?.ToString() ?? "", Date(x.AccountingDate), Time(x.OccurredAt), x.BranchId.ToString(),
                    x.WarehouseId.ToString(), x.IdentityId?.ToString() ?? "", x.LotNumber ?? "", x.SerialNumber ?? "",
                    x.InQuantity, x.OutQuantity, x.InValue, x.OutValue, x.RunningQuantity, x.RunningValue);
            Row("الحركة", "موضع السطر", "معرف وحدة المستند", "رمز الوحدة", "كمية المستند", "معامل التحويل", "تكلفة الوحدة", "القيمة");
            foreach (var x in r.Rows.Items)
                foreach (var u in x.DocumentUnits)
                    Row(x.MovementId.ToString(), u.Position.ToString(CultureInfo.InvariantCulture), u.UnitId.ToString(),
                        u.UnitCode, u.Quantity, u.FactorToBase, u.UnitCost, u.Value);
        });
    }
}
