<div dir="rtl">

# مخطط مخزن EF Core (EF Core store schema)

مخزن مجموعات موارد EF Core مخطط منفصل عن جدول ADO.NET
<code dir="ltr">LocalizationMessages</code>. تصف هذه الصفحة النموذج الذي تستقبله قاعدة بيانات
**جديدة**، وكيفية توفيره، وحدود الملكية التي يجب أن يخطط المستهلك حولها.

المكتبة غير منشورة، لذا لا يوجد دليل ترحيل أو ترقية للمستهلك بعد. وتغطي هذه الصفحة توفير قاعدة
بيانات جديدة فقط. وعندما يتغير أول نموذج منشور، يصبح دليل الترحيل/الترقية متطلب إصدار.

مصدر الحقيقة للنموذج هو
[<code dir="ltr">LocalizationDataContext</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/LocalizationDataContext.cs)
وأنواع الكيانات في المشروع نفسه.

## ما الذي تفعله EnsureCreated

تنشئ <code dir="ltr">Database.EnsureCreatedAsync()</code> النموذج الحالي **فقط عندما لا تحوي
قاعدة البيانات الهدف أي جداول إطلاقاً**. وإذا وُجد أي جدول بالفعل، بما فيه جدول ADO.NET
<code dir="ltr">LocalizationMessages</code>، فإنها لا تفعل شيئاً وتعيد
<code dir="ltr">false</code>؛ ولا تنشئ جداول المخزن المفقودة ولا تشغّل ترحيلات.

لذلك السلوك ثلاث نتائج عملية:

1. وفّر المخزن في **قاعدة بيانات فارغة مخصصة**، أو قبل وجود أي جدول آخر.
2. وإذا كان المخزن يتشارك قاعدة بيانات مع مخططات أخرى، فأنشئ جداوله بـ DDL مُراجَع تملكه أو
   بترحيل تملكه. لا تعتمد على <code dir="ltr">EnsureCreated</code> لإضافتها لاحقاً.
3. <code dir="ltr">EnsureCreated</code> ليست أداة ترحيل. ولا توجد ترحيلات EF Core في الحزمة.

## توفير قاعدة بيانات جديدة

سجّل المخزن، وحل مصنع السياق، وشغّل <code dir="ltr">EnsureCreated</code> كخطوة نشر:

<div dir="ltr">

```csharp
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEntityFrameworkLocalizationStore(connectionString);

await using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>();

await using var database = await factory.CreateDbContextAsync();
var created = await database.Database.EnsureCreatedAsync();

// created == false means the database already had at least one table.
// EnsureCreated did not add the store tables; verify them explicitly.
```

</div>

الجداول المتوقعة هي:

<div dir="ltr">

```text
LocalizedStrings
AuditRecords
StringVersions
CatalogState
StringNotes
```

</div>

تحقق منها قبل بدء التطبيق:

<div dir="ltr">

```sql
SELECT name
FROM sys.tables
WHERE name IN (
    N'LocalizedStrings',
    N'AuditRecords',
    N'StringVersions',
    N'CatalogState',
    N'StringNotes')
ORDER BY name;
```

</div>

يبدأ المخزن فارغاً. ولا يحوي <code dir="ltr">CatalogState</code> صفاً حتى ينشئ أول تعديل محكوم
الصف <code dir="ltr">Id = 1</code>؛ وتُبلّغ القراءات قبل ذلك عن الإصدار <code dir="ltr">0</code>.

## ملخص الجداول

يجسّد مزوّد SQL Server النموذج بـ <code dir="ltr">nvarchar(max)</code> للسلاسل العادية. وتُجسَّد
خصائص السلاسل المفهرسة كـ <code dir="ltr">nvarchar(450)</code> (900 بايت)، وهو حد المزوّد الحالي
لمفاتيح الفهرس. ولا تحوي الجداول مفاتيح خارجية؛ فـ <code dir="ltr">EntryId</code> و
<code dir="ltr">ResourceSet</code> و<code dir="ltr">Culture</code> و
<code dir="ltr">Name</code> قيم ربط، لا علاقات تفرضها قاعدة البيانات.

