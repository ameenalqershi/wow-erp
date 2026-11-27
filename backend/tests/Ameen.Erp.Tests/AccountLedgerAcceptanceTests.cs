using Ameen.Erp.Application;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Tests;

public static class AccountLedgerAcceptanceTests
{
    public static async Task Run(Fixture f)
    {
        var date = Fixture.Date;
        var year = f.Store.All<FiscalYear>().Single().Id;
        Task<CommandReceipt> Series(Guid branch, string prefix, long next) =>
            f.Service.Numbering.Create(f.CompanyId, f.Owner, Fixture.Key(),
                new(NumberingDocumentKind.Journal, year, next, prefix, branch), default);
        Task<AccountLedgerResult> Page(Actor actor, int offset, int limit) =>
            f.Service.Report.AccountLedger(f.CompanyId, f.CashId, actor, date, date,
                null, offset, limit, default);

        await Series(f.BranchId, "Z-", 1);
        await Series(f.OtherBranchId, "A-", 90);
        await f.Post(await f.Draft(f.Request(40, 40, date: date.AddDays(-1))));

        // The Z series has the lower internal sequence and is posted first.
        // Its two cash lines must still follow the A journal on the statement.
        var debit = f.Request() with { Lines = [
            new(f.CashId, null, "دفعة أولى", "SAR", null, 6, 0),
            new(f.CashId, null, "دفعة ثانية", "SAR", null, 4, 0),
            new(f.EquityId, null, "مقابل الدفعة", "SAR", null, 0, 10)] };
        var z = await f.Post(await f.Draft(debit));
        var credit = f.Request(branch: f.OtherBranchId) with { Lines = [
            new(f.CashId, null, "سداد", "SAR", null, 0, 5),
            new(f.EquityId, null, "مقابل السداد", "SAR", null, 5, 0)] };
        var a = await f.Post(await f.Draft(credit));

        await f.Post(await f.Draft(f.Request(999, 999, date: date.AddDays(1))));
        await f.Draft();
        await f.Action(await f.Draft(), "submit");
        await f.Action(await f.Action(await f.Draft(), "submit"), "approve", f.Approver);

        var full = await Page(f.Clerk, 0, 10);
        if (full.OpeningBalance != 40 || full.PeriodDebit != 10 || full.PeriodCredit != 5 ||
            full.ClosingBalance != 45 || full.Entries.TotalCount != 3)
            throw new Exception("RPT02 included an unposted or out-of-period movement");
        var rows = full.Entries.Items;
        if (a.Number != "A-000090" || z.Number != "Z-000002" ||
            rows[0] != new LedgerRow(a.ResourceId, a.Number, date, 1, "سداد", 0, 5, 35, "SAR", 0, 5, 1) ||
            rows[1] != new LedgerRow(z.ResourceId, z.Number, date, 1, "دفعة أولى", 6, 0, 41, "SAR", 6, 0, 1) ||
            rows[2] != new LedgerRow(z.ResourceId, z.Number, date, 2, "دفعة ثانية", 4, 0, 45, "SAR", 4, 0, 1))
            throw new Exception("RPT02 expected source lines ordered by accounting date, printed number and position");

        for (var i = 0; i < rows.Count; i++)
        {
            var page = await Page(f.Clerk, i, 1);
            if (page.Entries.Items.Single() != rows[i] || page.Entries.TotalCount != 3 ||
                page.OpeningBalance != 40 || page.ClosingBalance != 45)
                throw new Exception("RPT02 pagination changed the source line, balance or totals");
        }
        var restricted = await Page(f.Limited, 0, 10);
        if (restricted.Entries.TotalCount != 2 || restricted.PeriodCredit != 0 ||
            restricted.ClosingBalance != 50 || restricted.Entries.Items.Any(x => x.JournalId != z.ResourceId))
            throw new Exception("RPT02 branch scope included another branch's movement or balance");
    }
}
