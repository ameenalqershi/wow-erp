using Ameen.Erp.Domain;
namespace Ameen.Erp.Application;
public sealed partial class ReportService
{
    public async Task<InventoryBalancePrintDocument> InventoryBalancePrint(Guid cid, Actor actor, DateOnly from, DateOnly to,
        Guid? branchId, Guid? warehouseId, Guid? groupId, Guid? itemId, bool showZero, int offset, int limit,
        CancellationToken ct, Guid[]? branchIds = null)
    {
        await Authorize(cid, actor, Permissions.InventoryBalancePrint, branchId, ct);
        var report = await InventoryBalances(cid, actor, from, to, branchId, warehouseId, groupId, itemId, showZero, offset, limit, ct, branchIds);
        foreach (var selected in report.BranchIds)
            await Authorize(cid, actor, Permissions.InventoryBalancePrint, selected, ct);
        var company = await Company(cid, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        Check.That(branches.Count == report.BranchIds.Length, "reference_not_found", "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => x.Code + " — " + x.Name + " (" + x.Id + ")").ToArray();
        var warehouse = warehouseId is Guid wid ? await Required<Warehouse>(cid, wid, ct) : null;
        var group = groupId is Guid gid ? await Required<ItemGroup>(cid, gid, ct) : null;
        var item = itemId is Guid iid ? await Required<Item>(cid, iid, ct) : null;
        var extractedAt = clock.UtcNow;
        Audit(cid, actor, "report.rpt08.print", company, new
        {
            report.BranchIds, report.From, report.To, report.Currency, report.WarehouseId, report.GroupId, report.ItemId,
            report.ShowZero, report.InventoryOpeningsOnly, report.Rows.Offset, report.Rows.Limit,
            RowCount = report.Rows.Items.Count, report.Rows.TotalCount, ExtractedAt = extractedAt
        });
        await store.Save(ct);
        return new(report, company.Name, names, warehouse is null ? null : warehouse.Code + " — " + warehouse.Name,
            group is null ? null : group.Code + " — " + group.Name, item is null ? null : item.Code + " — " + item.Name, extractedAt);
    }
}
