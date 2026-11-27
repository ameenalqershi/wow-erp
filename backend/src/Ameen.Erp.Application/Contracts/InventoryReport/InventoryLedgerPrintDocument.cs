namespace Ameen.Erp.Application;
public sealed record InventoryLedgerPrintDocument(InventoryLedgerResult Report, string CompanyName,
    string[] BranchNames, string? WarehouseName, string BaseUnitCode, DateTimeOffset ExtractedAt);