| الجدول | المفتاح الأساسي | الفهارس | الغرض |
|---|---|---|---|
| <code dir="ltr">LocalizedStrings</code> | <code dir="ltr">Id</code> (<code dir="ltr">uniqueidentifier</code>) | المفتاح الأساسي فقط | صفوف الكتالوج الحي |
| <code dir="ltr">AuditRecords</code> | <code dir="ltr">Sequence</code> (<code dir="ltr">bigint</code>، هوية) | <code dir="ltr">Version</code> (غير فريد) | أثر تدقيق منقّح بسلسلة تجزئة |
| <code dir="ltr">StringVersions</code> | <code dir="ltr">Sequence</code> (<code dir="ltr">bigint</code>، هوية) | <code dir="ltr">(ResourceSet, Culture, Version)</code> (غير فريد) | يوميات إعادة التشغيل لقراءات عند إصدار |
| <code dir="ltr">CatalogState</code> | <code dir="ltr">Id</code> (<code dir="ltr">int</code>، بلا هوية؛ دائماً <code dir="ltr">1</code>) | المفتاح الأساسي فقط | عدّاد إصدار الكتالوج بصف واحد |
| <code dir="ltr">StringNotes</code> | <code dir="ltr">Id</code> (<code dir="ltr">uniqueidentifier</code>) | <code dir="ltr">(ResourceSet, Name)</code> فريد | ملاحظات مترجم على مستوى المفتاح |

### LocalizedStrings

| العمود | نوع SQL Server | Null | ملاحظات |
|---|---|---|---|
| <code dir="ltr">Id</code> | <code dir="ltr">uniqueidentifier</code> | لا | مفتاح أساسي؛ وتعيّن الإنشاءات المحكومة <code dir="ltr">Guid.NewGuid()</code> |
| <code dir="ltr">ResourceSet</code> | <code dir="ltr">nvarchar(max)</code> | لا | اسم مجموعة الموارد |
| <code dir="ltr">Culture</code> | <code dir="ltr">nvarchar(max)</code> | لا | رمز الثقافة الدقيق |
| <code dir="ltr">Name</code> | <code dir="ltr">nvarchar(max)</code> | لا | مفتاح الرسالة |
| <code dir="ltr">Value</code> | <code dir="ltr">nvarchar(max)</code> | لا | قيمة القالب |

للجدول فهرس مفتاحه الأساسي فقط. وتُرشّح القراءات على
<code dir="ltr">(ResourceSet, Culture, Name)</code>، لذا سيمسح كتالوج كبير ما لم يضف المستهلك
فهرساً. ولأن تلك الأعمدة <code dir="ltr">nvarchar(max)</code> في النموذج، يتطلب الفهرس شكلاً
قابلاً للفهرسة (أطوالاً محدودة أو مفتاحاً محسوباً) كجزء من إدارة المخطط التي يملكها المستهلك.

ولا يعرّف النموذج أيضاً فهرساً فريداً على
<code dir="ltr">(ResourceSet, Culture, Name)</code>. ويتحقق مسار الإدارة المحكوم من وجود صف داخل
العملية، لكن CRUD (Create, Read, Update, Delete) الخام لمجموعة الموارد والكتّاب المتزامنين غير محميين بقيد قاعدة بيانات. وإذا
كانت الفرادة التي تفرضها قاعدة البيانات مهمة، فعلى المستهلك توفير شكل العمود القابل للفهرسة نفسه
والقيد. راجع [القيود المعروفة](../03-reference/10-known-limitations.md) وملاحظات التزامن في
[README حزمة EF Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md).

### AuditRecords

