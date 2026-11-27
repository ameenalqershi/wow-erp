using Ameen.Erp.Application;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Tests;

public static class ReportBranchSelectionTests
{
    public static async Task Run(Fixture f)
    {
        var date = Fixture.Date;
        var third = await f.Service.Branch.CreateBranch(f.CompanyId, f.Owner, Fixture.Key(),
            new("THIRD", "فرع ثالث", "مدينة الاختبار", "عنوان الاختبار"), default);
        var center = await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(),
            new("OTHER", "مركز الفرع الثاني", f.OtherBranchId), default);
        await f.Post(await f.Draft(f.Request(4, 4, f.BranchId, date.AddDays(-1))));
        await f.Post(await f.Draft(f.Request(10, 10, f.BranchId)));
        await f.Post(await f.Draft(f.Request(20, 20, f.OtherBranchId)));
        await f.Post(await f.Draft(f.Request(30, 30, third.ResourceId)));
        await f.Post(await f.Draft(f.Request(50, 50, f.BranchId, date.AddDays(1))));

        async Task Compare(Actor actor, Guid[]? branches, decimal opening, decimal debit, Guid[] expected,
            Guid? single = null)
        {
            var trial = await f.Service.Report.TrialBalance(f.CompanyId, actor, date, date, single, default, branchIds: branches);
            var ledger = await f.Service.Report.AccountLedger(f.CompanyId, f.CashId, actor, date, date, single, 0, 50, default, branchIds: branches);
            var cash = trial.Rows.Single(x => x.AccountId == f.CashId);
            if (trial.CompanyId != f.CompanyId || ledger.CompanyId != f.CompanyId ||
                !expected.ToHashSet().SetEquals(trial.BranchIds) || !expected.ToHashSet().SetEquals(ledger.BranchIds) ||
                trial.BranchIds.Count != expected.Length || ledger.BranchIds.Count != expected.Length ||
                opening != cash.OpeningDebit || opening != ledger.OpeningBalance ||
                debit != cash.PeriodDebit || debit != ledger.PeriodDebit ||
                opening + debit != cash.ClosingDebit || opening + debit != ledger.ClosingBalance)
                throw new Exception("Report branch selection changed scope, opening, movement or closing");
        }
        Guid[] selected = [third.ResourceId, f.BranchId];
        await Compare(f.Owner, [..selected, f.BranchId], 4, 40, selected);
        await Compare(f.Limited, [], 4, 10, [f.BranchId]);
        await Compare(f.Owner, null, 0, 20, [f.OtherBranchId], f.OtherBranchId);

        async Task Reject(string code, Actor actor, Guid[] branches, Guid? single = null, Guid? centerId = null)
        {
            Func<Task>[] queries = [
                () => f.Service.Report.TrialBalance(f.CompanyId, actor, date, date, single, default, centerId, branchIds: branches),
                () => f.Service.Report.AccountLedger(f.CompanyId, f.CashId, actor, date, date, single, 0, 20, default, centerId, branchIds: branches)
            ];
            foreach (var query in queries)
            {
                try { await query(); }
                catch (BusinessException e) when (e.Code == code) { continue; }
                throw new Exception("Report did not reject selection with " + code);
            }
        }
        await Reject("branch_access_denied", f.Limited, selected);
        await Reject("reference_not_found", f.Owner, [f.BranchId, Guid.NewGuid()]);
        await Reject("ambiguous_branch_filter", f.Owner, selected, f.BranchId);
        await Reject("cost_center_mismatch", f.Owner, selected, centerId: center.ResourceId);
        await f.Service.Branch.SetBranchStatus(f.CompanyId, third.ResourceId, f.Owner, Fixture.Key(),
            new(third.Version, false, "إيقاف الفرع"), default);
        await Reject("branch_inactive", f.Owner, selected);
        await Compare(f.Owner, null, 4, 60, [f.BranchId, f.OtherBranchId, third.ResourceId]);
    }
}
