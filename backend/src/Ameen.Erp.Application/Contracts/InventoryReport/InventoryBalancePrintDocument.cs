namespace Ameen.Erp.Application;
public sealed record InventoryBalancePrintDocument(InventoryBalanceResult Report, string CompanyName,
    string[] BranchNames, string? WarehouseName, string? GroupName, string? ItemName, DateTimeOffset ExtractedAt);
