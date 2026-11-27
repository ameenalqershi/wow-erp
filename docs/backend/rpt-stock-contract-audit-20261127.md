# RC09 / RC10 — تدقيق عقود المخزون والصلاحية والراكد (#2018، #2019)

مرجع التدقيق: main عند 2b18c73. [SPEC08](../../muhaseb-erp-analysis/docs/08-reports-and-roadmap.md)، [RPT](../../muhaseb-erp-analysis/docs/modules/RPT.md)، [INV](../../muhaseb-erp-analysis/docs/modules/INV.md)، [GL](../../muhaseb-erp-analysis/docs/modules/GL.md)، [PUR](../../muhaseb-erp-analysis/docs/modules/PUR.md). RC09 وRC10 هما معرفا صفّي SPEC08؛ RPT-CONTRACT-08/09 معرفا المهمتين. لا تعديل للمصدر.

## المخطط والحقول

[خريطة RPT](../../muhaseb-erp-analysis/diagrams/source/module-RPT.mmd): RPT07 → RPT08 → RPT09 → RPT10 → RPT11؛ العقد تحت التقارير. هذه حواف التنقل وليست إذنًا بتجاوز اعتماديات البيانات. [خريطة INV](../../muhaseb-erp-analysis/diagrams/source/module-INV.mmd): INV04 → INV05، INV06 → INV07 → INV08، INV11 → INV12 → INV15. الاعتماديات الصريحة من screens.csv: RPT08 ← INV15 وGL10؛ RPT09 ← INV07 وINV08؛ RPT10 ← INV05 وINV11 وPUR04.

[لقطة مطابقة جميع 36 حقلًا](rpt-stock-contract-fields-20261127.csv) تربط معرف الحقل والاسم والإلزام من القاموس الأصلي بحالة وربط ومسار مصفوفة التنفيذ عند التدقيق. ليست مصدر حالة بديلًا عن fields-implementation.csv.

| الشاشة | الحقول | منفذ | جزئي | معلق |
|---|---|---:|---:|---:|
| RPT08 | F01–F13 | 7 | 3 | 3 |
| RPT09 | F01–F11 | 7 | 2 | 2 |
| RPT10 | F01–F12 | 0 | 0 | 12 |
| المجموع | 36 | 14 | 5 | 17 |

المشترك RPT08/09 F01–F04: الشركة والفروع والفترة، ومنها اختيار فروع متعددة مصرح بها. F05 عملة وظيفية فقط، F06 لا نمط تحويل، F07 مصادر مرحلة محدودة لا مرشح حالات عام، F08 لا مستوى تفصيل. RPT08 F09 حد to نفسه لا تاريخ قطع مستقل؛ F10–F12 مخزن ومجموعة وصنف؛ F13 لا خيار طريق وعزل. RPT09 F09–F11 صنف ومخزن وهوية نصية تحفظ الأصفار. اختيار البطاقات الفعالة لا يلغي قراءة حركة تاريخية لبطاقة معطلة؛ أسماء الأصناف والمجموعات بيانات حالية وليست لقطات تاريخية. RPT10 حتى الحقول المشتركة غير منفذة في عقد شاشة مستقل، ولا يجوز نقل حالة الحقول من RPT08/09 إليه.

## RC09 — حركة وتقييم المخزون

| العمود/الضابط | الربط الفعلي | الحد |
|---|---|---|
| صنف، مخزن، وحدة أساسية | InventoryBalanceRow.ItemId/WarehouseId/BaseUnitId؛ InventoryLedgerResult وRows | مصادر افتتاح INV06 وعكسها فقط |
| أول، داخل، خارج، ختام | Opening/In/Out/ClosingQuantity/Value؛ الرصيد الجاري يتضمن الصفحات السابقة | تجميع حسب فرع ومخزن وصنف وهوية وحساب رقابة تاريخي؛ لا جمع كميات أصناف بوحدات مختلفة |
| تكلفة وقيمة | StockMovement.Value وInventoryDocumentUnit.UnitCost/FactorToBase/Quantity/Value | قيمة محفوظة وتكلفة الوثيقة؛ لا تكلفة موحدة محسوبة في صف الرصيد أو محرك إعادة تقييم INV15 |
| المصدر | OpeningId وJournalId وReversesMovementId | إعادة تفويض المصدر لكل طلب؛ لا مصادر تجارية غير موجودة |
| مطابقة الرقابة | InventoryOpeningReconciliation.Matches وPeriodCloseService.InventoryReconciles؛ مقارنة AccountLedger في الاختبارات | مطابقة مستند افتتاح وقيده وحركته، لا محضر GL10 العام أو مطابقة آلية لكل تقرير |
| فصل الأرصدة | لا حقول موجود/محجوز/متاح/طريق/عزل مستقلة | لا يجوز تسمية الرصيد المتاح للبيع أو إخفاء فرق تحت الصفر |

