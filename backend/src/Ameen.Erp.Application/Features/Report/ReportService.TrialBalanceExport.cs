using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed partial class ReportService
{
    public async Task<byte[]> TrialBalanceExport(Guid cid, Actor actor, DateOnly from, DateOnly to,
        Guid? branchId, CancellationToken ct, Guid? costCenterId = null, int? accountLevel = null,
        bool showZero = true, Guid[]? branchIds = null)
    {
        await Authorize(cid, actor, Permissions.TrialBalanceExport, branchId, ct);
        var report = await TrialBalance(cid, actor, from, to, branchId, ct,
            costCenterId, accountLevel, showZero, branchIds);
        var company = await Company(cid, ct);
        var branches = await store.Find<Branch>(cid, x => report.BranchIds.Contains(x.Id), ct);
        Check.That(branches.Count == report.BranchIds.Count, "reference_not_found",
            "تعذر مطابقة فروع التقرير.", ErrorKind.NotFound);
        var names = branches.OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => x.Code + " — " + x.Name).ToArray();
        var center = costCenterId is Guid id ? await Required<CostCenter>(cid, id, ct) : null;
        var extractedAt = clock.UtcNow;
        var document = new TrialBalancePrintDocument(report, company.Name, names,
            center is null ? null : center.Code + " — " + center.Name, extractedAt);
        var bytes = TrialBalanceWorkbook.Create(document);
        Audit(cid, actor, "report.rpt01.export", company, new
        {
            report.BranchIds, report.From, report.To, report.Currency, report.CostCenterId,
            report.AccountLevel, report.ShowZero, RowCount = report.Rows.Count, ExtractedAt = extractedAt
        });
        await store.Save(ct);
        return bytes;
    }
}
