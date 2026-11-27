# RPT02-OP03 — account ledger source drill-through (#2057)

## Contract
The original [RPT02 screen](../../muhaseb-erp-analysis/docs/modules/RPT.md), [report contract](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md), [navigation](../../muhaseb-erp-analysis/diagrams/source/module-RPT.mmd) and BR02/BR18 require a source link subject to authorization. RPT02-F01–F04 establish tenant, branch and dates; F09–F12 establish account/center/reference scope. GL03 is the journal source.

1. GET /api/v1/companies/{companyId}/reports/accounts/{accountId}/ledger with the report filters.
2. From a returned entry, take journalId and position (not number or page index).
3. GET /api/v1/companies/{companyId}/journals/{journalId} with the same authenticated actor. Select the unique lines element with the returned position; its accountId must match the ledger account. Amounts, currency, date, number, description and rate reconcile exactly.

Existing ReportService excludes drafts and out-of-period postings and sorts by accounting date, printed number, journal id, and position before pagination. JournalService.GetJournal reauthorizes membership and branch on every source read, not by possession of an id. It intentionally allows other authorized GL03 workflows to read draft journals; the report itself never returns drafts. Posted/reversed sources remain historically addressable. No new route, schema, print/export, or permission policy is necessary for this specific backend action.

## Acceptance evidence
- AccountLedgerAcceptanceTests: all three ledger entries across two branches, including two same-account lines in one journal; verifies original and functional values; limited actor is denied the other branch source, outsider denied, unknown id not found; no audit/outbox/idempotency mutations from reads; historical report retains original line identities after a subsequent reversal.
- HostedApiIntegration: existing real SQL-backed HTTPS/JWT round-trip compares the source line; additional read-only branch member succeeds, nonmember and other-branch JWTs get 403, missing id gets 404.
- OpenAPI snapshot retains both existing routes. CI must pass on the exact PR head before closure. RPT02 as a whole stays partial (currency modes, range, print/export, concurrency and other acceptance tasks remain distinct).
