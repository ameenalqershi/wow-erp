# RPT01/RPT02 — dependency-aware acceptance review (2026-11-27)

Evidence base: main after #2941 (merge 781320b), original RPT01/RPT02 [screen contracts](../../muhaseb-erp-analysis/docs/modules/RPT.md), [diagram](../../muhaseb-erp-analysis/diagrams/source/module-RPT.mmd), [fields](../../muhaseb-erp-analysis/data/fields.csv), [controls](../../muhaseb-erp-analysis/docs/05-data-and-controls.md), BR02/BR18. Passing tests for an available contract are not proof of the final contract.

| Issue | Disposition | Required follow-up |
|---|---|---|
| #2011, #2012 | Audits remain closed, not implementation acceptance | Original contract audits explicitly identify incomplete fields; update evidence on final acceptance. |
| #2033, #2034, #2035 | Exact functional-currency equation/negative tests remain closed | Retain as regression gates; final expanded-mode integration is #2032. |
| #2042, #2043, #2044 | Reopened; current merged code preserved | #2037 (F05–F08), #2032 (historical/concurrent acceptance), #2616 (complete role/card policy). |
| #2056, #2058 | Reopened; current single-account code preserved | #2052 (F05–F08), #2053 (F09–F12), #2047 (integration), #2616 (policy). |
| #2057 | Reopened, source line identity preserved | #2616, #2047; enforce visibility of source details under final account policy. |
| #2934 | Reopened, independent print permission/audit preserved | #2616; verify role assignment/deny/card scope rather than only membership grants. |

#2032 additionally depends on #2037, #687 (historical account conversion) and #2616; #2047 depends on #2052, #2053 and #2616; #2053 depends on #2616 for complete account-card authorization. GL10 #865–#868 is the linked control-account reconciliation workflow; it is not called by the existing query, so it remains a cross-module final-acceptance check rather than a fabricated direct runtime dependency. The currency/state/detail allowed-value lists are not defined by the original field dictionary. No alternative conversion or document-state policy is assumed.

For final closure, verify all approved filters on display, drill-down, print and export against the same calculation, explicit scope, stable historical account hierarchy, no unauthorized disclosure, extraction audit and concurrent behavior. Verify SQL/HTTPS/JWT with a current-main CI result. UI and fiscal/bilingual templates are later deliverables; do not label current HTML as certified final printing. Performance at production volumes still requires measurement (the queries materialize matching journal lines). These are action-level gates, not a claim that the entire ERP is complete. No original file was modified.
