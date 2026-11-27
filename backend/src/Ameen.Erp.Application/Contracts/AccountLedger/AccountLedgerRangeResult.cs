using Ameen.Erp.Domain;

namespace Ameen.Erp.Application;

public sealed record AccountLedgerRangeResult(Guid CompanyId, Guid FromAccountId, Guid ToAccountId,
    string Currency, DateOnly From, DateOnly To, IReadOnlyList<Guid> BranchIds, Guid? CostCenterId,
    string? ExternalReference, IReadOnlyList<AccountLedgerRangeSummary> Accounts, Page<AccountLedgerRangeRow> Entries);

public sealed record AccountLedgerRangeSummary(Guid AccountId, string Code, string Name,
    decimal OpeningBalance, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingBalance, int MovementCount);

public sealed record AccountLedgerRangeRow(Guid AccountId, LedgerRow Entry);
