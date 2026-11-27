# RC05 — تدقيق عقد تقرير اليومية (#2014)

النطاق: تدقيق التغطية وفق المصدر الأصلي [SPEC08](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md) على main عند 78dc75a، وليس إنشاء تقرير تشغيلي. RC05 في مصفوفة SPEC08 يبقى not-verified على مستوى التقرير الكامل.

## مطابقة الشاشات والحقول

المصدر [GL](../../muhaseb-erp-analysis/docs/modules/GL.md)، [مخطط التنقل](../../muhaseb-erp-analysis/diagrams/source/module-GL.mmd)، [قاموس الحقول](../../muhaseb-erp-analysis/data/fields.csv)، [مصفوفة التنفيذ](fields-implementation.csv). المسار GL05 → GL06، GL03 → GL07، GL06 → GL08. GL03/05/06/07 هي مصادر المستند والمراجعة والترحيل والعكس، وليست شاشة تقرير جديدة.

| مطلب RC05 | حقول أصلية | التنفيذ الفعلي والحدود |
|---|---|---|
| القيد ومصدره | GL03-F01–F07,F10–F13 | JournalEntry.Id/Number/CompanyId/BranchId/AccountingDate/Source/State؛ JournalSummary يحمل Source وRecurringTemplateId؛ نوع القيد القابل للتهيئة لا يزال جزئيًا |
| حالة الاعتماد | GL03-F12؛ GL05-F03–F09 | JournalEntry.State/ApprovedBy؛ سجل المراجعة من AuditEvent.Action/ActorId/Details؛ حالة مراجعة مستقلة GL05-F05 معلقة |
| الحسابات والمبالغ | GL03-F16–F24 | JournalLine.AccountId/Debit/Credit/CurrencyCode/ExchangeRate/FunctionalDebit/FunctionalCredit؛ مركز التكلفة موجود، المشروع والطرف معلقان |
| المنشئ والمعتمد | GL03-F25–F28؛ GL05-F06,F10–F13 | CreatedBy/CreatedAt/Revision/LastModifiedAt/ApprovedBy في المستند؛ JournalSummary لا يعرض المنشئ والمعتمد، وJournalPrintDocument يعرض المنشئ دون المعتمد |
| عملة ومطابقة | GL03-F08–F09,F17–F20 | عملة وسعر تاريخيان لكل سطر؛ عملة وسعر رأس موحدان جزئيان، لا يجوز جمع مبالغ أصلية متعددة العملات |

## مصدر الأرقام والضوابط

- JournalService.ListJournals يقرأ JournalEntry ويجمع FunctionalDebit/Credit من Lines مرة واحدة لكل قيد؛ الفرع والحالة والمصدر والبحث والصفحات مدعومة. لا مرشح فترة من/إلى أو عملة عرض/نمط تحويل أو وقت استخراج موحد، ولا إجماليات على كامل نتائج التقرير. لا يجوز اعتبار الصفحة إجمالي الفترة.
- JournalService.GetJournal يعزل الشركة والفرع والصلاحية Read؛ ProcessingHistory يتطلب Audit على جميع الفروع وRead للقيد. لا يخول عرض القيد عرض سجل التدقيق تلقائيًا.
- JournalEntry.ValidateBalance يشترط سطرين على الأقل وإجمالي مدين وظيفي موجب ومساواته بالدائن عند التقديم والاعتماد والترحيل. المسودة قد تكون غير متزنة؛ لا تمثل أثرًا ماليًا. فصل المنشئ والمعتمد حسب السياسة في Approve. Version رمز التزامن منفصل عن Revision. حراس EF تمنع تغيير السطور بعد المسودة والتلاعب بالأثر المرحل؛ العكس مستند مستقل.
- JournalService.JournalPrint يقرأ القيد نفسه، ويعرض سطور الحساب وأسعار الصرف التاريخية؛ يجمع الأصل فقط عند عملة واحدة، ويجمع الوظيفي دائمًا. HTML لقيد يدوي/عكس فقط، لا تصدير مجموعة اليوميات ولا طباعة جميع المصادر. أسماء الحساب والشركة والفرع الحالية ليست لقطة أسماء تاريخية.
- قواعد BR01/02/03/05/06/07/09/10/18 مرتبطة بالقيود الأصلية؛ وجود حماية بالقراءة والترحيل لا يثبت أن صلاحية تصدير التقرير أو مرشحاته الكاملة منفذة. AT01–03 وAT25 لها أدلة جزئية موجودة، ولا تتغير حالة القبول الرسمية.

## الأدلة

- [JournalService](../../backend/src/Ameen.Erp.Application/Features/Journal/JournalService.cs)، [JournalPrint](../../backend/src/Ameen.Erp.Application/Features/Journal/JournalService.JournalPrint.cs)، [JournalEntry](../../backend/src/Ameen.Erp.Domain/Accounting/JournalEntry.cs)، [حراس EF](../../backend/src/Ameen.Erp.Infrastructure/Persistence/ErpDbContext.Validation.cs).
- [اختبارات GL03](../../backend/tests/Ameen.Erp.Tests/Program.cs): رفض قيد غير متزن، وصول قيد مرحل للأستاذ دون أثر مرفوض، منع الاعتماد الذاتي، منع تعديل السطور المرحلة.
- [JournalPrintTests](../../backend/tests/Ameen.Erp.Tests/JournalPrintTests.cs): عملات متعددة، عكس تاريخي، عدم إنشاء أثر من القراءة، منع فرع غير مصرح ومصدر غير مدعوم.
- [HostedApiIntegration](../../backend/tests/Ameen.Erp.Tests/HostedApiIntegration.cs): GL03-T01 قيد متزن في الأستاذ عبر SQL/HTTPS/JWT ومنع وصول مستأجر غير مصرح. هذه إحالات لا نتائج تشغيل جديدة.
- [OpenAPI](../../backend/openapi/v1.json): GET journals، GET journals/{id}، GET journals/{id}/print وprocessing-history؛ لا مسار مستقل /reports/journal.

## قرار التدقيق

تغطية المصدر المحاسبي والقراءة والطباعة الفردية جزئية ومثبتة بالكود والاختبارات الموجودة. عقد اليومية الموحد ما زال غير متحقق: فترة/فروع متعددة، عملة ونمط تحويل، وقت استخراج، أعمدة المنشئ والمعتمد في قائمة موحدة، إجماليات مستقلة عن الصفحات، وتصدير مطابق للصلاحيات والمرشحات. لا افتراض لسياسة عملة/تصدير أو تصحيح للمصدر الأصلي. يمكن إغلاق مهمة التدقيق #2014 بعد قبول الوثيقة، لا مهمة تنفيذ التقرير ولا AT أو شاشة تشغيلية.
