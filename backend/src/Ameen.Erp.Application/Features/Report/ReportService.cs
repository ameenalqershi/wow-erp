using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed partial class ReportService : ApplicationService
{
    public ReportService(IErpStore store, ITransactionRunner transactions, IClock clock) : base(store, transactions, clock)
    {
    }


    private async Task<Guid[]> ReportBranches(Guid cid, Actor actor, Guid? branchId, Guid[]? branchIds,
        Guid? centerId, CancellationToken ct)
    {
        var member = await Authorize(cid, actor, Permissions.Read, branchId, ct);
        Check.That(branchId is null || branchIds is not { Length: > 0 },
            "ambiguous_branch_filter", "حدد الفروع باستخدام معامل واحد.");
        Guid[] selected = branchId is Guid single ? [single] : branchIds?.Distinct().ToArray() ?? [];
        if (selected.Length == 0)
            selected = await AllowedBranches(cid, member, null, ct);
        else
        {
            Check.That(selected.All(member.Allows), "branch_access_denied",
                "الفرع خارج نطاق صلاحياتك.", ErrorKind.Forbidden);
            var matches = await store.Find<Branch>(cid, x => selected.Contains(x.Id), ct);
            Check.That(matches.Count == selected.Length, "reference_not_found",
                "الفرع غير موجود ضمن الشركة المحددة.", ErrorKind.NotFound);
            Check.That(matches.All(x => x.IsActive), "branch_inactive", "الفرع معطل.");
        }
        if (centerId is Guid id)
        {
            var center = await Required<CostCenter>(cid, id, ct);
            Check.That(center.BranchId is null || member.Allows(center.BranchId.Value),
                "branch_access_denied", "مركز التكلفة خارج نطاق الفروع المسموحة.", ErrorKind.Forbidden);
            Check.That(center.BranchId is null || selected.Contains(center.BranchId.Value),
                "cost_center_mismatch", "مركز التكلفة خارج الفروع المحددة.");
        }
        return selected;
    }
    public async Task<TrialBalanceResult> TrialBalance(Guid cid, Actor actor, DateOnly from, DateOnly to, Guid? branchId,
        CancellationToken ct, Guid? costCenterId = null, int? accountLevel = null, bool showZero = true, Guid[]? branchIds = null)
        => (await BuildTrialBalance(cid, actor, from, to, branchId, ct, costCenterId, accountLevel,
            showZero, branchIds, null)).Report;

    public async Task<TrialBalanceDrilldownResult> TrialBalanceDrilldown(Guid cid, Guid accountId, Actor actor,
        DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct, Guid? costCenterId = null,
        int? accountLevel = null, bool showZero = true, Guid[]? branchIds = null)
    {
        var result = await BuildTrialBalance(cid, actor, from, to, branchId, ct, costCenterId, accountLevel,
            showZero, branchIds, accountId);
        return new(result.Selected!, result.Report, accountLevel);
    }

    private async Task<(TrialBalanceResult Report, TrialBalanceRow? Selected)> BuildTrialBalance(
        Guid cid, Actor actor, DateOnly from, DateOnly to, Guid? branchId, CancellationToken ct,
        Guid? costCenterId, int? accountLevel, bool showZero, Guid[]? branchIds, Guid? selectedId)
    {
        Check.That(to >= from, "invalid_date_range", "نهاية التقرير قبل بدايته.");
        Check.That(accountLevel is null or >= 1 and <= 20, "invalid_account_level", "مستوى دليل الحسابات بين 1 و20.");
        var branches = await ReportBranches(cid, actor, branchId, branchIds, costCenterId, ct);
        var company = await Company(cid, ct);
        var journals = await store.Find<JournalEntry>(cid, x => branches.Contains(x.BranchId) && x.AccountingDate <= to &&
            (x.State == JournalState.Posted || x.State == JournalState.Reversed), ct);
        var accounts = await store.Find<Account>(cid, x => true, ct);
        var byId = accounts.ToDictionary(x => x.Id);
        var paths = new Dictionary<Guid, Account[]>();
        foreach (var account in accounts)
        {
            var path = new List<Account>(); var visited = new HashSet<Guid>(); var current = account;
            while (true)
            {
                Check.That(visited.Add(current.Id), "invalid_account_tree", "توجد دورة في شجرة الحسابات.", ErrorKind.Conflict);
                path.Add(current);
                if (current.ParentId is not Guid parent) break;
                Check.That(byId.ContainsKey(parent), "invalid_account_tree", "الحساب الأب غير موجود ضمن الشركة.", ErrorKind.Conflict);
                current = byId[parent];
            }
            path.Reverse(); paths[account.Id] = path.ToArray();
        }
        Account Target(Guid id) => accountLevel is int level ? paths[id][Math.Min(level, paths[id].Length) - 1] : byId[id];
        var postingAccounts = accounts.Where(x => x.IsPosting).ToArray();
        var sourceLines = journals.SelectMany(j => j.Lines.Where(l => costCenterId == null || l.CostCenterId == costCenterId)
            .Select(l => (Date: j.AccountingDate, Line: l))).ToArray();
        var lines = sourceLines.ToLookup(x => Target(x.Line.AccountId).Id);
        TrialBalanceRow Row(Account a, IEnumerable<(DateOnly Date, JournalLine Line)> items)
        {
            var opening = items.Where(x => x.Date < from).Sum(x => x.Line.FunctionalDebit - x.Line.FunctionalCredit);
            var debit = items.Where(x => x.Date >= from).Sum(x => x.Line.FunctionalDebit);
            var credit = items.Where(x => x.Date >= from).Sum(x => x.Line.FunctionalCredit);
            var closing = opening + debit - credit;
            return new(a.Id, a.Code, a.Name, Math.Max(opening, 0), Math.Max(-opening, 0), debit, credit,
                Math.Max(closing, 0), Math.Max(-closing, 0), a.ParentId, paths[a.Id].Length, a.IsPosting);
        }
        bool Visible(TrialBalanceRow x) => showZero || x.OpeningDebit != 0 || x.OpeningCredit != 0 || x.PeriodDebit != 0 || x.PeriodCredit != 0;
        TrialBalanceRow? selected = null;
        TrialBalanceRow[] rows;
        if (selectedId is Guid id)
        {
            // Only a row that could have appeared in this exact report can
            // be expanded. No arbitrary group or other company's account.
            var displayed = postingAccounts.Select(x => Target(x.Id)).DistinctBy(x => x.Id)
                .SingleOrDefault(x => x.Id == id);
            if (displayed is null)
                throw new BusinessException("report_row_not_found", "الحساب غير موجود في مستوى التقرير المحدد.", ErrorKind.NotFound);
            selected = Row(displayed, lines[id]);
            Check.That(Visible(selected), "report_row_not_found", "الصف مستبعد من التقرير الحالي.", ErrorKind.NotFound);
            var leafLines = sourceLines.ToLookup(x => x.Line.AccountId);
            rows = postingAccounts.Where(x => Target(x.Id).Id == id)
                .OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => Row(x, leafLines[x.Id]))
                .Where(Visible).ToArray();
        }
        else
        {
            rows = postingAccounts.Select(x => Target(x.Id)).DistinctBy(x => x.Id)
                .OrderBy(x => x.Code, StringComparer.Ordinal).Select(x => Row(x, lines[x.Id]))
                .Where(Visible).ToArray();
        }
        return (new(cid, company.FunctionalCurrency, from, to, branches, rows,
            rows.Sum(x => x.OpeningDebit), rows.Sum(x => x.OpeningCredit),
            rows.Sum(x => x.PeriodDebit), rows.Sum(x => x.PeriodCredit), rows.Sum(x => x.ClosingDebit),
            rows.Sum(x => x.ClosingCredit), costCenterId, selectedId is null ? accountLevel : null, showZero), selected);
    }
    public async Task<AccountLedgerResult> AccountLedger(Guid cid, Guid accountId, Actor actor, DateOnly from,
        DateOnly to, Guid? branchId, int offset, int limit, CancellationToken ct, Guid? costCenterId = null, string? externalReference = null, Guid[]? branchIds = null)
    {
        Paging(offset, limit);
        return (await BuildAccountLedger(cid, accountId, actor, from, to, branchId, offset, limit, ct,
            costCenterId, externalReference, branchIds)).Report;
    }

    // A null limit is internal to print preview; public paging stays bounded.
    private async Task<(AccountLedgerResult Report, IReadOnlyList<AccountLedgerPrintRow> PrintRows)> BuildAccountLedger(
        Guid cid, Guid accountId, Actor actor, DateOnly from, DateOnly to, Guid? branchId,
        int offset, int? limit, CancellationToken ct, Guid? costCenterId, string? externalReference, Guid[]? branchIds)
    {
        Check.That(to >= from, "invalid_date_range", "نهاية التقرير قبل بدايته.");
        var branches = await ReportBranches(cid, actor, branchId, branchIds, costCenterId, ct);
        externalReference = string.IsNullOrWhiteSpace(externalReference) ? null : Check.Text(externalReference, "المرجع الخارجي", 100);
        await Required<Account>(cid, accountId, ct);
        var company = await Company(cid, ct);
        var journals = await store.Find<JournalEntry>(cid, x => branches.Contains(x.BranchId) && x.AccountingDate <= to &&
            (x.State == JournalState.Posted || x.State == JournalState.Reversed) &&
            (externalReference == null || x.ExternalReference == externalReference) &&
            x.Lines.Any(l => l.AccountId == accountId && (costCenterId == null || l.CostCenterId == costCenterId)), ct);
        var items = journals.OrderBy(x => x.AccountingDate).ThenBy(x => x.Number, StringComparer.Ordinal).ThenBy(x => x.Id)
            .SelectMany(j => j.Lines.Where(l => l.AccountId == accountId && (costCenterId == null || l.CostCenterId == costCenterId))
                .OrderBy(l => l.Position).Select(l => (Journal: j, Line: l))).ToArray();
        var opening = items.Where(x => x.Journal.AccountingDate < from).Sum(x => x.Line.FunctionalDebit - x.Line.FunctionalCredit);
        var running = opening; var rows = new List<LedgerRow>();
        var printRows = new List<AccountLedgerPrintRow>();
        foreach (var (journal, line) in items.Where(x => x.Journal.AccountingDate >= from))
        {
            running += line.FunctionalDebit - line.FunctionalCredit;
            var row = new LedgerRow(journal.Id, journal.Number, journal.AccountingDate, line.Position, line.Description,
                line.FunctionalDebit, line.FunctionalCredit, running, line.CurrencyCode, line.Debit, line.Credit, line.ExchangeRate);
            rows.Add(row);
            if (limit is null)
                printRows.Add(new(row, journal.Source.ToString(), line.CostCenterId));
        }
        return (new(accountId, company.FunctionalCurrency, from, to, opening, running,
            rows.Sum(x => x.Debit), rows.Sum(x => x.Credit),
            new(rows.Skip(offset).Take(limit ?? rows.Count).ToArray(), rows.Count, offset, limit ?? rows.Count),
            cid, branches, costCenterId, externalReference), printRows);
    }
}
