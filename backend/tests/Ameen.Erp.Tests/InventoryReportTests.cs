using Ameen.Erp.Application;
using Ameen.Erp.Domain;
namespace Ameen.Erp.Tests;
public static class InventoryReportTests
{
    private static Task<InventoryBalanceResult> Balance(Fixture f, DateOnly? from = null, DateOnly? to = null, int offset = 0, bool zero = true) =>
        f.Service.Report.InventoryBalances(f.CompanyId, f.Owner, from ?? Fixture.Date, to ?? Fixture.Date, null, null, null, null, zero, offset, 1, default);
    private static Task<InventoryLedgerResult> Ledger(Fixture f, Guid item, DateOnly? from = null, DateOnly? to = null, int offset = 0, string? identity = null) =>
        f.Service.Report.InventoryLedger(f.CompanyId, item, f.Owner, from ?? Fixture.Date, to ?? Fixture.Date, null, null, identity, offset, 1, default);
    private static async Task<(CreateInventoryOpeningRequest Request, CommandReceipt Posted)> Post(Fixture f, ItemTracking tracking = ItemTracking.None)
    {
        var (request, approved) = await InventoryOpeningPostingTests.Setup(f, tracking);
        return (request, await InventoryOpeningPostingTests.Post(f, approved, await InventoryOpeningPostingTests.Plan(f, approved)));
    }
    private static async Task Reverse(Fixture f, CommandReceipt source, DateOnly date)
    {
        var member = f.Store.All<Membership>().Single(x => x.Subject == InventoryOpeningTests.Writer.Subject);
        await f.Service.Membership.GrantMembership(f.CompanyId, f.Owner, Fixture.Key(), new(member.Subject,
            member.GrantedPermissions.Append(Permissions.InventoryReverse).ToArray(), true, Array.Empty<Guid>(), member.Version), default);
        var r = await f.Service.InventoryOpening.CreateReversal(f.CompanyId, source.ResourceId, InventoryOpeningTests.Writer, Fixture.Key(), new(source.Version, date, "تصحيح"), default);
        r = await InventoryOpeningTests.Action(f, r, "submit"); r = await InventoryOpeningTests.Action(f, r, "approve", new("stock-reviewer"));
        await f.Service.InventoryOpening.PostReversal(f.CompanyId, r.ResourceId, InventoryOpeningTests.Writer, Fixture.Key(), new(r.Version), default);
    }
    public static void Register(Action<string, Func<Fixture, Task>> test)
    {
        test("RPT08/09: approved stock has no balance until posted; inclusive date boundaries", async f =>
        {
            var (request, approved) = await InventoryOpeningPostingTests.Setup(f);
            Ensure((await Balance(f)).Rows.TotalCount == 0);
            await InventoryOpeningPostingTests.Post(f, approved, await InventoryOpeningPostingTests.Plan(f, approved));
            var item = request.Lines[0].ItemId; var row = (await Balance(f)).Rows.Items.Single();
            Ensure(row.OpeningQuantity == 0 && row.InQuantity == 2 && row.ClosingValue == 10.5m);
            Ensure((await Balance(f, to: Fixture.Date.AddDays(-1), from: Fixture.Date.AddDays(-1))).Rows.TotalCount == 0);
            var later = await Ledger(f, item, Fixture.Date.AddDays(1), Fixture.Date.AddDays(1));
            Ensure(later.OpeningQuantity == 2 && later.OpeningValue == 10.5m && later.Rows.TotalCount == 0 && later.ClosingValue == 10.5m);
        });
        test("RPT08/09: later reversal preserves historical as-of values and running balance across pages", async f =>
        {
            var (request, source) = await Post(f); var item = request.Lines[0].ItemId;
            await Reverse(f, source, Fixture.Date.AddDays(1));
            Ensure((await Balance(f)).ClosingValue == 10.5m);
            var second = await Ledger(f, item, to: Fixture.Date.AddDays(1), offset: 1);
            var row = second.Rows.Items.Single();
            Ensure(second.Rows.TotalCount == 2 && row.OutQuantity == 2 && row.RunningQuantity == 0 && row.RunningValue == 0 && row.ReversesMovementId != null);
            Ensure(row.DocumentUnits.Single().Quantity == -2 && row.DocumentUnits.Single().UnitCost == 5.25m && row.DocumentUnits.Single().Value == -10.5m);
            var onlyReverse = await Ledger(f, item, Fixture.Date.AddDays(1), Fixture.Date.AddDays(1));
            Ensure(onlyReverse.OpeningValue == 10.5m && onlyReverse.OutValue == 10.5m && onlyReverse.ClosingValue == 0);
            var beyond = await Ledger(f, item, to: Fixture.Date.AddDays(1), offset: 20);
            Ensure(beyond.Rows.Items.Count == 0 && beyond.InValue == 10.5m && beyond.OutValue == 10.5m && beyond.ClosingQuantity == 0);
        });
        test("RPT08: zero closing filter and totals are computed before pagination", async f =>
        {
            var (_, source) = await Post(f); var beyond = await Balance(f, offset: 10);
            Ensure(beyond.Rows.Items.Count == 0 && beyond.Rows.TotalCount == 1 && beyond.ClosingValue == 10.5m);
            await Reverse(f, source, Fixture.Date);
            var withZero = await Balance(f); Ensure(withZero.Rows.TotalCount == 1 && withZero.InValue == 10.5m && withZero.OutValue == 10.5m);
            Ensure((await Balance(f, zero: false)).Rows.TotalCount == 0);
        });
        test("RPT09: lot and serial number filters do not confuse card code or another identity", async f =>
        {
            var (request, _) = await Post(f, ItemTracking.Serial); var item = request.Lines[0].ItemId;
            var matching = await Ledger(f, item, identity: " id-1 ");
            Ensure(matching.ClosingQuantity == 1 && matching.Rows.Items.Single().SerialNumber == "ID-1");
            Ensure((await Ledger(f, item, identity: "UNKNOWN")).Rows.TotalCount == 0);
            await Error("invalid_code", () => Ledger(f, item, identity: "bad number"));
        });
        test("RPT08/09: branch and company scope are enforced before exposing stock", async f =>
        {
            var (request, _) = await Post(f); var item = request.Lines[0].ItemId;
            await Error("permission_denied", () => f.Service.Report.InventoryBalances(Guid.NewGuid(), f.Owner, Fixture.Date, Fixture.Date, null, null, null, null, true, 0, 10, default));
            await Error("branch_access_denied", () => f.Service.Report.InventoryLedger(f.CompanyId, item, f.Limited, Fixture.Date, Fixture.Date, f.OtherBranchId, null, null, 0, 10, default));
            var empty = await f.Service.Report.InventoryBalances(f.CompanyId, f.Owner, Fixture.Date, Fixture.Date, f.OtherBranchId, null, null, null, true, 0, 10, default);
            Ensure(empty.Rows.TotalCount == 0 && empty.ClosingValue == 0);
            await Error("warehouse_branch_mismatch", () => f.Service.Report.InventoryBalances(f.CompanyId, f.Owner, Fixture.Date, Fixture.Date, f.OtherBranchId, request.WarehouseId, null, null, true, 0, 10, default));
        });
        test("RPT08/09: stock valuation equals control ledger and reads create no effects", async f =>
        {
            var (request, _) = await Post(f); var count = f.Store.Count<AuditEvent>();
            var balance = await Balance(f); var row = balance.Rows.Items.Single();
            var ledger = await f.Service.Report.AccountLedger(f.CompanyId, row.InventoryAccountId, f.Owner, Fixture.Date, Fixture.Date, null, 0, 10, default);
            Ensure(balance.ClosingValue == ledger.ClosingBalance);
            var detail = await Ledger(f, request.Lines[0].ItemId);
            Ensure(detail.Rows.Items.Single().OpeningId == f.Store.All<InventoryOpening>().Single().Id && detail.InventoryOpeningsOnly && f.Store.Count<AuditEvent>() == count);
        });
        test("RPT08/09: inactive cards retain history and implicit branch scope excludes other stock", async f =>
        {
            var (request, _) = await Post(f); var item = f.Store.All<Item>().Single(x => x.Id == request.Lines[0].ItemId);
            await f.Service.Item.SetItemStatus(f.CompanyId, item.Id, f.Owner, Fixture.Key(), new(item.Version, false, "إيقاف"), default);
            Ensure((await Balance(f)).ClosingValue == 10.5m && (await Ledger(f, item.Id)).ClosingQuantity == 2);
            var reader = new Actor("other-stock-reader");
            await f.Service.Membership.GrantMembership(f.CompanyId, f.Owner, Fixture.Key(), new(reader.Subject, new[] { Permissions.Read }, false, new[] { f.OtherBranchId }, null), default);
            var report = await f.Service.Report.InventoryBalances(f.CompanyId, reader, Fixture.Date, Fixture.Date, null, null, null, null, true, 0, 10, default);
            Ensure(report.Rows.TotalCount == 0 && report.BranchIds.SequenceEqual(new[] { f.OtherBranchId }));
            await Error("branch_access_denied", () => f.Service.Report.InventoryLedger(f.CompanyId, item.Id, reader, Fixture.Date, Fixture.Date, null, request.WarehouseId, null, 0, 10, default));
        });
        test("RPT08/09 FG01: multiple branch selection, historical dates and denied scopes", async f =>
        {
            var (request, _) = await Post(f);
            var item = request.Lines[0].ItemId;
            var third = await f.Service.Branch.CreateBranch(f.CompanyId, f.Owner, Fixture.Key(),
                new("RPT-THIRD", "فرع ثالث", "مدينة", "عنوان"), default);
            var date = Fixture.Date;
            async Task Compare(Actor actor, Guid[]? selected, Guid[] expected, decimal value, Guid? single = null,
                Guid? warehouse = null, DateOnly? from = null, DateOnly? to = null)
            {
                var b = await f.Service.Report.InventoryBalances(f.CompanyId, actor, from ?? date, to ?? date,
                    single, warehouse, null, item, true, 0, 20, default, selected);
                var l = await f.Service.Report.InventoryLedger(f.CompanyId, item, actor, from ?? date, to ?? date,
                    single, warehouse, null, 0, 20, default, selected);
                Ensure(b.CompanyId == f.CompanyId && l.CompanyId == f.CompanyId &&
                    b.BranchIds.Length == expected.Length && l.BranchIds.Length == expected.Length &&
                    expected.ToHashSet().SetEquals(b.BranchIds) && expected.ToHashSet().SetEquals(l.BranchIds) &&
                    b.ClosingValue == value && l.ClosingValue == value &&
                    b.OpeningValue + b.InValue - b.OutValue == b.ClosingValue &&
                    l.OpeningValue + l.InValue - l.OutValue == l.ClosingValue);
            }
            Guid[] selected = [f.BranchId, third.ResourceId];
            await Compare(f.Owner, [..selected, f.BranchId], selected, 10.5m, warehouse: request.WarehouseId);
            await Compare(f.Owner, [f.OtherBranchId, third.ResourceId], [f.OtherBranchId, third.ResourceId], 0);
            await Compare(f.Limited, [], [f.BranchId], 10.5m);
            await Compare(f.Owner, null, [f.BranchId], 10.5m, single: f.BranchId, from: date.AddDays(1), to: date.AddDays(1));
            await Compare(f.Owner, selected, selected, 0, from: date.AddDays(-1), to: date.AddDays(-1));
            async Task Reject(string code, Actor actor, Guid[] branches, Guid? single = null, Guid? warehouse = null)
            {
                await Error(code, () => f.Service.Report.InventoryBalances(f.CompanyId, actor, date, date,
                    single, warehouse, null, item, true, 0, 10, default, branches));
                await Error(code, () => f.Service.Report.InventoryLedger(f.CompanyId, item, actor, date, date,
                    single, warehouse, null, 0, 10, default, branches));
            }
            await Reject("branch_access_denied", f.Limited, selected);
            await Reject("reference_not_found", f.Owner, [f.BranchId, Guid.NewGuid()]);
            await Reject("ambiguous_branch_filter", f.Owner, selected, f.BranchId);
            await Reject("warehouse_branch_mismatch", f.Owner, [f.OtherBranchId], warehouse: request.WarehouseId);
            await f.Service.Branch.SetBranchStatus(f.CompanyId, third.ResourceId, f.Owner, Fixture.Key(),
                new(third.Version, false, "إيقاف الفرع"), default);
            await Reject("branch_inactive", f.Owner, selected);
            await Compare(f.Owner, null, [f.BranchId, f.OtherBranchId, third.ResourceId], 10.5m);
        });
        foreach (var tracking in new[] { ItemTracking.Lot, ItemTracking.Serial })
        test($"RPT09 FG03/OP03: {tracking} exact identity, historical source and protected drill-through", async f =>
        {
            var (request, approved) = await InventoryOpeningPostingTests.Setup(f, tracking, "00017");
            var posted = await InventoryOpeningPostingTests.Post(f, approved, await InventoryOpeningPostingTests.Plan(f, approved));
            var item = request.Lines.Single().ItemId;
            var original = await Ledger(f, item, identity: " 00017 ");
            var row = original.Rows.Items.Single();
            Ensure(row.IdentityId == request.Lines.Single().IdentityId &&
                (row.LotNumber ?? row.SerialNumber) == "00017" && row.OpeningId == posted.ResourceId);
            Ensure((await Ledger(f, item, identity: "17")).Rows.TotalCount == 0);
            Ensure((await Ledger(f, item, identity: "000170")).Rows.TotalCount == 0);
            await Error("reference_not_found", () => Ledger(f, Guid.NewGuid()));
            await Error("reference_not_found", () => f.Service.Report.InventoryLedger(f.CompanyId, item, f.Owner,
                Fixture.Date, Fixture.Date, null, Guid.NewGuid(), null, 0, 10, default));
            await Reverse(f, posted, Fixture.Date.AddDays(1));
            var reversal = (await Ledger(f, item, to: Fixture.Date.AddDays(1), offset: 1, identity: "00017")).Rows.Items.Single();
            var counts = (f.Store.Count<AuditEvent>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<StockMovement>(),
                f.Store.Count<JournalEntry>(), f.Store.Count<OutboxEvent>());
            foreach (var movement in new[] { row, reversal })
            {
                var source = await f.Service.InventoryOpening.Get(f.CompanyId, movement.OpeningId, f.Owner, default);
                var journal = await f.Service.Journal.GetJournal(f.CompanyId, movement.JournalId, f.Owner, default);
                var saved = source.Lines.Single();
                Ensure(source.Number == movement.DocumentNumber && source.WarehouseId == movement.WarehouseId &&
                    source.JournalId == journal.Id && journal.InventoryOpeningId == source.Id &&
                    saved.ItemId == item && saved.IdentityId == movement.IdentityId &&
                    saved.UnitId == movement.DocumentUnits.Single().UnitId &&
                    saved.UnitCost == movement.DocumentUnits.Single().UnitCost &&
                    movement.InValue + movement.OutValue == saved.Value &&
                    journal.Lines.Sum(x => x.FunctionalDebit) == saved.Value &&
                    journal.Lines.Sum(x => x.FunctionalCredit) == saved.Value);
                var outsider = new Actor("rpt09-no-membership");
                await Error("permission_denied", () => f.Service.InventoryOpening.Get(f.CompanyId, movement.OpeningId, outsider, default));
                await Error("permission_denied", () => f.Service.Journal.GetJournal(f.CompanyId, movement.JournalId, outsider, default));
            }
            Ensure(reversal.ReversesMovementId == row.MovementId && reversal.RunningQuantity == 0 && reversal.RunningValue == 0);
            Ensure(counts == (f.Store.Count<AuditEvent>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<StockMovement>(),
                f.Store.Count<JournalEntry>(), f.Store.Count<OutboxEvent>()));
        });
        test("RPT09 OP02/AC01: mixed document units, same-day reversal and full-range totals", async f =>
        {
            var (request, _) = await InventoryOpeningPostingTests.Setup(f);
            var itemId = request.Lines[0].ItemId;
            var item = f.Store.All<Item>().Single(x => x.Id == itemId);
            var box = item.Units.Single(x => x.FactorToBase == 12);
            var combined = await InventoryOpeningTests.Create(f, request with
            {
                CountReference = "RPT09-MIXED",
                Lines = new[] { request.Lines[0] with { Quantity = 3, UnitCost = 2.5m },
                    request.Lines[0] with { UnitId = box.UnitId, Quantity = 2, UnitCost = 30m } }
            });
            combined = await InventoryOpeningTests.Action(f, combined, "submit");
            combined = await InventoryOpeningTests.Action(f, combined, "approve", new Actor("stock-reviewer"));
            combined = await InventoryOpeningPostingTests.Post(f, combined, await InventoryOpeningPostingTests.Plan(f, combined));
            var initial = await Ledger(f, itemId);
            var first = initial.Rows.Items.Single();
            Ensure(initial.BaseUnitId == item.BaseUnitId && initial.OpeningQuantity == 0 && initial.InQuantity == 27 &&
                initial.ClosingQuantity == 27 && initial.InValue == 67.5m && initial.ClosingValue == 67.5m &&
                first.OpeningId == combined.ResourceId && first.InQuantity == 27 && first.RunningQuantity == 27 &&
                first.DocumentUnits.Length == 2 && first.DocumentUnits.Sum(x => x.Quantity * x.FactorToBase) == 27 &&
                first.DocumentUnits.Sum(x => x.Value) == 67.5m && first.DocumentUnits[0].UnitCost == 2.5m &&
                first.DocumentUnits[1].UnitId == box.UnitId && first.DocumentUnits[1].FactorToBase == 12 &&
                first.DocumentUnits[1].Quantity == 2 && first.DocumentUnits[1].UnitCost == 30m);
            await Reverse(f, combined, Fixture.Date);
            var page0 = await Ledger(f, itemId);
            var page1 = await Ledger(f, itemId, offset: 1);
            var empty = await Ledger(f, itemId, offset: 2);
            foreach (var result in new[] { page0, page1, empty })
                Ensure(result.InventoryOpeningsOnly && result.Rows.TotalCount == 2 && result.InQuantity == 27 &&
                    result.OutQuantity == 27 && result.InValue == 67.5m && result.OutValue == 67.5m &&
                    result.ClosingQuantity == 0 && result.ClosingValue == 0);
            Ensure(page0.Rows.Items.Single().MovementId == first.MovementId && page0.Rows.Items.Single().RunningValue == 67.5m);
            var reversal = page1.Rows.Items.Single();
            Ensure(reversal.ReversesMovementId == first.MovementId && reversal.RunningQuantity == 0 && reversal.RunningValue == 0 &&
                reversal.DocumentUnits.Length == 2 && reversal.DocumentUnits.Sum(x => x.Quantity * x.FactorToBase) == -27 &&
                reversal.DocumentUnits.Sum(x => x.Value) == -67.5m && reversal.DocumentUnits[1].UnitCost == 30m &&
                empty.Rows.Items.Count == 0);
        });
        test("RPT09 CTRL01/AC02: inconsistent historical units and recorded movement fail closed", async f =>
        {
            var (request, posted) = await Post(f);
            var item = request.Lines[0].ItemId;
            var movement = f.Store.All<StockMovement>().Single();
            var source = f.Store.All<InventoryOpening>().Single(x => x.Id == posted.ResourceId);
            var line = source.Lines.Single();
            var initial = (f.Store.Count<AuditEvent>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<StockMovement>(),
                f.Store.Count<JournalEntry>(), f.Store.Count<OutboxEvent>());
            async Task RejectCorruption(object target, string property, object incorrect)
            {
                var member = target.GetType().GetProperty(property)!;
                var original = member.GetValue(target);
                try
                {
                    member.SetValue(target, incorrect);
                    await Error("inventory_ledger_source_mismatch", () => Ledger(f, item));
                }
                finally { member.SetValue(target, original); }
                Ensure((await Ledger(f, item)).ClosingValue == 10.5m);
            }
            await RejectCorruption(movement, nameof(StockMovement.BaseQuantity), 3m);
            await RejectCorruption(movement, nameof(StockMovement.Value), 11.5m);
            await RejectCorruption(line, nameof(InventoryOpeningLine.FactorToBase), 2m);
            await RejectCorruption(movement, nameof(StockMovement.OpeningId), Guid.NewGuid());
            Ensure(initial == (f.Store.Count<AuditEvent>(), f.Store.Count<IdempotencyRecord>(), f.Store.Count<StockMovement>(),
                f.Store.Count<JournalEntry>(), f.Store.Count<OutboxEvent>()));
        });
        test("RPT08/09: invalid dates and paging are rejected", async f =>
        {
            await Error("invalid_date_range", () => Balance(f, to: Fixture.Date.AddDays(-1)));
            await Error("invalid_date_range", () => Ledger(f, Guid.NewGuid(), to: Fixture.Date.AddDays(-1)));
        });
    }
    private static void Ensure(bool value) { if (!value) throw new Exception("Inventory report assertion failed"); }
    private static async Task Error(string code, Func<Task> action)
    { try { await action(); } catch (BusinessException e) when (e.Code == code) { return; } throw new Exception("Expected " + code); }
}
