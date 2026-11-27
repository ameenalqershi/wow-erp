using Ameen.Erp.Domain;
namespace Ameen.Erp.Application;
public sealed partial class ReportService
{
    public async Task<InventoryLedgerPrintDocument> InventoryLedgerPrint(Guid cid, Guid itemId, Actor actor,
        DateOnly from, DateOnly to, Guid? branchId, Guid? warehouseId, string? identityNumber,
        int offset, int limit, CancellationToken ct, Guid[]? branchIds = null)
    {
        await Authorize(cid, actor, Permissions.InventoryLedgerPrint, branchId, ct);
        var report = await InventoryLedger(cid, itemId, actor, from, to, branchId, warehouseId, identityNumber, offset, limit, ct, branchIds);
        foreach (var selectedBranch in report.BranchIds)
            await Authorize(cid, actor, Permissions.InventoryLedgerPrint, selectedBranch, ct);
        var extractedAt = clock.UtcNow;
        var company = await Company(cid, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => x.Code + " — " + x.Name + " (" + x.Id + ")").ToArray();
        Check.That(names.Length == report.BranchIds.Length, "reference_not_found", "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var warehouse = warehouseId is Guid id ? await Required<Warehouse>(cid, id, ct) : null;
        var unit = await Required<UnitOfMeasure>(cid, report.BaseUnitId, ct);
        var item = await Required<Item>(cid, report.ItemId, ct);
        Audit(cid, actor, "report.rpt09.print", item, new
        {
            report.BranchIds, report.From, report.To, report.Currency, report.WarehouseId,
            report.IdentityNumber, report.InventoryOpeningsOnly, report.Rows.Offset, report.Rows.Limit,
            RowCount = report.Rows.Items.Count, report.Rows.TotalCount, ExtractedAt = extractedAt
        });
        await store.Save(ct);
        return new(report, company.Name, names, warehouse is null ? null : warehouse.Code + " — " + warehouse.Name, unit.Code, extractedAt);
    }
}
