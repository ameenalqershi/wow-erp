using Ameen.Erp.Application;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Tests;

public static class TrialBalanceAcceptanceTests
{
    public static async Task Run(Fixture f)
    {
        var date = Fixture.Date;
        await f.Post(await f.Draft(f.Request(40, 40, date: date.AddDays(-1))));
        await f.Post(await f.Draft(f.Request(20, 20)));
        await f.Post(await f.Draft(f.Request(9, 9, f.OtherBranchId)));
        await f.Post(await f.Draft(f.Request(999, 999, date: date.AddDays(1))));
        await f.Service.Currency.CreateCurrency(f.CompanyId, f.Owner, Fixture.Key(), new("USD", 2, "دولار"), default);
        var rate = await f.Service.ExchangeRate.CreateExchangeRate(f.CompanyId, f.Owner, Fixture.Key(),
            new("USD", date, 3.75m, "اختبار"), default);
        var fx = f.Request(10, 37.50m);
        fx.Lines[0] = fx.Lines[0] with { CurrencyCode = "USD", ExchangeRateId = rate.ResourceId };
        await f.Post(await f.Draft(fx));

        Task<TrialBalanceResult> Read(Actor actor, Guid? branch = null) =>
            f.Service.Report.TrialBalance(f.CompanyId, actor, date, date, branch, default, showZero: false);
        var full = await Read(f.Clerk);
        CheckReport(full, 40, 66.50m);
        CheckReport(await Read(f.Limited), 40, 57.50m);
        CheckReport(await Read(f.Clerk, f.OtherBranchId), 0, 9);

        var unbalanced = await f.Draft(f.Request(100, 99));
        var draft = await f.Draft();
        await f.Action(await f.Draft(), "submit");
        var approved = await f.Action(await f.Action(await f.Draft(), "submit"), "approve", f.Approver);
        var counts = (f.Store.Count<AuditEvent>(), f.Store.Count<OutboxEvent>(), f.Store.Count<IdempotencyRecord>());
        await Reject("journal_unbalanced", () => f.Action(unbalanced, "submit"));
        await Reject("branch_access_denied", () => Read(f.Limited, f.OtherBranchId));
        await Reject("permission_denied", () => Read(new Actor("trial-balance-outsider")));
        await Reject("invalid_date_range", () => f.Service.Report.TrialBalance(f.CompanyId, f.Clerk, date, date.AddDays(-1), null, default));
        var after = await Read(f.Clerk);
        if (!after.Rows.SequenceEqual(full.Rows) || (await f.Journal(unbalanced.ResourceId)).State != JournalState.Draft ||
            (await f.Journal(draft.ResourceId)).State != JournalState.Draft ||
            (await f.Journal(approved.ResourceId)).State != JournalState.Approved ||
            counts != (f.Store.Count<AuditEvent>(), f.Store.Count<OutboxEvent>(), f.Store.Count<IdempotencyRecord>()))
            throw new Exception("RPT01 rejected or unposted journal affected report or command effects");
        CheckReport(after, 40, 66.50m);

        void CheckReport(TrialBalanceResult report, decimal opening, decimal movement)
        {
            if (report.Currency != "SAR" || report.CompanyId != f.CompanyId || report.From != date || report.To != date ||
                report.Rows.Count != 2 || report.TotalOpeningDebit != opening || report.TotalOpeningCredit != opening ||
                report.TotalPeriodDebit != movement || report.TotalPeriodCredit != movement ||
                report.TotalClosingDebit != opening + movement || report.TotalClosingCredit != opening + movement)
                throw new Exception("RPT01 wrong functional currency scope or totals");
            var cash = report.Rows.Single(x => x.AccountId == f.CashId);
            var equity = report.Rows.Single(x => x.AccountId == f.EquityId);
            if (cash.Code != "1101" || cash.Name != "نقدية" || equity.Code != "3001" || equity.Name != "رأس المال" ||
                cash.OpeningDebit != opening || cash.PeriodDebit != movement || cash.ClosingDebit != opening + movement ||
                equity.OpeningCredit != opening || equity.PeriodCredit != movement || equity.ClosingCredit != opening + movement ||
                cash.OpeningCredit != 0 || cash.PeriodCredit != 0 || cash.ClosingCredit != 0 ||
                equity.OpeningDebit != 0 || equity.PeriodDebit != 0 || equity.ClosingDebit != 0)
                throw new Exception("RPT01 wrong account identity or debit/credit columns");
            AssertEquation(report);
        }
    }

    public static void AssertEquation(TrialBalanceResult result)
    {
        foreach (var row in result.Rows)
            if (row.OpeningDebit - row.OpeningCredit + row.PeriodDebit - row.PeriodCredit != row.ClosingDebit - row.ClosingCredit)
                throw new Exception("RPT01 opening + movement != closing for " + row.Code);
        if (result.TotalOpeningDebit != result.TotalOpeningCredit || result.TotalPeriodDebit != result.TotalPeriodCredit ||
            result.TotalClosingDebit != result.TotalClosingCredit ||
            result.TotalOpeningDebit != result.Rows.Sum(x => x.OpeningDebit) ||
            result.TotalOpeningCredit != result.Rows.Sum(x => x.OpeningCredit) ||
            result.TotalPeriodDebit != result.Rows.Sum(x => x.PeriodDebit) ||
            result.TotalPeriodCredit != result.Rows.Sum(x => x.PeriodCredit) ||
            result.TotalClosingDebit != result.Rows.Sum(x => x.ClosingDebit) ||
            result.TotalClosingCredit != result.Rows.Sum(x => x.ClosingCredit))
            throw new Exception("RPT01 unbalanced or inconsistent whole-branch totals");
    }

    private static async Task Reject(string code, Func<Task> action)
    {
        try { await action(); }
        catch (BusinessException e) when (e.Code == code) { return; }
        throw new Exception("Expected " + code);
    }
}
