namespace Ameen.Erp.Application;

// Detail rows are posting accounts; the selected summary is computed from
// the same materialized journal set and original report filters.
public sealed record TrialBalanceDrilldownResult(TrialBalanceRow Summary, TrialBalanceResult Detail, int? SummaryAccountLevel);
