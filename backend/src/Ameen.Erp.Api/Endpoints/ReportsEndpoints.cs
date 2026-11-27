using Ameen.Erp.Application;
using Ameen.Erp.Domain;
using Microsoft.AspNetCore.Mvc;
using static Ameen.Erp.Api.ApiSupport;

namespace Ameen.Erp.Api;

internal static class ReportsEndpoints
{
    public static void Map(WebApplication app)
    {
        const string p = EndpointRoutes.Company;
        app.MapGet(p + "/reports/inventory-balances", async (Guid companyId, [FromQuery] string from, [FromQuery] string to,
            Guid? branchId, [FromQuery] Guid[]? branchIds, Guid? warehouseId, Guid? groupId, Guid? itemId, bool? showZero, int? offset, int? limit,
            ReportService s, HttpContext h, CancellationToken ct) =>
            Results.Ok(await s.InventoryBalances(companyId, Actor(h), Date(from), Date(to), branchId, warehouseId, groupId, itemId, showZero ?? true, offset ?? 0, limit ?? 50, ct, branchIds)))
            .Produces<InventoryBalanceResult>().RequireAuthorization().WithTags("RPT08 Inventory balances");
        app.MapGet(p + "/reports/items/{itemId:guid}/ledger", async (Guid companyId, Guid itemId, [FromQuery] string from, [FromQuery] string to,
            Guid? branchId, [FromQuery] Guid[]? branchIds, Guid? warehouseId, string? identityNumber, int? offset, int? limit,
            ReportService s, HttpContext h, CancellationToken ct) =>
            Results.Ok(await s.InventoryLedger(companyId, itemId, Actor(h), Date(from), Date(to), branchId, warehouseId, identityNumber, offset ?? 0, limit ?? 50, ct, branchIds)))
            .Produces<InventoryLedgerResult>().RequireAuthorization().WithTags("RPT09 Item movement ledger");
        app.MapGet(p + "/reports/items/{itemId:guid}/ledger/print", async (Guid companyId, Guid itemId, [FromQuery] string from, [FromQuery] string to,
            Guid? branchId, [FromQuery] Guid[]? branchIds, Guid? warehouseId, string? identityNumber, int? offset, int? limit,
            ReportService s, HttpContext h, CancellationToken ct) =>
        {
            var document = await s.InventoryLedgerPrint(companyId, itemId, Actor(h), Date(from), Date(to), branchId,
                warehouseId, identityNumber, offset ?? 0, limit ?? 50, ct, branchIds);
            h.Response.Headers["Cache-Control"] = "no-store, private";
            h.Response.Headers["X-Content-Type-Options"] = "nosniff";
            h.Response.Headers["X-Frame-Options"] = "DENY";
            h.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
            return Results.Content(InventoryLedgerPrintHtml.Render(document), "text/html; charset=utf-8");
        }).Produces<string>(StatusCodes.Status200OK, contentType: "text/html").RequireAuthorization().WithTags("RPT09 Item movement ledger");
        app.MapGet(p + "/reports/trial-balance", async (Guid companyId, [FromQuery] string from, [FromQuery] string to,
            Guid? branchId, [FromQuery] Guid[]? branchIds, Guid? costCenterId, int? accountLevel, bool? showZero, ReportService s, HttpContext h, CancellationToken ct) =>
            Results.Ok(await s.TrialBalance(companyId, Actor(h), Date(from), Date(to), branchId, ct, costCenterId, accountLevel, showZero ?? true, branchIds)))
            .Produces<TrialBalanceResult>().RequireAuthorization().WithTags("RPT01 Trial balance");
        app.MapGet(p + "/reports/trial-balance/print", async (Guid companyId, [FromQuery] string from, [FromQuery] string to,
            Guid? branchId, [FromQuery] Guid[]? branchIds, Guid? costCenterId, int? accountLevel, bool? showZero,
            ReportService s, HttpContext h, CancellationToken ct) =>
        {
            var document = await s.TrialBalancePrint(companyId, Actor(h), Date(from), Date(to),
                branchId, ct, costCenterId, accountLevel, showZero ?? true, branchIds);
            h.Response.Headers["Cache-Control"] = "no-store, private";
            h.Response.Headers["X-Content-Type-Options"] = "nosniff";
            h.Response.Headers["X-Frame-Options"] = "DENY";
            h.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
            return Results.Content(TrialBalancePrintHtml.Render(document), "text/html; charset=utf-8");
        }).Produces(StatusCodes.Status200OK, contentType: "text/html")
          .RequireAuthorization().WithTags("RPT01 Trial balance");
        app.MapGet(p + "/reports/trial-balance/{accountId:guid}/drilldown", async (Guid companyId, Guid accountId,
            [FromQuery] string from, [FromQuery] string to, Guid? branchId, [FromQuery] Guid[]? branchIds,
            Guid? costCenterId, int? accountLevel, bool? showZero, ReportService s, HttpContext h, CancellationToken ct) =>
            Results.Ok(await s.TrialBalanceDrilldown(companyId, accountId, Actor(h), Date(from), Date(to),
                branchId, ct, costCenterId, accountLevel, showZero ?? true, branchIds)))
            .Produces<TrialBalanceDrilldownResult>().RequireAuthorization().WithTags("RPT01 Trial balance");
        app.MapGet(p + "/reports/accounts/{accountId:guid}/ledger/print", async (Guid companyId, Guid accountId,
            [FromQuery] string from, [FromQuery] string to, Guid? branchId, [FromQuery] Guid[]? branchIds,
            Guid? costCenterId, string? externalReference, ReportService s, HttpContext h, CancellationToken ct) =>
        {
            var document = await s.AccountLedgerPrint(companyId, accountId, Actor(h), Date(from), Date(to),
                branchId, ct, costCenterId, externalReference, branchIds);
            h.Response.Headers["Cache-Control"] = "no-store, private";
            h.Response.Headers["X-Content-Type-Options"] = "nosniff";
            h.Response.Headers["X-Frame-Options"] = "DENY";
            h.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
            return Results.Content(AccountLedgerPrintHtml.Render(document), "text/html; charset=utf-8");
        }).Produces(StatusCodes.Status200OK, contentType: "text/html")
          .RequireAuthorization().WithTags("RPT02 Account ledger");
        app.MapGet(p + "/reports/accounts/{accountId:guid}/ledger", async (Guid companyId, Guid accountId,
            [FromQuery] string from, [FromQuery] string to, Guid? branchId, [FromQuery] Guid[]? branchIds, int? offset, int? limit, Guid? costCenterId, string? externalReference,
            ReportService s, HttpContext h, CancellationToken ct) =>
            Results.Ok(await s.AccountLedger(companyId, accountId, Actor(h), Date(from), Date(to), branchId, offset ?? 0, limit ?? 50, ct, costCenterId, externalReference, branchIds)))
            .Produces<AccountLedgerResult>().RequireAuthorization().WithTags("RPT02 Account ledger");
    }
}
