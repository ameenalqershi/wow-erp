using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed partial class ReportService
{
    public async Task<AccountLedgerPrintDocument> AccountLedgerPrint(Guid cid, Guid accountId, Actor actor,
        DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct,
        Guid? costCenterId = null, string? externalReference = null, Guid[]? branchIds = null)
    {
        var (report, rows) = await BuildAccountLedger(cid, accountId, actor, from, to, branchId, 0, null, ct,
            costCenterId, externalReference, branchIds);
        var extractedAt = clock.UtcNow;
        var company = await Company(cid, ct);
        var account = await Required<Account>(cid, accountId, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => x.Code + " — " + x.Name).ToArray();
        Check.That(names.Length == report.BranchIds.Count, "reference_not_found",
            "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var ids = rows.Where(x => x.CostCenterId.HasValue).Select(x => x.CostCenterId!.Value)
            .Concat(costCenterId is Guid id ? new[] { id } : Array.Empty<Guid>()).Distinct().ToArray();
        var centers = (await store.Find<CostCenter>(cid, x => ids.Contains(x.Id), ct))
            .ToDictionary(x => x.Id, x => x.Code + " — " + x.Name);
        Check.That(centers.Count == ids.Length, "reference_not_found", "تعذر مطابقة مراكز تكلفة التقرير.", ErrorKind.NotFound);
        return new(report, company.Name, account.Code, account.Name, names,
            costCenterId is Guid selected ? centers[selected] : null, centers, rows, extractedAt);
    }
}
