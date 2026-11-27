# RPT01 acceptance — #2033, #2034, #2035

Original requirements: [screen](../../muhaseb-erp-analysis/docs/modules/RPT.md), [report contract](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md), [field dictionary](../../muhaseb-erp-analysis/data/fields.csv), [rules BR01/BR02/BR18](../../muhaseb-erp-analysis/docs/02-accounting-and-business-rules.md). The report returns account code/name and opening, movement, closing debit/credit in functional currency. RPT01-F01–F04 establish company, authorized branches and period; F05–F08 retain their existing partial/pending statuses; F09–F11 select level, center and zero display.

| Case | Expected evidence |
|---|---|
| AC01 #2033 | Opening 40, movement 66.50, closing 106.50 in both sides; exact account names/codes, functional SAR, 10 USD × 3.75 = 37.50 SAR; limited branch movement 57.50, second branch 9 |
| AC02 #2034 | Unbalanced draft 100/99 cannot submit; draft, submitted, approved and future journals excluded; unauthorized branch/member and invalid date range rejected; report unchanged and rejected requests have no command effects |
| CTRL01 #2035 | Every row signed opening + signed movement = signed closing; column totals equal row sums; whole-branch functional debits and credits equal |

A cost-center line filter may legitimately select only one side of a journal; the whole-branch equality assertion deliberately does not apply to such a subset. No artificial balancing entry, new accounting policy, or screen completion is inferred.

Evidence: TrialBalanceAcceptanceTests (memory), BalancedJournalLedgerSqlAcceptance (isolated live SQL and rejected draft), HostedApiIntegration (real HTTPS/JWT scope and exact decimal contract), existing hierarchy/zero/cost-center/history and OpenAPI regression suite. SQL draft fixture is cancelled after the negative test. Merge requires successful CI on the final head.
