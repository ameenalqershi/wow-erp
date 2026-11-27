# COST01-FG04 / OP06 — حقول التدقيق وسجل التغييرات

القضيتان [#426](https://github.com/ameenalqershi/wow-erp/issues/426) و[#434](https://github.com/ameenalqershi/wow-erp/issues/434). المصدر: [COST01](../../muhaseb-erp-analysis/docs/modules/COST.md)، [قاموس الحقول](../../muhaseb-erp-analysis/data/fields.csv)، [مصفوفة التنفيذ](fields-implementation.csv). النطاق مستقل عن نوع المركز F11 والموازنات والتوزيعات.

| الحقل أو الإجراء | الربط والتغطية |
|---|---|
| F13 وقت الإنشاء | AuditEvent.OccurredAt، حدث cost-center.created، وقت الخادم مع المنطقة الزمنية |
| F14 رقم إصدار التعديل | CostCenter.Revision عدد صحيح؛ 1 للإنشاء ثم يزيد لكل تعديل وحالة ناجحة؛ Version مستقل للتزامن |
| F15 آخر تعديل | AuditEvent.OccurredAt لأحداث cost-center.edited وcost-center.status-changed |
| OP06 سجل التغييرات | GET /companies/{companyId}/cost-centers/{id}/audit-events؛ يتطلب audit.read لجميع الفروع وledger.read للمركز؛ ترتيب الوقت ثم المعرف، وصفحات مستقرة في غياب تغييرات متزامنة |

## أدلة القبول

- CostCenterTests: إنشاء وتعديل وإعادة الطلب بمفتاحه ثم رفض إصدار قديم وتعطيل؛ ثلاثة أحداث فقط، ActorId/CompanyId/ResourceId ووقت FixedClock، Revision=3، صفحات دون تكرار، رفض القارئ غير المخول وعضوية فرع آخر ومرجع شركة أخرى.
- FoundationSqlIntegration: أربع عمليات ناجحة على البطاقة نفسها (إنشاء، تعديل متزامن بمفتاح واحد، إعادة تعديل بعد فشل حفظ التدقيق، تعطيل بدور التشغيل)؛ أربعة أحداث فقط وRevision=4، التوقيت من الخادم، تطابق ترتيب الصفحات ورفض مرجع شركة أخرى.
- HostedApiIntegration: تحقق عبر HTTPS/JWT من الإصدار العددي، أحداث الإنشاء والتعديل والتعطيل ووقت الخادم، معرف الشركة والمركز والفاعل، الصفحات، ورفض JWT محدود الفرع أو بلا عضوية.
- الحقول تظل audit/implemented/audit حسب المصفوفة؛ لا تغيير للمخطط الأصلي أو API أو SQL أو حالة الشاشة الجزئية. إثبات القبول النهائي يتطلب CI على رأس الطلب.