[خدمة التقرير](../../backend/src/Ameen.Erp.Application/Features/Report/ReportService.Inventory.cs) تجمع من الحركات مرة واحدة للنطاق؛ الإجماليات قبل الصفحات. showZero=false يستبعد صفوف الختام الصفري حتى لو كانت لها حركة، والإجمالي يخص الصفوف الباقية. ترتيب الحركة بتاريخ الأثر ثم وقت الترحيل ثم الأصل قبل العكس عند التعادل ثم معرف الحركة. التزامن: الحركة والقيود مرتبطة ذريًا عند الترحيل؛ حراس EF تمنع تغيير المصدر المرحل. طلب تقرير مستقل عن طلب الأستاذ، فلا ادعاء لقطة مشتركة متزامنة. تجميع القراءة في الذاكرة يحتاج اختبار حجم وتحسينًا.

## RC10 — الصلاحية والراكد

| العمود/الضابط | مصدر البيانات المتاح | النقص |
|---|---|---|
| دفعة، تاريخ انتهاء | INV05-F08–F13: ItemId/LotNumber/SerialNumber/ProductionDate/ExpiryDate/QualityStatus | سجل البطاقة ليس تقريرًا تاريخيًا للرصيد؛ سياسة الجودة والصلاحية جزئية |
| أيام حتى الانتهاء | ExpiryDate nullable | لا حساب أفق أو مرشح؛ لا تفسير للتاريخ الغائب بصفر |
| آخر حركة | StockMovement.AccountingDate وOccurredAt | افتتاح وعكس فقط، لا آخر حركة عامة عبر مصادر INV07/08/09/10 |
| كمية وقيمة | المصدر نفسه في RPT08/09 | لا تجميع حسب الصلاحية/الراكد أو توصية شراء |
| استبعاد الخدمة | InventoryOpeningService يرفض ItemType.Service عند إنشاء افتتاح | لا استعلام RPT10 لاختبار الاستبعاد فيه |
| العزل والطريق والحجز والأوامر | INV05 جودة، INV11 حجز، PUR04 طلب داخلي | لا دمج بيانات الحجز والطريق والأوامر المفتوحة؛ لا سياسة احتياج مستنتجة |

لا مسار RPT10 في ReportsEndpoints أو OpenAPI على النسخة المراجعة. كل RPT10-F01–F12 pending. لا يجوز استعمال تقرير افتتاحات محدود على أنه تقرير راكد أو صلاحية. INV07/08/11/15 وGL10 وPUR04 غير مكتملة تشغيليًا؛ اعتماد INV05 جزئي. AT04/06/11/12/13 أمثلة قبول مرتبطة بالمصادر ولا يتغير وضعها من التدقيق.

## ضوابط مشتركة وأدلة

BR02: التحقق من الشركة والفرع والمخزن والصنف. BR18: تفويض Read لكل نطاق ومصدر؛ لا إثبات صلاحية تصدير تقرير غير منفذ. عملة وظيفية فقط، لا تحويل عرض ولا ختم وقت استخراج موحد ولا تصدير مطابق للمرشحات. لا كتابة أو أثر محاسبي من القراءة. إثباتات موجودة: [InventoryReportTests](../../backend/tests/Ameen.Erp.Tests/InventoryReportTests.cs)، [InventoryOpeningSqlIntegration](../../backend/tests/Ameen.Erp.Tests/InventoryOpeningSqlIntegration.cs)، [HostedApiIntegration](../../backend/tests/Ameen.Erp.Tests/HostedApiIntegration.cs)، [حراس EF](../../backend/src/Ameen.Erp.Infrastructure/Persistence/ErpDbContext.Validation.cs)، [عقد التقرير](inventory-reports.md). PR #2918 و#2921 اجتازا CI؛ هذا تدقيق للمطابقة ولا يضيف تشغيلًا جديدًا.

القرار: يمكن إغلاق #2018 و#2019 كتدقيق تغطية فقط بعد قبول الوثائق. RC09 وRC10 يبقيان not-verified في مصفوفة عقود التقارير، والشاشات partial/partial/not-started؛ لا زيادة في نسبة تنفيذ الحقول، ولا تغيير في سياسة التكلفة/العملة/الحجز أو التحليل والمخططات.
