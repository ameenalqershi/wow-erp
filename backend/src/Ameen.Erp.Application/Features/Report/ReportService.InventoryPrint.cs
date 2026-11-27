using Ameen.Erp.Domain;
namespace Ameen.Erp.Application;
public sealed partial class ReportService
{
    public async Task<InventoryLedgerPrintDocument> InventoryLedgerPrint(Guid cid, Guid itemId, Actor actor,
        DateOnly from, DateOnly to, Guid? branchId, Guid? warehouseId, string? identityNumber,
        int offset, int limit, CancellationToken ct, Guid[]? branchIds = null)
    {
        var report = await InventoryLedger(cid, itemId, actor, from, to, branchId, warehouseId, identityNumber, offset, limit, ct, branchIds);
        var extractedAt = clock.UtcNow;
        var company = await Company(cid, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => x.Code + " — " + x.Name + " (" + x.Id + ")").ToArray();
        Check.That(names.Length == report.BranchIds.Length, "reference_not_found", "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var warehouse = warehouseId is Guid id ? await Required<Warehouse>(cid, id, ct) : null;
        var unit = await Required<UnitOfMeasure>(cid, report.BaseUnitId, ct);
        return new(report, company.Name, names, warehouse is null ? null : warehouse.Code + " — " + warehouse.Name, unit.Code, extractedAt);
    }
}
