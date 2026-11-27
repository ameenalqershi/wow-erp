# RPT01-OP05 — Excel export (#2044)

Sources: original [RPT01](../../muhaseb-erp-analysis/docs/modules/RPT.md), [diagram](../../muhaseb-erp-analysis/diagrams/source/module-RPT.mmd), [fields](../../muhaseb-erp-analysis/data/fields.csv), [report contract](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md), [data controls](../../muhaseb-erp-analysis/docs/05-data-and-controls.md), BR02/BR18.

GET /api/v1/companies/{companyId}/reports/trial-balance/export returns an XLSX attachment. Uses the original trial balance engine and identical company, branch, date, cost center, hierarchy and zero-row filters. All six opening/movement/closing debit/credit columns, account code/name and totals are sourced directly from the query; header declares F01–F11 and extraction time. Existing functional currency and historical posted/reversed modes only; other mode choices remain independent.

Requires both ledger.read and report.rpt01.export, scoped to authorized branches. No automatic grant to existing members. After the workbook is built, an audit event records actor, resource, filters, extraction time and row count. Failed checks do not create audit; no journal, receipt or outbox changes. Strings are inline text, not formulas; decimal amounts with more than 15 significant digits are stored as exact text rather than silently rounded by Excel. No external relationships or macros. Separate requests may see newer postings; no snapshot guarantee. No change to original analysis or schema.

Validation: TrialBalanceAcceptanceTests, TrialBalanceExportTests, HostedApiIntegration, OpenAPI; compare every workbook row and total against scoped JSON and verify permission rejection and audit. SQL/HTTPS/JWT and CI required before closing.