| العمود | نوع SQL Server | Null | ملاحظات |
|---|---|---|---|
| <code dir="ltr">Sequence</code> | <code dir="ltr">bigint</code> هوية | لا | مفتاح أساسي، ترتيب الإلحاق |
| <code dir="ltr">Version</code> | <code dir="ltr">bigint</code> | لا | إصدار الكتالوج الذي تتشاركه دفعة تعديل |
| <code dir="ltr">Timestamp</code> | <code dir="ltr">datetimeoffset</code> | لا | وقت التثبيت (UTC) |
| <code dir="ltr">Actor</code> | <code dir="ltr">nvarchar(max)</code> | لا | هوية المستدعي، وفارغة عند عدم التعريف |
| <code dir="ltr">Operation</code> | <code dir="ltr">int</code> | لا | تعداد <code dir="ltr">ResourceAdminOperation</code> |
| <code dir="ltr">ResourceSet</code> | <code dir="ltr">nvarchar(max)</code> | لا | مجموعة موارد الصف المعدَّل |
| <code dir="ltr">Culture</code> | <code dir="ltr">nvarchar(max)</code> | لا | ثقافة الصف المعدَّل؛ وفارغة لصفوف الملاحظات |
| <code dir="ltr">Key</code> | <code dir="ltr">nvarchar(max)</code> | لا | مفتاح الصف المعدَّل |
| <code dir="ltr">NewKey</code> | <code dir="ltr">nvarchar(max)</code> | نعم | هدف إعادة التسمية، وإلا <code dir="ltr">NULL</code> |
| <code dir="ltr">EntryId</code> | <code dir="ltr">uniqueidentifier</code> | لا | الهوية المحفوظة للصف المعدَّل |
| <code dir="ltr">OldValueHash</code> | <code dir="ltr">nvarchar(max)</code> | نعم | تجزئة القيمة القديمة المنقّحة؛ و<code dir="ltr">NULL</code> عند الإنشاء |
| <code dir="ltr">NewValueHash</code> | <code dir="ltr">nvarchar(max)</code> | نعم | تجزئة القيمة الجديدة المنقّحة؛ و<code dir="ltr">NULL</code> عند الحذف/إعادة التسمية |
| <code dir="ltr">PreviousHash</code> | <code dir="ltr">nvarchar(max)</code> | لا | تجزئة السلسلة السابقة أو علامة البداية |
| <code dir="ltr">ChainHash</code> | <code dir="ltr">nvarchar(max)</code> | لا | تجزئة هذا السجل و<code dir="ltr">PreviousHash</code> |

يحفظ <code dir="ltr">AuditRecords</code> تجزئات، لا نص القالب. ويوميات إعادة التشغيل هي الطبقة
التي تحفظ القيم الفعلية.

### StringVersions

| العمود | نوع SQL Server | Null | ملاحظات |
|---|---|---|---|
| <code dir="ltr">Sequence</code> | <code dir="ltr">bigint</code> هوية | لا | مفتاح أساسي، ترتيب الإلحاق |
| <code dir="ltr">Version</code> | <code dir="ltr">bigint</code> | لا | إصدار الكتالوج الفعلي |
| <code dir="ltr">ResourceSet</code> | <code dir="ltr">nvarchar(450)</code> | لا | اسم مجموعة الموارد عند هذا الإصدار |
| <code dir="ltr">Culture</code> | <code dir="ltr">nvarchar(450)</code> | لا | الثقافة عند هذا الإصدار |
| <code dir="ltr">Name</code> | <code dir="ltr">nvarchar(max)</code> | لا | المفتاح عند هذا الإصدار |
| <code dir="ltr">EntryId</code> | <code dir="ltr">uniqueidentifier</code> | لا | الهوية المحفوظة عبر إعادات التسمية |
| <code dir="ltr">Value</code> | <code dir="ltr">nvarchar(max)</code> | نعم | القيمة عند هذا الإصدار؛ و<code dir="ltr">NULL</code> لشاهد حذف/إعادة تسمية |

يحفظ <code dir="ltr">StringVersions</code> قيم القالب الفعلية. تعامل معه بالسرية نفسها التي
تعامل بها الكتالوج الحي.

### CatalogState

| العمود | نوع SQL Server | Null | ملاحظات |
|---|---|---|---|
| <code dir="ltr">Id</code> | <code dir="ltr">int</code> | لا | مفتاح أساسي؛ دائماً <code dir="ltr">1</code> |
| <code dir="ltr">Version</code> | <code dir="ltr">bigint</code> | لا | آخر إصدار كتالوج معيَّن |

يُنشأ الصف كسولاً بأول تعديل محكوم. وغياب الصف يعني الإصدار <code dir="ltr">0</code>.

### StringNotes

| العمود | نوع SQL Server | Null | ملاحظات |
|---|---|---|---|
| <code dir="ltr">Id</code> | <code dir="ltr">uniqueidentifier</code> | لا | مفتاح أساسي |
| <code dir="ltr">ResourceSet</code> | <code dir="ltr">nvarchar(450)</code> | لا | اسم مجموعة الموارد |
| <code dir="ltr">Name</code> | <code dir="ltr">nvarchar(450)</code> | لا | مفتاح الرسالة |
| <code dir="ltr">Note</code> | <code dir="ltr">nvarchar(max)</code> | لا | نص الملاحظة؛ والفارغ صالح |

يفرض الفهرس الفريد على <code dir="ltr">(ResourceSet, Name)</code> ملاحظة واحدة لكل مفتاح.
والملاحظات لا تخص ثقافة، وتنتقل أو تزول مع دورة حياة المفتاح.

## التعايش مع جدول رسائل ADO.NET

