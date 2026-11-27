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
        // RPT02-OP04: one materialized statement, not a succession of page reads.
        var print = await f.Service.Report.AccountLedgerPrint(f.CompanyId, f.CashId, f.Clerk, date, date, null, default);
        if (!print.Rows.Select(x => x.Entry).SequenceEqual(rows) ||
            print.Rows.Any(x => x.Source != "Manual") ||
            print.Report.OpeningBalance != 40 || print.Report.ClosingBalance != 45 ||
            print.Report.PeriodDebit != 10 || print.Report.PeriodCredit != 5 ||
            print.BranchNames.Count != 2 || print.AccountCode != "1101")
            throw new Exception("RPT02 print omitted rows or changed statement");
        var restrictedPrint = await f.Service.Report.AccountLedgerPrint(f.CompanyId, f.CashId, f.Limited,
            date, date, null, default);
        if (restrictedPrint.Rows.Count != 2 || restrictedPrint.Rows.Any(x => x.Entry.JournalId != z.ResourceId) ||
            restrictedPrint.Report.ClosingBalance != 50 || restrictedPrint.BranchNames.Count != 1)
            throw new Exception("RPT02 print leaked another branch");
        // RPT02-OP02: an exhausted page must retain full scope and totals,
        // not reset the running balance to zero or hide prior movements.
        var exhausted = await Page(f.Clerk, 3, 1);
        if (exhausted.Entries.Items.Count != 0 || exhausted.Entries.TotalCount != 3 ||
            exhausted.Entries.Offset != 3 || exhausted.Entries.Limit != 1 ||
            exhausted.AccountId != f.CashId || exhausted.CompanyId != f.CompanyId ||
            exhausted.Currency != "SAR" || exhausted.From != date || exhausted.To != date ||
            exhausted.OpeningBalance != full.OpeningBalance || exhausted.PeriodDebit != full.PeriodDebit ||
            exhausted.PeriodCredit != full.PeriodCredit || exhausted.ClosingBalance != full.ClosingBalance ||
            !exhausted.BranchIds.ToHashSet().SetEquals(full.BranchIds))
            throw new Exception("RPT02 exhausted page lost statement totals or scope");
        var openingOnly = await f.Service.Report.AccountLedger(f.CompanyId, f.CashId, f.Clerk,
            date.AddDays(2), date.AddDays(2), null, 0, 1, default);
        if (openingOnly.Entries.TotalCount != 0 || openingOnly.OpeningBalance != 1044 ||
            openingOnly.ClosingBalance != 1044 || openingOnly.PeriodDebit != 0 || openingOnly.PeriodCredit != 0)
            throw new Exception("RPT02 empty historical period discarded carried balance");
        // RPT-CONTRACT-01/02: compare every posting account, not just the
        // cash debit, across the same authorized historical report scope.
        foreach (var actor in new[] { f.Clerk, f.Limited })
        {
            var trial = await f.Service.Report.TrialBalance(f.CompanyId, actor, date, date,
                null, default);
            if (trial.TotalOpeningDebit != trial.TotalOpeningCredit ||
                trial.TotalPeriodDebit != trial.TotalPeriodCredit ||
                trial.TotalClosingDebit != trial.TotalClosingCredit ||
                trial.Rows.Count != 2)
                throw new Exception("RPT01 functional currency trial balance is not balanced");
            foreach (var row in trial.Rows)
            {
                var statement = await f.Service.Report.AccountLedger(f.CompanyId, row.AccountId,
                    actor, date, date, null, 0, 1, default);
                if (statement.Currency != trial.Currency ||
                    !statement.BranchIds.ToHashSet().SetEquals(trial.BranchIds) ||
                    statement.OpeningBalance != row.OpeningDebit - row.OpeningCredit ||
                    statement.PeriodDebit != row.PeriodDebit || statement.PeriodCredit != row.PeriodCredit ||
                    statement.ClosingBalance != row.ClosingDebit - row.ClosingCredit ||
                    statement.ClosingBalance != statement.OpeningBalance + statement.PeriodDebit - statement.PeriodCredit)
                    throw new Exception("RPT01/RPT02 account-level reconciliation failed: " + row.Code);
            }
            var grouped = await f.Service.Report.TrialBalance(f.CompanyId, actor, date, date,
                null, default, accountLevel: 1);
            if (grouped.TotalPeriodDebit != trial.TotalPeriodDebit ||
                grouped.TotalPeriodCredit != trial.TotalPeriodCredit ||
                grouped.TotalClosingDebit - grouped.TotalClosingCredit !=
                    trial.TotalClosingDebit - trial.TotalClosingCredit)
                throw new Exception("RPT01 hierarchy duplicated underlying movements");
        }

        var restricted = await Page(f.Limited, 0, 10);
        if (restricted.Entries.TotalCount != 2 || restricted.PeriodCredit != 0 ||
            restricted.ClosingBalance != 50 || restricted.Entries.Items.Any(x => x.JournalId != z.ResourceId))
            throw new Exception("RPT02 branch scope included another branch's movement or balance");

        // RPT02-OP03: the public source key is (JournalId, Position), not
        // the printed number or an account-relative row index.
        var auditCount = f.Store.All<AuditEvent>().Count();
        var outboxCount = f.Store.All<OutboxEvent>().Count();
        var receiptCount = f.Store.All<IdempotencyRecord>().Count();
        foreach (var actor in new[] { f.Clerk, f.Limited })
        {
            var report = await Page(actor, 0, 10);
            foreach (var row in report.Entries.Items)
            {
                var journal = await f.Service.Journal.GetJournal(f.CompanyId, row.JournalId, actor, default);
                var line = journal.Lines.Single(x => x.Position == row.Position);
                if (journal.Id != row.JournalId || journal.CompanyId != f.CompanyId ||
                    !report.BranchIds.Contains(journal.BranchId) || journal.State != JournalState.Posted ||
                    journal.AccountingDate != row.AccountingDate || journal.Number != row.Number ||
                    line.AccountId != report.AccountId || line.Description != row.Description ||
                    line.FunctionalDebit != row.Debit || line.FunctionalCredit != row.Credit ||
                    line.CurrencyCode != row.CurrencyCode || line.Debit != row.OriginalDebit ||
                    line.Credit != row.OriginalCredit || line.ExchangeRate != row.ExchangeRate)
                    throw new Exception("RPT02 source drill-through returned a different journal line");
            }
        }
        await RejectSource("branch_access_denied", f.Limited, a.ResourceId);
        await RejectSource("permission_denied", new Actor("rpt02-outsider"), z.ResourceId);
        await RejectSource("reference_not_found", f.Clerk, Guid.NewGuid());
        if (auditCount != f.Store.All<AuditEvent>().Count() ||
            outboxCount != f.Store.All<OutboxEvent>().Count() ||
            receiptCount != f.Store.All<IdempotencyRecord>().Count())
            throw new Exception("Report/source read created accounting or command effects");

        // A later reversal must not erase the historical source or change
        // the prior period's line identity and amounts.
        var reversal = await f.Service.Journal.ReverseJournal(f.CompanyId, z.ResourceId, f.Clerk,
            Fixture.Key(), new(z.Version, date.AddDays(1), "تصحيح لاحق"), default);
        await f.Post(reversal);
        var historical = await Page(f.Clerk, 0, 10);
        var original = await f.Service.Journal.GetJournal(f.CompanyId, z.ResourceId, f.Clerk, default);
        if (!historical.Entries.Items.SequenceEqual(rows) || original.State != JournalState.Reversed ||
            original.Lines.Single(x => x.Position == 2).FunctionalDebit != 4)
            throw new Exception("Historical report lost its reversed source");

        async Task RejectSource(string code, Actor actor, Guid id)
        {
            try { await f.Service.Journal.GetJournal(f.CompanyId, id, actor, default); }
            catch (BusinessException e) when (e.Code == code) { return; }
            throw new Exception("Expected source access error: " + code);
        }
    }
}
