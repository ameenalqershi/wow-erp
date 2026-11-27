using Ameen.Erp.Application;
using Ameen.Erp.Domain;

namespace Ameen.Erp.Tests;

public static class CostCenterTests
{
    public static void Register(Action<string, Func<Fixture, Task>> test)
    {
        test("COST01: bilingual hierarchy and responsible member roundtrip with scoped search", async f =>
        {
            var parent = await Create(f, "ROOT", null); var member = f.Store.All<Membership>().Single(x => x.Subject == f.Limited.Subject);
            var child = await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("001", "المبيعات", f.BranchId, parent.ResourceId, member.Id, "Sales", "ملاحظة"), default);
            var c = await Get(f, child); Ensure(c.Code == "001" && c.Revision == 1 && c.ResponsibleMembershipId == member.Id);
            var page = await f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Limited, "Sales", null, parent.ResourceId, true, 0, 10, default);
            Ensure(page.Items.Single().Id == c.Id);
        });
        test("COST01-FG01: server identity, leading zero code and Arabic name", async f =>
        {
            var request = new CreateCostCenterRequest("00042", "مركز المبيعات", null);
            var key = Fixture.Key();
            var receipt = await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, key, request, default);
            var card = await Get(f, receipt);
            Ensure(card.Id != Guid.Empty && card.Id == receipt.ResourceId && card.CompanyId == f.CompanyId &&
                card.Code == "00042" && card.Name == "مركز المبيعات");
            Ensure(await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, key, request, default) == receipt);
            var audits = f.Store.Count<AuditEvent>();
            await Error("duplicate_code", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), request, default));
            await Error("invalid_text", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), request with { Code = "00043", Name = " " }, default));
            await Error("invalid_code", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), request with { Code = "00 043" }, default));
            Ensure(f.Store.Count<AuditEvent>() == audits && (await Get(f, receipt)).Version == receipt.Version);
        });
        test("COST01-OP02: create is atomic and creates no accounting movement", async f =>
        {
            var parent = await Create(f, "NEW-PARENT", null);
            var request = new CreateCostCenterRequest("NEW-CENTER", "مركز جديد", f.BranchId,
                parent.ResourceId, EnglishName: "New center", Notes: "ملاحظات");
            var key = Fixture.Key();
            var centers = f.Store.Count<CostCenter>();
            var audits = f.Store.Count<AuditEvent>();
            var receipts = f.Store.Count<IdempotencyRecord>();
            var journals = f.Store.Count<JournalEntry>();
            var movements = f.Store.Count<StockMovement>();
            f.Store.FailBeforeCommit = true;
            try { await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, key, request, default); throw new Exception("Expected rollback"); }
            catch (IOException) { }
            Ensure(f.Store.Count<CostCenter>() == centers && f.Store.Count<AuditEvent>() == audits &&
                f.Store.Count<IdempotencyRecord>() == receipts);
            var result = await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, key, request, default);
            Ensure(await f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, key, request, default) == result);
            var card = await Get(f, result);
            Ensure(result.Status == "Created" && card.Revision == 1 && card.IsActive &&
                card.ParentId == parent.ResourceId && card.BranchId == f.BranchId &&
                card.EnglishName == request.EnglishName && card.Notes == request.Notes);
            Ensure(f.Store.Count<CostCenter>() == centers + 1 && f.Store.Count<AuditEvent>() == audits + 1 &&
                f.Store.Count<IdempotencyRecord>() == receipts + 1 && f.Store.Count<JournalEntry>() == journals &&
                f.Store.Count<StockMovement>() == movements);
        });
        test("COST01: direct and indirect cycles are rejected atomically", async f =>
        {
            var root = await Create(f, "ROOT", null); var child = await Create(f, "CHILD", null, root.ResourceId); var r = await Get(f, root);
            foreach (var parent in new[] { root.ResourceId, child.ResourceId })
                await Error("center_tree_cycle", () => f.Service.CostCenter.EditCostCenter(f.CompanyId, r.Id, f.Owner, Fixture.Key(), Edit(r) with { ParentId = parent }, default));
            Ensure((await Get(f, root)).Version == root.Version);
        });
        test("COST01: branch parent cannot own a global or another-branch child", async f =>
        {
            var root = await Create(f, "ROOT", f.BranchId);
            await Error("center_branch_mismatch", () => Create(f, "GLOBAL", null, root.ResourceId));
            await Error("center_branch_mismatch", () => Create(f, "OTHER", f.OtherBranchId, root.ResourceId));
            await Create(f, "VALID", f.BranchId, root.ResourceId);
        });
        test("COST01: parent branch narrowing cannot hide existing global or other-branch descendants", async f =>
        {
            var root = await Create(f, "ROOT", null); await Create(f, "OTHER", f.OtherBranchId, root.ResourceId);
            var c = await Get(f, root);
            await Error("center_branch_mismatch", () => f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, Fixture.Key(), Edit(c) with { BranchId = f.BranchId }, default));
        });
        test("COST01: only active same-company members with center scope can be responsible", async f =>
        {
            var limited = f.Store.All<Membership>().Single(x => x.Subject == f.Limited.Subject);
            await Error("invalid_center_responsible", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("GLOBAL", "عام", null, ResponsibleMembershipId: limited.Id), default));
            await Error("invalid_center_responsible", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("OTHER", "آخر", f.OtherBranchId, ResponsibleMembershipId: limited.Id), default));
            await f.Service.Membership.SetMembershipStatus(f.CompanyId, limited.Id, f.Owner, Fixture.Key(), new(limited.Version, false, "إيقاف المسؤول"), default);
            await Error("invalid_center_responsible", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("OFF", "معطل", f.BranchId, ResponsibleMembershipId: limited.Id), default));
            await Error("reference_not_found", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("BAD", "غير موجود", f.BranchId, ResponsibleMembershipId: Guid.NewGuid()), default));
        });
        test("COST01: disable parent requires inactive children; reactivate child requires active parent", async f =>
        {
            var root = await Create(f, "ROOT", null); var child = await Create(f, "CHILD", null, root.ResourceId);
            await Error("active_child_centers", () => Status(f, root, false));
            child = await Status(f, child, false); root = await Status(f, root, false);
            await Error("inactive_parent_center", () => Status(f, child, true));
            await Error("inactive_parent_center", () => Create(f, "NEW", null, root.ResourceId));
            root = await Status(f, root, true); await Status(f, child, true);
        });
        test("COST01-FG02/OP04: optional metadata, explicit state and rollback", async f =>
        {
            var parent = await Create(f, "META-PARENT", null);
            var child = await Create(f, "META-CHILD", f.BranchId, parent.ResourceId);
            var initial = await Get(f, child);
            Ensure(initial.EnglishName is null && initial.Notes is null && initial.IsActive &&
                initial.ParentId == parent.ResourceId);
            var edited = await f.Service.CostCenter.EditCostCenter(f.CompanyId, initial.Id, f.Owner, Fixture.Key(),
                Edit(initial) with { EnglishName = "Center Ω", Notes = "ملاحظات", ParentId = null }, default);
            var before = await Get(f, edited);
            Ensure(before.EnglishName == "Center Ω" && before.Notes == "ملاحظات" && before.ParentId is null && before.IsActive);
            var key = Fixture.Key();
            var beforeVersion = before.Version;
            var beforeRevision = before.Revision;
            var status = new SetCostCenterStatusRequest(beforeVersion, false, "تعطيل");
            var auditBefore = (await f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, before.Id, f.Owner, 0, 10, default)).TotalCount;
            f.Store.FailBeforeCommit = true;
            try { await f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, before.Id, f.Owner, key, status, default); throw new Exception("Expected rollback"); }
            catch (IOException) { }
            Ensure((await Get(f, edited)).Version == beforeVersion && (await Get(f, edited)).IsActive);
            var disabled = await f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, before.Id, f.Owner, key, status, default);
            Ensure(await f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, before.Id, f.Owner, key, status, default) == disabled);
            await Error("version_conflict", () => f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, before.Id, f.Owner, Fixture.Key(), status, default));
            await Error("permission_denied", () => f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, before.Id, f.Limited, Fixture.Key(), new(disabled.Version, true, "تفعيل"), default));
            var after = await Get(f, disabled);
            Ensure(!after.IsActive && after.Revision == beforeRevision + 1 && after.EnglishName == before.EnglishName &&
                after.Notes == before.Notes && after.ParentId == before.ParentId &&
                (await f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, before.Id, f.Owner, 0, 10, default)).TotalCount == auditBefore + 1);
        });
        test("COST01: usage in cancelled journals locks own and ancestor structure", async f =>
        {
            var root = await Create(f, "ROOT", null); var child = await Create(f, "CHILD", f.BranchId, root.ResourceId);
            var draft = await f.Draft(Request(f, child.ResourceId)); await f.Action(draft, "cancel", reason: "إلغاء");
            foreach (var receipt in new[] { root, child })
            {
                var c = await Get(f, receipt);
                await Error("center_structure_in_use", () => f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, Fixture.Key(), Edit(c) with { Code = "CHANGED" }, default));
            }
            var usage = await f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, root.ResourceId, f.Owner, 0, 10, default);
            Ensure(usage.HasUsageInSubtree && usage.JournalCount == 0 && usage.ChildCenterCount == 1);
            usage = await f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, child.ResourceId, f.Owner, 0, 10, default);
            Ensure(usage.JournalCount == 1 && usage.References.Items.Single().State == "Cancelled");
        });
        test("COST01: unused center can move between compatible parents with audit and revision", async f =>
        {
            var one = await Create(f, "ONE", null); var two = await Create(f, "TWO", null); var child = await Create(f, "CHILD", f.BranchId, one.ResourceId);
            var c = await Get(f, child);
            var r = await f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, Fixture.Key(), Edit(c) with { ParentId = two.ResourceId, EnglishName = "Moved" }, default);
            c = await Get(f, r); Ensure(c.ParentId == two.ResourceId && c.Revision == 2);
            Ensure((await f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, c.Id, f.Owner, 0, 10, default)).TotalCount == 2);
        });
        test("COST01: inactive center blocks posting while reports preserve past movement", async f =>
        {
            var center = await Create(f, "C", f.BranchId); await f.Post(await f.Draft(Request(f, center.ResourceId)));
            var draft = await f.Draft(Request(f, center.ResourceId)); var approved = await f.Action(await f.Action(draft, "submit"), "approve", f.Approver);
            center = await Status(f, center, false);
            await Error("cost_center_mismatch", () => f.Action(approved, "post"));
            var report = await f.Service.Report.TrialBalance(f.CompanyId, f.Clerk, Fixture.Date, Fixture.Date, null, default, center.ResourceId);
            Ensure(report.TotalClosingDebit == 100);
            center = await Status(f, center, true); await f.Action(approved, "post");
        });
        test("COST01: global card visibility never exposes another branch card or its usage", async f =>
        {
            var global = await Create(f, "GLOBAL", null); var other = await Create(f, "OTHER", f.OtherBranchId, global.ResourceId);
            var page = await f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Limited, null, null, null, null, 0, 20, default);
            Ensure(page.Items.Any(x => x.Id == global.ResourceId) && page.Items.All(x => x.Id != other.ResourceId));
            await Error("branch_access_denied", () => f.Service.CostCenter.GetCostCenter(f.CompanyId, other.ResourceId, f.Limited, default));
            await Error("branch_access_denied", () => f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Limited, null, null, other.ResourceId, null, 0, 10, default));
            await Error("all_branches_required", () => f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, global.ResourceId, f.Limited, 0, 10, default));
        });
        test("COST01-OP01: search filters and deterministic pages", async f =>
        {
            var root = await Create(f, "FIND-00", null);
            var child = await Create(f, "FIND-01", f.BranchId, root.ResourceId);
            var inactive = await Create(f, "FIND-02", f.BranchId, root.ResourceId);
            await Status(f, inactive, false);
            var first = await f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Owner, " FIND ", null, null, true, 0, 1, default);
            var next = await f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Owner, "FIND", null, null, true, 1, 1, default);
            Ensure(first.TotalCount == 2 && next.TotalCount == 2 && first.Items.Single().Id == root.ResourceId &&
                next.Items.Single().Id == child.ResourceId);
            var children = await f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Owner, null, f.BranchId, root.ResourceId, false, 0, 10, default);
            Ensure(children.TotalCount == 1 && children.Items.Single().Id == inactive.ResourceId);
            await Error("invalid_pagination", () => f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Owner, null, null, null, null, -1, 10, default));
            await Error("search_too_long", () => f.Service.CostCenter.SearchCostCenters(f.CompanyId, f.Owner, new string('A', 101), null, null, null, 0, 10, default));
            Ensure((await Get(f, child)).Version == child.Version);
        });
        test("COST01: same-key edit replays, stale version fails and rollback keeps revision", async f =>
        {
            var r = await Create(f, "C", null); var c = await Get(f, r); var request = Edit(c) with { Notes = "جديد" }; var key = Fixture.Key();
            f.Store.FailBeforeCommit = true;
            try { await f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, key, request, default); throw new Exception("Expected failure"); }
            catch (IOException) { }
            Ensure((await Get(f, r)).Revision == 1 && (await Get(f, r)).Notes is null);
            var changed = await f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, key, request, default);
            Ensure(await f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, key, request, default) == changed);
            await Error("version_conflict", () => f.Service.CostCenter.EditCostCenter(f.CompanyId, c.Id, f.Owner, Fixture.Key(), request, default));
            Ensure((await Get(f, r)).Revision == 2);
        });
        test("COST01-OP05: direct and descendant usage, paging and read-only history", async f =>
        {
            var root = await Create(f, "USAGE-ROOT", null);
            var child = await Create(f, "USAGE-CHILD", f.BranchId, root.ResourceId);
            var draft = await f.Draft(Request(f, child.ResourceId));
            await f.Action(draft, "cancel", reason: "إلغاء");
            var member = f.Store.All<Membership>().Single(x => x.Subject == f.Clerk.Subject);
            await f.Service.Membership.GrantMembership(f.CompanyId, f.Owner, Fixture.Key(),
                new(f.Clerk.Subject, Permissions.All, true, Array.Empty<Guid>(), member.Version), default);
            var template = await f.Service.RecurringJournal.CreateRecurringJournal(f.CompanyId, f.Clerk, Fixture.Key(),
                new(f.BranchId, "قالب استخدام", RecurringFrequency.Monthly, RecurringGenerationMode.Manual, Fixture.Date, Fixture.Date,
                    Request(f, child.ResourceId).Lines.Select(x => new RecurringLineRequest(x.AccountId, x.CostCenterId, x.Description, x.CurrencyCode, x.Debit, x.Credit)).ToArray()), default);
            await f.Service.RecurringJournal.SetRecurringStatus(f.CompanyId, template.ResourceId, f.Clerk, Fixture.Key(), new(template.Version, false, "تعطيل"), default);
            var before = await Get(f, child);
            var auditBefore = f.Store.All<AuditEvent>().Count();
            var parentUsage = await f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, root.ResourceId, f.Owner, 0, 10, default);
            Ensure(parentUsage.ChildCenterCount == 1 && parentUsage.HasUsageInSubtree && parentUsage.References.TotalCount == 0);
            var one = await f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, child.ResourceId, f.Owner, 0, 1, default);
            var two = await f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, child.ResourceId, f.Owner, 1, 1, default);
            Ensure(one.JournalCount == 1 && one.RecurringTemplateCount == 1 && one.HasUsageInSubtree &&
                one.References.TotalCount == 2 && two.References.TotalCount == 2 &&
                one.References.Items.Single().ResourceId == draft.ResourceId && one.References.Items.Single().State == "Cancelled" &&
                two.References.Items.Single().ResourceId == template.ResourceId && two.References.Items.Single().State == "Inactive");
            await Error("invalid_pagination", () => f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, child.ResourceId, f.Owner, 0, 201, default));
            await Error("all_branches_required", () => f.Service.CostCenter.GetCostCenterUsage(f.CompanyId, root.ResourceId, f.Limited, 0, 10, default));
            Ensure((await Get(f, child)).Version == before.Version && f.Store.All<AuditEvent>().Count() == auditBefore);
        });
        test("COST01-FG04/OP06: audit history is scoped, paged and unchanged on stale edit", async f =>
        {
            var r = await Create(f, "AUDIT", f.BranchId);
            var original = await Get(f, r);
            var request = Edit(original) with { Notes = "audit note" };
            var key = Fixture.Key();
            var edited = await f.Service.CostCenter.EditCostCenter(f.CompanyId, r.ResourceId, f.Owner, key, request, default);
            Ensure(await f.Service.CostCenter.EditCostCenter(f.CompanyId, r.ResourceId, f.Owner, key, request, default) == edited);
            await Error("version_conflict", () => f.Service.CostCenter.EditCostCenter(f.CompanyId, r.ResourceId, f.Owner, Fixture.Key(), request, default));
            var disabled = await Status(f, edited, false);
            var history = await f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, r.ResourceId, f.Owner, 0, 10, default);
            Ensure(history.TotalCount == 3 && history.Items.All(x => x.CompanyId == f.CompanyId &&
                x.ResourceId == r.ResourceId && x.ActorId == f.Owner.Subject && x.OccurredAt == new FixedClock().UtcNow));
            Ensure(history.Items.Select(x => x.Action).ToHashSet().SetEquals(new[] { "cost-center.created", "cost-center.edited", "cost-center.status-changed" }));
            var pages = new List<Guid>();
            for (var offset = 0; offset < 3; offset++)
            {
                var page = await f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, r.ResourceId, f.Owner, offset, 1, default);
                Ensure(page.TotalCount == 3);
                pages.Add(page.Items.Single().Id);
            }
            Ensure(pages.SequenceEqual(history.Items.Select(x => x.Id)));
            Ensure((await Get(f, disabled)).Revision == 3);
            await Error("permission_denied", () => f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, r.ResourceId, f.Clerk, 0, 10, default));
            var scoped = new Actor("cost-audit-scoped");
            await f.Service.Membership.GrantMembership(f.CompanyId, f.Owner, Fixture.Key(),
                new(scoped.Subject, new[] { Permissions.Read, Permissions.Audit }, false, new[] { f.OtherBranchId }, null), default);
            await Error("all_branches_required", () => f.Service.CostCenter.ListCostCenterAudit(f.CompanyId, r.ResourceId, scoped, 0, 10, default));
            var other = Guid.NewGuid();
            await f.Service.Company.CreateCompany(f.Owner, Fixture.Key(), new(other, "OTHER", "أخرى", "YE", "SAR", 2, Fixture.Profile()), default);
            await Error("reference_not_found", () => f.Service.CostCenter.ListCostCenterAudit(other, r.ResourceId, f.Owner, 0, 10, default));
        });
        test("COST01: foreign-company parent and responsible member cannot be referenced", async f =>
        {
            var cid = Guid.NewGuid(); await f.Service.Company.CreateCompany(f.Owner, Fixture.Key(), new(cid, "OTHER", "أخرى", "YE", "SAR", 2, Fixture.Profile()), default);
            var parent = await f.Service.CostCenter.CreateCostCenter(cid, f.Owner, Fixture.Key(), new("C", "آخر", null), default);
            var member = f.Store.All<Membership>().Single(x => x.CompanyId == cid);
            await Error("reference_not_found", () => Create(f, "BAD", null, parent.ResourceId));
            await Error("reference_not_found", () => f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new("BAD", "آخر", null, ResponsibleMembershipId: member.Id), default));
        });
    }
    public static EditCostCenterRequest Edit(CostCenter c) => new(c.Version, c.Code, c.Name, c.BranchId, c.ParentId, c.ResponsibleMembershipId, c.EnglishName, c.Notes, "تحديث المركز");
    private static Task<CommandReceipt> Create(Fixture f, string code, Guid? branch, Guid? parent = null) =>
        f.Service.CostCenter.CreateCostCenter(f.CompanyId, f.Owner, Fixture.Key(), new(code, "مركز " + code, branch, parent), default);
    private static Task<CostCenter> Get(Fixture f, CommandReceipt r) => f.Service.CostCenter.GetCostCenter(f.CompanyId, r.ResourceId, f.Owner, default);
    private static Task<CommandReceipt> Status(Fixture f, CommandReceipt r, bool active) => f.Service.CostCenter.SetCostCenterStatus(f.CompanyId, r.ResourceId, f.Owner, Fixture.Key(), new(r.Version, active, "تغيير الحالة"), default);
    public static CreateJournalRequest Request(Fixture f, Guid center) => f.Request() with { Lines = f.Request().Lines.Select(x => x with { CostCenterId = center }).ToArray() };
    private static void Ensure(bool condition) { if (!condition) throw new Exception("Cost center assertion failed"); }
    private static async Task Error(string code, Func<Task> action)
    {
        try { await action(); } catch (BusinessException e) when (e.Code == code) { return; }
        throw new Exception("Expected " + code);
    }
}
