# RPT-CONTRACT-01 / RPT-CONTRACT-02 — contract audit

Issues [#2011](https://github.com/ameenalqershi/wow-erp/issues/2011) and [#2012](https://github.com/ameenalqershi/wow-erp/issues/2012). Scope is coverage audit, not completion of RPT01/02 screens. Reviewed against main ecd9e4a.

## Sources and field trace

- [Report contract and reconciliation](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md), [screen definitions](../../muhaseb-erp-analysis/docs/modules/RPT.md), [navigation](../../muhaseb-erp-analysis/diagrams/source/module-RPT.mmd).
- [Original field dictionary](../../muhaseb-erp-analysis/data/fields.csv), [implementation matrix](fields-implementation.csv), [issue crosswalk](field-task-crosswalk.csv).
- [Rules BR01, BR02, BR18 and accounting events](../../muhaseb-erp-analysis/docs/02-accounting-and-business-rules.md); RPT01 links GL01/GL10, RPT02 links GL03.

| Original fields | Actual binding | Coverage |
| --- | --- | --- |
| RPT01-F01–F04 / RPT02-F01–F04 | CompanyId, branchId/branchIds, from, to | Implemented; company and branch authorization, explicit selection active, omitted historical scope |
| RPT01-F05 / RPT02-F05 | Currency (functional); LedgerRow original currency, debit/credit and exchange rate | Partial; no display currency selection |
| RPT01-F06 / RPT02-F06 | No conversion-mode input | Missing |
| RPT01-F07 / RPT02-F07 | Posted/Reversed journals only | Partial; no selectable document status |
| RPT01-F08 / RPT02-F08 | No detail-mode input | Missing |
| RPT01-F09 | accountLevel, 1–20 or posting leaves | Implemented; no double counting |
| RPT01-F10 / RPT02-F11 | costCenterId; authorized historical reference | Implemented |
| RPT01-F11 | showZero; retains offsetting activity | Implemented |
| RPT02-F09 | accountId route | Partial; single account, not lower range bound |
| RPT02-F10 | No upper account bound | Missing |
| RPT02-F12 | externalReference | Implemented; exact match intended; SQL collation equivalence must be independently verified |

23 distinct fields: 13 implemented, 5 partial, 5 pending. Neither screen is marked complete.

## Outputs and accounting reconciliation

| Required | Actual evidence | Disposition |
| --- | --- | --- |
| Trial: account code/name, opening, movement, closing debit/credit | TrialBalanceRow Code, Name, OpeningDebit/Credit, PeriodDebit/Credit, ClosingDebit/Credit | Present |
| Ledger: date, source type/number, description, debit/credit, running balance, cost center, currency | LedgerRow AccountingDate, JournalId, Number, Position, Description, Debit/Credit, RunningBalance, CurrencyCode, OriginalDebit/Credit, ExchangeRate | Partial: type and per-line cost center absent; JournalId/Position identify source; number is journal number, not necessarily business document number |
| Scope / extraction time / status / conversion mode | Both result contracts echo company/branches/dates/functional currency; no extraction timestamp, explicit status or mode | Partial |
| Single source | ReportService reads tenant-scoped JournalEntry.Lines with accounting date <= to and Posted/Reversed states; both use FunctionalDebit/Credit (decimal) | Present for existing ledger |
| Reconcile | Per posting account: opening signed + period debit − period credit = closing signed; same figures in trial and ledger. Aggregate whole selected branches balance; filtering individual cost-center lines may legitimately produce unequal sides and must not fabricate balancing lines | Present for tested scope |
| Pagination and hierarchy | Ledger sorts date, ordinal printed journal number, journal id, line position; builds running balance before paging. Trial groups posting leaves once at requested depth | Present |
| Export/print | ReportsEndpoints has GET only; no report export/print contracts or parity tests | Missing; no claim of export authorization |
| Source drill-down | JournalId and Position are available; journal read authorization exists; dedicated report-to-source UI not in backend | Partial |
| Concurrency | Queries are separate store reads; no common snapshot across two HTTP requests or report/export execution | Gap: no guarantee of same-time cross-request reconciliation during concurrent posting |
| Audit and permissions | RequireAuthorization + ReportBranches + tenant-scoped account/center lookup; Read and branch membership checks. Report reads do not post journals; no report-specific extraction/export audit or independent account-access policy demonstrated | Partial BR18 |

Code: [ReportService](../../backend/src/Ameen.Erp.Application/Features/Report/ReportService.cs), [ReportsEndpoints](../../backend/src/Ameen.Erp.Api/Endpoints/ReportsEndpoints.cs), [TrialBalance contracts](../../backend/src/Ameen.Erp.Application/Contracts/TrialBalance), [AccountLedger contracts](../../backend/src/Ameen.Erp.Application/Contracts/AccountLedger), [OpenAPI](../../backend/openapi/v1.json).

## Verification map

- Program.cs: opening/movement/closing, historical reversal, hierarchy and zero rows, center/reference filtering, invalid level and branch scope.
- AccountLedgerAcceptanceTests.cs: approved/draft/submitted and future movements excluded, stable source order, running balance and paging, limited branch; extended here to reconcile **both posting accounts**, all and restricted branches, and aggregation with the trial balance.
- ReportBranchSelectionTests.cs: matching scope, duplicate selections, inactive/history, unauthorized and missing branches, center mismatch.
- BalancedJournalLedgerSqlAcceptance.cs: live SQL posted journal reconciles both ledgers and trial with audit/outbox; tenant denial.
- HostedApiIntegration.cs: HTTPS/JWT/SQL source-line identity, approved-unposted exclusion, tenant denial and report branch filters.
- OpenApiSnapshotTests.cs: generated contract equality. CI evidence is tied to the PR head; no unexecuted live test is claimed locally.

Completion criterion for these two **audit tasks** is traceability plus actual verification and documented gaps. No change to original analysis, field statuses, production policy, report endpoints, SQL migration or screen completion is made. Outstanding RPT01/02 feature and acceptance issues remain open; missing source type/center, currency modes, account range, export/print, authorization/audit and concurrency need their respective implementation and acceptance.
