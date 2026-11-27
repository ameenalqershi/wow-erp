# RPT02-OP02 — account ledger display (#2056)

Original contract: [RPT02](../../muhaseb-erp-analysis/docs/modules/RPT.md), [report rules](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md), [fields](../../muhaseb-erp-analysis/data/fields.csv), BR02/BR18. The backend display action uses GET /api/v1/companies/{companyId}/reports/accounts/{accountId}/ledger, returns historical date, document number, description, functional debit/credit, running balance and original amounts/rate. The statement scope (company, account, branches, dates, optional center/reference) and total count, opening, period movement, closing must be invariant across pages.

Acceptance:
- Existing three-line memory scenario: sorted by accounting date, number, journal identity and position, including repeated account lines; only posted/reversed movements; source and trial balance reconciliation; limited branch isolation. New exhausted-page checks retain report totals and scope, and a later period with no movement carries forward the posted balance (1044) rather than resetting it.
- Production HTTPS/JWT/SQL: scoped read-only member reads first and exhausted pages, preserving all header totals; empty reference returns no movement or balance; invalid offset, limit and date order rejected (422), unknown account rejected (404), nonmember rejected (403). Existing source round-trip proves the displayed date, document, description, debit/credit and running balance.
- Existing OpenAPI route and decimal string serialization are retained. No schema/API change or fabricated balancing entry. Separate RPT02 feature gaps (range, display currencies, print/export, cross-request snapshot) remain open.

CI on the final head is the closure gate. The action is backend display, not a claim of completed UI or full RPT02 screen.
