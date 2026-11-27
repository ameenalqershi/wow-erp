using Ameen.Erp.Application;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Tests;

public static class AccountLedgerRangeTests
{
    public static async Task Run(Fixture f)
    {
        var date = Fixture.Date;
        var center = (await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(),
            new("RANGE", "مركز النطاق", f.BranchId), default)).ResourceId;
        async Task Post(decimal amount, DateOnly day, Guid branch, bool selected)
        {
            var request = f.Request(amount, amount, branch, day) with { ExternalReference = selected ? "000-RANGE" : "OTHER" };
            if (branch == f.BranchId)
                request = request with { Lines = request.Lines.Select(x => x with { CostCenterId = center }).ToArray() };
            await f.Post(await f.Draft(request));
        }
        await Post(40, date.AddDays(-1), f.BranchId, true);
        await Post(10, date, f.BranchId, true);
        await Post(5, date, f.OtherBranchId, true);
        await Post(9, date, f.BranchId, false);
        await Post(999, date.AddDays(1), f.BranchId, true);
        await f.Draft(f.Request(100,100) with { ExternalReference = "000-RANGE" });
        var outside = (await f.Service.Account.CreateAccount(f.CompanyId, f.Owner, Fixture.Key(),
            new("9000", "خارج النطاق", AccountType.Expense, null, true, false, false, null, NormalBalance.Debit), default)).ResourceId;
        var counts = (f.Store.Count<AuditEvent>(), f.Store.Count<JournalEntry>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<OutboxEvent>());
        Task<AccountLedgerRangeResult> Range(Actor actor, Guid first, Guid last, int offset = 0, int limit = 20,
            Guid? branch = null, Guid? costCenter = null, string? reference = "000-RANGE") =>
            f.Service.Report.AccountLedgerRange(f.CompanyId, first, last, actor, date, date, branch,
                offset, limit, default, costCenter, reference);
        var result = await Range(f.Limited, f.CashId, f.EquityId, costCenter: center);
        if (result.Accounts.Count != 2 || result.Accounts[0].AccountId != f.CashId ||
            result.Accounts[1].AccountId != f.EquityId || result.Entries.TotalCount != 2 ||
            result.Entries.Items.Any(x => x.AccountId == outside) ||
            result.BranchIds.Single() != f.BranchId || result.CostCenterId != center ||
            result.ExternalReference != "000-RANGE" || result.FromAccountId != f.CashId || result.ToAccountId != f.EquityId)
            throw new Exception("RPT02-FG03 range scope mismatch");
        foreach (var account in result.Accounts)
        {
            var single = await f.Service.Report.AccountLedger(f.CompanyId, account.AccountId, f.Limited, date, date,
                null, 0, 20, default, center, "000-RANGE");
            if ((account.OpeningBalance, account.PeriodDebit, account.PeriodCredit, account.ClosingBalance, account.MovementCount) !=
                (single.OpeningBalance, single.PeriodDebit, single.PeriodCredit, single.ClosingBalance, single.Entries.TotalCount) ||
                !result.Entries.Items.Where(x => x.AccountId == account.AccountId).Select(x => x.Entry).SequenceEqual(single.Entries.Items))
                throw new Exception("RPT02-FG03 range and single statement diverged");
        }
        if (result.Accounts[0].ClosingBalance != 50 || result.Accounts[1].ClosingBalance != -50)
            throw new Exception("RPT02-FG03 wrong scoped historical balances");
        var second = await Range(f.Limited, f.CashId, f.EquityId, 1, 1, costCenter: center);
        if (second.Entries.Items.Single() != result.Entries.Items[1] || !second.Accounts.SequenceEqual(result.Accounts))
            throw new Exception("RPT02-FG03 pagination lost account totals or stable order");
        var empty = await Range(f.Limited, f.CashId, f.EquityId, 2, 1, costCenter: center);
        if (empty.Entries.Items.Count != 0 || empty.Entries.TotalCount != 2 || !empty.Accounts.SequenceEqual(result.Accounts))
            throw new Exception("RPT02-FG03 empty page lost totals");
        var both = await Range(f.Clerk, f.CashId, f.EquityId);
        if (both.Accounts[0].ClosingBalance != 55 || both.Accounts[1].ClosingBalance != -55 || both.Entries.TotalCount != 4)
            throw new Exception("RPT02-FG03 multi-branch balances mismatch");
        var exact = await Range(f.Limited, f.CashId, f.CashId, reference: "000-range");
        if (exact.Entries.TotalCount != 0 || exact.Accounts.Single().ClosingBalance != 0)
            throw new Exception("RPT02-FG03 external reference was not exact");
        async Task Reject(string code, Func<Task> action)
        {
            try { await action(); }
            catch (BusinessException e) when (e.Code == code) { return; }
            throw new Exception("Expected " + code);
        }
        await Reject("invalid_account_range", () => Range(f.Clerk, f.EquityId, f.CashId));
        await Reject("reference_not_found", () => Range(f.Clerk, Guid.NewGuid(), f.CashId));
        await Reject("permission_denied", () => Range(new Actor("range-outsider"), f.CashId, f.EquityId));
        await Reject("branch_access_denied", () => Range(f.Limited, f.CashId, f.EquityId, branch: f.OtherBranchId));
        await Reject("invalid_pagination", () => Range(f.Clerk, f.CashId, f.EquityId, limit: 201));
        if (counts != (f.Store.Count<AuditEvent>(), f.Store.Count<JournalEntry>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<OutboxEvent>()))
            throw new Exception("RPT02-FG03 range query produced command effects");
    }
}
