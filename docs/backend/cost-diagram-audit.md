# DIAG-module-COST — مطابقة خريطة الموازنات ومراكز التكلفة

تدقيق مستقل للقضية [#550](https://github.com/ameenalqershi/wow-erp/issues/550)، على main عند `78dc75a1a61e1acc5484394fd659fc36ba9ddc56`، 27 نوفمبر 2026. مصدر الحقيقة [Mermaid](../../muhaseb-erp-analysis/diagrams/source/module-COST.mmd)، [SVG](../../muhaseb-erp-analysis/diagrams/svg/module-COST.svg)، [مواصفة الشاشات](../../muhaseb-erp-analysis/docs/modules/COST.md)، [قاموس الحقول](../../muhaseb-erp-analysis/data/fields.csv). لا تعدّ هذه المراجعة تنفيذًا لشاشة.

## عقد وحواف التنقل

10 عقد: جذر، أربع فئات، خمس شاشات. تسع حواف موجهة تطابق مسارات SVG الهندسية وعناوين العقد، بما في ذلك العناوين الموزعة على سطرين.

| من | إلى | دلالة |
|---|---|---|
| الجذر | البيانات الأساسية | قائمة |
| الجذر | العمليات | قائمة |
| الجذر | الإجراءات | قائمة |
| الجذر | التقارير | قائمة |
| البيانات الأساسية | COST01 | عنصر قائمة |
| العمليات | COST02 | عنصر قائمة |
| الإجراءات | COST04 | عنصر قائمة |
| التقارير | COST05 | عنصر قائمة |
| COST02 | COST03 | ترتيب تنقل |

لا توجد حالات مستند أو شروط محاسبية على الحواف. ترتيب القائمة لا يفرض ترتيبًا للترحيل أو الاعتماد أو الإقفال.

## مطابقة الحقول والضوابط

85/85 زوجًا من معرف الحقل والاسم يطابق [مصفوفة الحقول](fields-implementation.csv) بالترتيب: 11 implemented، 3 audit، 71 pending. [مصفوفة الشاشات](screens-implementation.csv): COST01 partial، COST02–05 not-started. [مصفوفة المخططات](diagrams-implementation.csv): module-COST partial. كل نطاق F يبدأ بمعرف الشاشة ويشمل الطرفين.

| الشاشة | الحقول | ضابط المصدر والمخرج | ارتباطات المصدر | قضايا التنفيذ والقبول |
|---|---|---|---|---|
| COST01 دليل مراكز التكلفة | F01–F15؛ 11 منفذًا و3 تدقيق و1 معلق | شجرة بلا دورات ولا حذف لمركز له حركة؛ بعد تحليلي | GL03, COST02 | [البنية #417](https://github.com/ameenalqershi/wow-erp/issues/417)، [قبول موجب #420](https://github.com/ameenalqershi/wow-erp/issues/420)، [سالب #421](https://github.com/ameenalqershi/wow-erp/issues/421)، [ضابط #422](https://github.com/ameenalqershi/wow-erp/issues/422) |
| COST02 الموازنة التقديرية | F01–F23؛ 23 معلقة | الإصدار المعتمد لا يعدل؛ إصدار جديد مع أسباب؛ موازنة دون قيد أستاذ | COST03 | [البنية #435](https://github.com/ameenalqershi/wow-erp/issues/435)، [موجب #438](https://github.com/ameenalqershi/wow-erp/issues/438)، [سالب #439](https://github.com/ameenalqershi/wow-erp/issues/439)، [ضابط #440](https://github.com/ameenalqershi/wow-erp/issues/440) |
| COST03 المناقلات وتعديل الموازنة | F01–F22؛ 22 معلقة | المناقلة لا تزيد الإجمالي؛ الزيادة تحتاج نوع اعتماد إضافي منفصل؛ إصدار تعديل | COST02 | [البنية #456](https://github.com/ameenalqershi/wow-erp/issues/456)، [موجب #459](https://github.com/ameenalqershi/wow-erp/issues/459)، [سالب #460](https://github.com/ameenalqershi/wow-erp/issues/460)، [ضابط #461](https://github.com/ameenalqershi/wow-erp/issues/461) |
| COST04 توزيع المصروفات والتكاليف | F01–F13؛ 13 معلقة | مجموع الأوزان موجب؛ لا تكرار لنفس الرصيد والدورة؛ معاينة وقيد إعادة توزيع | GL03, COST01 | [البنية #477](https://github.com/ameenalqershi/wow-erp/issues/477)، [موجب #480](https://github.com/ameenalqershi/wow-erp/issues/480)، [سالب #481](https://github.com/ameenalqershi/wow-erp/issues/481)، [ضابط #482](https://github.com/ameenalqershi/wow-erp/issues/482) |
| COST05 الموازنة مقابل الفعلي | F01–F12؛ 12 معلقة | الانحراف = الفعلي ناقص المخطط مع تفسير إشارة المصروف والإيراد؛ تقرير انحراف ونسبة تنفيذ | COST02, PUR06 | [البنية #494](https://github.com/ameenalqershi/wow-erp/issues/494)، [موجب #497](https://github.com/ameenalqershi/wow-erp/issues/497)، [سالب #498](https://github.com/ameenalqershi/wow-erp/issues/498)، [ضابط #499](https://github.com/ameenalqershi/wow-erp/issues/499) |

### التنفيذ الفعلي لـCOST01

- F01–F10 وF14 مرتبطة ببطاقة CostCenter وVersion تقني منفصل عن Revision. F12/F13/F15 مستمدة من AuditEvent المنشئ والتاريخ وآخر تعديل، لا حقول إدخال مباشرة. F11 نوع المركز معلق لحين تحديد قائمة قيم معتمدة. لا تدعي المصفوفة أن القيمة النصية في المصدر معرف أعمال منفصل عن Guid التقني.
- [الخدمة](../../backend/src/Ameen.Erp.Application/Features/CostCenter/CostCenterService.cs) تتحقق من شجرة الآباء وفعاليتهم والنطاق والعضوية المسؤولة، وتمنع تغيير الرمز/الفرع/الأب للمركز أو الشجرة المستخدمة. التعطيل منفصل عن الحذف ويحفظ التاريخ؛ لا يتيح [API](../../backend/src/Ameen.Erp.Api/Endpoints/CostCentersEndpoints.cs) DELETE. تقارير الأستاذ تختار مركزًا محددًا ولا تجمع الأحفاد تلقائيًا.
- [SQL 005](../../backend/database/005_cost_center_hierarchy.sql) يضيف مفاتيح مركبة داخل الشركة وCHECK للربط المباشر بالنفس، بينما [SQL 001](../../backend/database/001_initial.sql) يحفظ مرجع سطر القيد إلى المركز. [دور التشغيل](../../backend/database/runtime-role.sql) لا يمنح حذف CostCenters. [المعاملات](../../backend/src/Ameen.Erp.Infrastructure/Persistence/SqlTransactionRunner.cs) تسلسل أوامر الشركة بقفل معاملة Serializable، وتلتزم بالتدقيق والإيصال أو ترجعها جميعًا.
- [اختبارات المجال](../../backend/tests/Ameen.Erp.Tests/CostCenterTests.cs) تثبت الدورات والنطاق وقيود الاستخدام والتعطيل والتاريخ وإعادة الطلب وتعارض الإصدار. [SQL](../../backend/tests/Ameen.Erp.Tests/FoundationSqlIntegration.cs) يثبت المراجع المركبة وتزامن إعادة الطلب ورفض تعديل البنية بعد الاستخدام وفشل التدقيق الذري؛ [HTTPS](../../backend/tests/Ameen.Erp.Tests/HostedApiIntegration.cs) يغطي الإنشاء والتعديل والبحث والاستخدام والحالة. قبول إضافي للدورة المتزامنة والحذف في PR #2913 مستقل ولم يكن مدمجًا عند هذا التدقيق.
- لا تغير عمليات بطاقة COST01 رصيد الأستاذ بذاتها؛ تستخدم GL03 مرجع المركز في السطر. [عقد الحقول والتشغيل](currencies-and-cost-centers.md) يسجل القيود.

### قواعد مشتركة وفجوات

BR02 وBR18 مشتركتان للخمسة (عزل المراجع والشركة والصلاحيات)، BR09 مشتركة للأربع الأولى (التدقيق وفصل المهام). COST02/03 تضيف BR01/03/05/06/07/10: الاتزان حيث يوجد أثر مالي، الفترة، الذرية، منع التكرار، عدم تغيير الأثر المعتمد، سعر الصرف. لا يعني إدراج قواعد القيد أن إنشاء الموازنة يولد قيدًا، فالمصدر ينفي ذلك صراحة. [القواعد](../../muhaseb-erp-analysis/docs/02-accounting-and-business-rules.md).

COST02–05 لا تملك مسار API أو اختبارًا خاصًا في مصفوفة الشاشات؛ لا يجوز إسقاط اختبارات COST01 أو التقارير الحالية عليها. يلزم إثبات حفظ إصدار معتمد غير قابل للتعديل، الموافقات الإضافية، عزل وتزامن المناقلات، منع توزيع مكرر وحفظ قيده وتدقيقه ذرّيًا، وصحة إشارة الانحراف ومرجعه الزمني. هذه شروط تحقق مستنبطة من المصدر وليست نتائج منفذة. لا توجد سياسة توزيع أو نوع مركز جديدة معتمدة في هذا التدقيق.

## التحقق

حارس التنقل نجح لـ15 خريطة و150 شاشة. تمت مقارنة CSV بالمصدر: 85 معرّفًا واسمًا مطابقًا، وحالاتها كما أعلاه. تم فحص SVG: تسعة أسهم تطابق مواضع بداية ونهاية العقد وعناوينها. لا تعديل للملفات الأصلية، أو API أو SQL أو حالة المصفوفات. اكتمال هذه القضية هو اكتمال التدقيق لا اكتمال الوحدة.
