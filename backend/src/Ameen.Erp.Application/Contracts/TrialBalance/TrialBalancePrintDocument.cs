namespace Ameen.Erp.Application;

public sealed record TrialBalancePrintDocument(TrialBalanceResult Report, string CompanyName,
    IReadOnlyList<string> BranchNames, string? CostCenterName, DateTimeOffset ExtractedAt);
