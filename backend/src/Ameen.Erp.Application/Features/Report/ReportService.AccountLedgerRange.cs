using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed partial class ReportService
{
    public async Task<AccountLedgerRangeResult> AccountLedgerRange(Guid cid, Guid fromAccountId, Guid toAccountId,
        Actor actor, DateOnly from, DateOnly to, Guid? branchId, int offset, int limit, CancellationToken ct,
        Guid? costCenterId = null, string? externalReference = null, Guid[]? branchIds = null)
    {
        Paging(offset, limit);
        Check.That(to >= from, "invalid_date_range", "نهاية التقرير قبل بدايته.");
        var branches = await ReportBranches(cid, actor, branchId, branchIds, costCenterId, ct);
        externalReference = string.IsNullOrWhiteSpace(externalReference) ? null :
            Check.Text(externalReference, "المرجع الخارجي", 100);
        var accounts = await store.Find<Account>(cid, x => true, ct);
        var start = accounts.SingleOrDefault(x => x.Id == fromAccountId);
        var end = accounts.SingleOrDefault(x => x.Id == toAccountId);
        if (start is null || end is null)
            throw new BusinessException("reference_not_found",
                "حدود نطاق الحسابات غير موجودة ضمن الشركة المحددة.", ErrorKind.NotFound);
        Check.That(start.IsActive && end.IsActive, "account_inactive", "حدد حسابات فعالة لحدود النطاق.");
        Check.That(StringComparer.Ordinal.Compare(start.Code, end.Code) <= 0,
            "invalid_account_range", "نهاية نطاق الحسابات قبل بدايته.");
        // Same ordinal code order as the existing trial balance. Historical
        // inactive posting accounts inside the interval retain their history.
        var selected = accounts.Where(x => x.IsPosting &&
            StringComparer.Ordinal.Compare(x.Code, start.Code) >= 0 &&
            StringComparer.Ordinal.Compare(x.Code, end.Code) <= 0)
            .OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        var ids = selected.Select(x => x.Id).ToArray();
        var company = await Company(cid, ct);
        var journals = await store.Find<JournalEntry>(cid, x => branches.Contains(x.BranchId) && x.AccountingDate <= to &&
            (x.State == JournalState.Posted || x.State == JournalState.Reversed) &&
            (externalReference == null || x.ExternalReference == externalReference) &&
            x.Lines.Any(l => ids.Contains(l.AccountId) && (costCenterId == null || l.CostCenterId == costCenterId)), ct);
        var idSet = ids.ToHashSet();
        var lines = journals.SelectMany(j => j.Lines.Where(l => idSet.Contains(l.AccountId) &&
            (costCenterId == null || l.CostCenterId == costCenterId)).Select(l => (Journal: j, Line: l)))
            .ToLookup(x => x.Line.AccountId);
        var summaries = new List<AccountLedgerRangeSummary>();
        var page = new List<AccountLedgerRangeRow>();
        var total = 0;
        foreach (var account in selected)
        {
            var statement = CalculateLedger(lines[account.Id], from, false);
            summaries.Add(new(account.Id, account.Code, account.Name, statement.Opening,
                statement.Debit, statement.Credit, statement.Closing, statement.Rows.Count));
            foreach (var row in statement.Rows)
            {
                if (total >= offset && page.Count < limit)
                    page.Add(new(account.Id, row));
                total = checked(total + 1);
            }
        }
        return new(cid, start.Id, end.Id, company.FunctionalCurrency, from, to, branches, costCenterId,
            externalReference, summaries, new(page, total, offset, limit));
    }
}