يمكن أن تعيش جداول مخزن EF وجدول ADO.NET <code dir="ltr">LocalizationMessages</code> في قاعدة
بيانات واحدة، لكنها مستقلة:

- يُنشأ <code dir="ltr">LocalizationMessages</code> بسكربتات المورّد الموصوفة في
  [دليل قاعدة البيانات](../../database/README.md)؛ ولا ينشئه مخزن EF ولا يهاجره.
- لا يغذّي مخزن EF الواجهة <code dir="ltr">IMessageLocalizer</code>؛ إنه يسند
  <code dir="ltr">ILocalizationStore</code> وجسر <code dir="ltr">IStringLocalizer</code>.
- لن تضيف <code dir="ltr">EnsureCreated</code> جداول المخزن إلى قاعدة بيانات تحوي
  <code dir="ltr">LocalizationMessages</code> بالفعل (أو أي جدول آخر). وفّر المخزن أولاً، أو
  استخدم قاعدة بيانات مخصصة، أو أدِر المخططين بعملية مُراجَعة تملكها.
- المخططان غير متزامنين. وإذا كان التطبيق يستخدم <code dir="ltr">IMessageLocalizer</code> و
  <code dir="ltr">IStringLocalizer</code> معاً، فابذر وتحقّق من المسارين أو اختر واحداً.

راجع [النشر](01-deployment.md) للمقارنة الكاملة، و
[آثار مخطط قاعدة البيانات](../../database/README.md) لسكربتات ADO.NET.

## النسخ الاحتياطية وإعادة الضبط والأذونات

- **انسخ** قاعدة البيانات احتياطياً قبل توفيرها أو تطبيق تغيير مخطط يملكه المستهلك. فجداول
  التدقيق وإعادة التشغيل أدلة، لا بديلاً عن النسخ الاحتياطي.
- **أعد ضبط** بيئة قابلة للتخلص بإسقاط جداول المخزن الخمسة (أو قاعدة البيانات) وتشغيل التوفير
  مجدداً. لا تحذف صفوف تدقيق أو يوميات فردية وتتوقع أن يبقى
  <code dir="ltr">VerifyHistoryAsync</code> ذا معنى.
- **أذونات التوفير** تتطلب DDL لإنشاء الجداول (وقاعدة بيانات إن أنشأت واحدة).
- **أذونات وقت التشغيل** تتطلب <code dir="ltr">SELECT</code> و
  <code dir="ltr">INSERT</code> و<code dir="ltr">UPDATE</code> و
  <code dir="ltr">DELETE</code> على جداول المخزن للإدارة المحكومة؛ ويمكن تضييق نشر للقراءة فقط
  إلى <code dir="ltr">SELECT</code>.
- لا تمنح حساب وقت التشغيل <code dir="ltr">db_owner</code> أو حقوق تغيير المخطط لمجرد تجنب
  التوفير.

## ملكية المخطط والتغييرات المستقبلية

لا تشحن الحزمة **ترحيلات EF Core**. وقاعدة بيانات أُنشئت بـ
<code dir="ltr">EnsureCreated</code> تتضمن النموذج الحالي، بما فيه جداول المساءلة والملاحظات.
وقاعدة بيانات أقدم تحتاج ترحيلاً أو سكربتاً مُراجَعاً مكافئاً قبل تشغيل التعديلات المحكومة.

ولأن المكتبة غير منشورة، لا يوجد مخطط أقدم منشور ولا صفحة ترحيل للمستهلك بعد. وعندما يتغير أول
نموذج منشور، يجب أن يتضمن الإصدار مسار ترقية، ويجب تحديث هذه الصفحة به. وإلى ذلك الحين، تعامل مع
النموذج كعقد قاعدة بيانات جديدة فقط.

للتسلسل التشغيلي الأوسع، راجع [النشر](01-deployment.md). ولـ AOT (Ahead-of-Time)/التقليص، تذكّر أن هذه الحزمة
استبعاد موثّق؛ راجع [AOT والتقليص](../aot-and-trimming.md).

## توثيق ذو صلة

- [README حزمة مخزن EF Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md)
- [مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md)
- [واجهة التخزين](../03-reference/02-storage-api.md)
- [التدقيق والتاريخ](../02-guides/07-audit-and-history.md)
- [النشر](01-deployment.md)
- [القيود المعروفة](../03-reference/10-known-limitations.md)
- [AOT والتقليص](../aot-and-trimming.md)
- [استكشاف الأخطاء](04-troubleshooting.md)

## الترخيص

MIT — راجع [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

</div>
