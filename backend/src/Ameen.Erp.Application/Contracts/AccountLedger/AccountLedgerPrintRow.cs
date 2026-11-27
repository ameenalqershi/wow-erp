namespace Ameen.Erp.Application;

public sealed record AccountLedgerPrintRow(LedgerRow Entry, string Source, Guid? CostCenterId);
