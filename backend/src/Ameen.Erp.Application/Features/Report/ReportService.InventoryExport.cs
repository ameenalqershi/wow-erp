using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed partial class ReportService
{
    public async Task<byte[]> InventoryLedgerExport(Guid cid, Guid itemId, Actor actor, DateOnly from, DateOnly to,
        Guid? branchId, Guid? warehouseId, string? identityNumber, CancellationToken ct, Guid[]? branchIds = null)
    {
        await Authorize(cid, actor, Permissions.InventoryLedgerExport, branchId, ct);
        // One movement read supplies every exported row and the totals; never concatenate separately read pages.
        const int maximumMovements = 10_000;
        var report = await BuildInventoryLedger(cid, itemId, actor, from, to, branchId, warehouseId, identityNumber,
            0, maximumMovements, true, ct, branchIds);
        foreach (var selected in report.BranchIds)
            await Authorize(cid, actor, Permissions.InventoryLedgerExport, selected, ct);
        var company = await Company(cid, ct);
        var item = await Required<Item>(cid, itemId, ct);
        var unit = await Required<UnitOfMeasure>(cid, report.BaseUnitId, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        Check.That(branches.Count == report.BranchIds.Length, "reference_not_found", "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => x.Code + " — " + x.Name + " (" + x.Id + ")").ToArray();
        var warehouse = warehouseId is Guid id ? await Required<Warehouse>(cid, id, ct) : null;
        var extractedAt = clock.UtcNow;
        var bytes = InventoryLedgerWorkbook.Create(new(report, company.Name, names,
            warehouse is null ? null : warehouse.Code + " — " + warehouse.Name, unit.Code, extractedAt));
        Audit(cid, actor, "report.rpt09.export", item, new
        {
            report.BranchIds, report.From, report.To, report.Currency, report.WarehouseId, report.IdentityNumber,
            report.InventoryOpeningsOnly, RowCount = report.Rows.Items.Count, ExtractedAt = extractedAt
        });
        await store.Save(ct);
        return bytes;
    }
}
