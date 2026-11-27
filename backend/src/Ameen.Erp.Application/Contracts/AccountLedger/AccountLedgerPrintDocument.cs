namespace Ameen.Erp.Application;

public sealed record AccountLedgerPrintDocument(AccountLedgerResult Report, string CompanyName,
    string AccountCode, string AccountName, IReadOnlyList<string> BranchNames, string? CostCenterName,
    IReadOnlyDictionary<Guid, string> CostCenters, IReadOnlyList<AccountLedgerPrintRow> Rows,
    DateTimeOffset ExtractedAt);
