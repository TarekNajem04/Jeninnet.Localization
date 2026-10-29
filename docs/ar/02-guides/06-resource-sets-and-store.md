<div dir="rtl">

# مجموعات الموارد والمخزن (resource sets and the store)

مخزن مجموعات الموارد (resource-set store) هو النطاق الثاني في المكتبة. إنه ليس مزوّد رسائل ولا
يشارك في خط أنابيب الرسائل. يوجد من أجل مجموعات نصية مسمّاة وقابلة للتغيير تحتاج إلى عمليات
CRUD (Create, Read, Update, Delete) والتحقق والتدقيق وجسر مستقر لـ ASP.NET Core.

اقرأ هذا الفصل عندما تحتاج إلى <code dir="ltr">IStringLocalizer</code>، أو كتالوج قابل للتعديل
وقت التشغيل، أو ملاحظات المترجمين، أو مزامنة قائمة على الكود. وإذا كنت تحتاج
<code dir="ltr">IMessageLocalizer</code> فقط، فيمكنك تجاوز المخزن كلياً.

## ما هي مجموعة الموارد

تُعرَّف مجموعة الموارد باسم مجموعة (<code dir="ltr">Bookstore</code>)، وثقافة محددة
(<code dir="ltr">en</code> أو <code dir="ltr">en-US</code> أو <code dir="ltr">ar</code>)، ومفتاح
(<code dir="ltr">Name</code>). ويكشف المخزن مجموعة موارد كاملة
<code dir="ltr">ILocalizationResourceSet</code> أو قيم إدخالات فردية
<code dir="ltr">LocalizedStringEntry</code> تحمل <code dir="ltr">Id</code> و
<code dir="ltr">ResourceSet</code> و<code dir="ltr">Culture</code> و<code dir="ltr">Name</code>
و<code dir="ltr">Value</code> و<code dir="ltr">ResourceNotFound</code>.

لا يوجد ارتداد داخل المخزن. تبحث
<code dir="ltr">GetResourceSetAsync("Bookstore", "de-CH")</code> عن ذلك الزوج بالضبط وتعيد
<code dir="ltr">null</code> عندما يوجد <code dir="ltr">de</code> فقط. كما لا يمشي جسر ASP.NET
Core في الآباء.

لا توجد المجموعة إلا ما دامت لها إدخالات محفوظة: إنشاء أول إدخال ينشئها، وحذف آخر إدخال يزيلها.

## عقد المخزن

<code dir="ltr">ILocalizationStore</code> هو سطح القراءة وإعادة التحميل:

| العضو | الغرض |
|---|---|
| <code dir="ltr">GetResourceSetAsync</code> / <code dir="ltr">GetResourceSetsAsync</code> | إعادة مجموعة واحدة أو كل المجموعات لثقافة؛ والمجموعة المفقودة <code dir="ltr">null</code>. |
| <code dir="ltr">TryGetResourceSet</code> / <code dir="ltr">TryGetString</code> | فحوص متزامنة. |
| <code dir="ltr">GetStringAsync</code> | إعادة إدخال، أو قيمة حارسة للغياب. |
| <code dir="ltr">GetAllStringsAsync</code> / <code dir="ltr">GetAllStringsForCultureAsync</code> | إعادة مجموعة/ثقافة واحدة، أو خريطة أسماء على مستوى الثقافة. |
| <code dir="ltr">ReloadAsync</code> / <code dir="ltr">GetSupportedCulturesAsync</code> | إعادة تحميل المصدر؛ وسرد الثقافات المخدومة. |

لا تعيد <code dir="ltr">GetStringAsync</code> القيمة <code dir="ltr">null</code> أبداً لمفتاح
مفقود. إنها تعيد <code dir="ltr">LocalizedStringEntry.Missing(...)</code>، الذي يكون
<code dir="ltr">Value</code> فيه <code dir="ltr">[MISSING:key]</code> و
<code dir="ltr">ResourceNotFound</code> هو <code dir="ltr">true</code>. أما خط أنابيب الرسائل
المرجعي فيعيد <code dir="ltr">null</code> بدلاً من ذلك.

تعيد <code dir="ltr">GetAllStringsForCultureAsync</code> خريطة مسطحة من الاسم إلى الإدخال. وإذا
وجد الاسم نفسه في أكثر من مجموعة، تدمجها التنفيذات؛ ويسمح مخزن EF للإدخالات اللاحقة بالكتابة
فوق السابقة. وهي ليست خريطة مؤهلة بالمجموعة.

### CRUD منخفض المستوى لمجموعة الموارد

يكشف <code dir="ltr">ILocalizationResourceSet</code> الدوال
<code dir="ltr">GetStringAsync</code> و<code dir="ltr">GetAllStringsAsync</code> و
<code dir="ltr">TryGetString</code> و<code dir="ltr">AddAsync</code> و
<code dir="ltr">UpdateAsync</code> و<code dir="ltr">DeleteAsync</code>، مع تحميلات لإدخال واحد
وتحميلات قابلة للتعداد. هذه عمليات دورة الحياة منخفضة المستوى. وهي مفيدة للبذر والاستيراد
الجماعي والاختبارات.

لكنها **ليست** مسار الإدارة المحكوم. لا يتحقق CRUD الخام عبر
<code dir="ltr">ResourceAdminValidation</code>، ولا يبلّغ عن التعارضات في
<code dir="ltr">ResourceAdminResult</code>، ولا يرفع أحداث تدقيق، وليس مُصدَّراً بإصدار. ويوثّق
مخزن EF هذا الحد صراحةً: كتابات مجموعات الموارد الخام خارج نموذج المساءلة.

يمكن تمثيل المجموعات المفقودة بـ <code dir="ltr">EmptyLocalizationResourceSet</code>، وهي قيمة
حارسة للقراءة فقط تعيد إدخالات مفقودة وترمي <code dir="ltr">NotSupportedException</code> من
دوال التعديل. ويستخدمها جسر ASP.NET Core لغياب مؤقت كي لا يفشل البحث.

## جسر ASP.NET Core

ينفّذ <code dir="ltr">CustomStringLocalizerFactory</code> الواجهة
<code dir="ltr">IStringLocalizerFactory</code> فوق <code dir="ltr">ILocalizationStore</code>.

- يستخدم <code dir="ltr">Create(Type)</code> الخاصية
  <code dir="ltr">resourceSource.Name</code>؛ ويستخدم
  <code dir="ltr">Create(baseName, location)</code> الاسم الأساسي الدقيق ويعامل
  <code dir="ltr">location</code> كتحقق من العقد فقط.
- يلتقط المُعرِّب <code dir="ltr">CurrentUICulture.Name</code> عند الإنشاء، ويعيد فحص المجموعة
  والثقافة الدقيقتين في كل بحث، لذا لا يكون الغياب المؤقت دائماً.

أنشئ مُعرِّباً جديداً عندما تتغير ثقافة واجهة المستخدم. النوع
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> عابر ويحصل على نتيجة مصنع جديدة لكل حل؛ أما
المُعرِّب طويل العمر فيحتفظ بثقافته الملتقطة.

تظهر الإدخالات المفقودة كـ <code dir="ltr">[MISSING:key]</code> مع
<code dir="ltr">ResourceNotFound = true</code>. وتعدّد
<code dir="ltr">GetAllStrings(true)</code> الثقافة الدقيقة الملتقطة فقط؛ ولا يكرر الجسر مسير
الآباء المرجعي. ويستخدم المفهرس ذو الوسائط
<code dir="ltr">string.Format(CultureInfo.CurrentCulture, ...)</code>، لذا تستخدم قيم المخزن
عناصر نائبة مركبة (<code dir="ltr">{0}</code> و<code dir="ltr">{1}</code>)، لا العناصر النائبة
المسمّاة في خط الأنابيب.

لا توجد <code dir="ltr">IStringLocalizer</code> جذرية مسجّلة؛ حلّ
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> أو أنشئ المُعرِّب غير العام من
<code dir="ltr">IStringLocalizerFactory</code>.

## مخزن EF Core

توفّر حزمة <code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code>
النوعين <code dir="ltr">LocalizationDataContext</code> و
<code dir="ltr">SqlDbLocalizationStore</code>. وسجّلها بـ:

<code dir="ltr">services.AddEntityFrameworkLocalizationStore(configuration, connectionStringName: "LocalizationDb")</code>
يسجّل السياق عبر <code dir="ltr">AddDbContextFactory</code> والمخزن كـ singleton
<code dir="ltr">ILocalizationStore</code>. وينفّذ المخزن
<code dir="ltr">IResourceAdminStore</code> و<code dir="ltr">IResourceHistoryStore</code>؛ ويشمل
مخططه <code dir="ltr">LocalizedStrings</code>، وسجلات التدقيق، ويوميات إعادة التشغيل، وصف
الإصدار، والملاحظات.

إدارة المخطط مسؤولية المستهلك؛ فـ <code dir="ltr">EnsureCreated</code> يوفّر النموذج الحالي في
قاعدة بيانات فارغة فقط، وقاعدة البيانات التي تحتوي أي جدول قائم تحتاج خطوة مخطط صريحة قبل
التعديلات المحكومة. راجع
[مخطط مخزن EF Core](../04-operations/06-ef-core-store-schema.md) للجداول وفحوص التوفير وقواعد
التعايش. مخزن EF ليس <code dir="ltr">IMessageProvider</code> ولا يغذّي
<code dir="ltr">IMessageLocalizer</code>؛ وتستخدم مزوّدات رسائل قواعد البيانات جداول مختلفة.

## الإدارة المحكومة

يمدّ <code dir="ltr">IResourceAdminStore</code> الواجهة
<code dir="ltr">ILocalizationStore</code> بمسار كتابة محكوم. القدرة ذات مستويين: تنفيذ الواجهة
هو التصريح على مستوى النوع؛ و<code dir="ltr">SupportsAdministration</code> هو الجواب الصادق على
مستوى النسخة. والمخزن الذي يبلّغ <code dir="ltr">false</code> يرفض كل تعديل بـ
<code dir="ltr">StoreReadOnlyException</code>. استخدم دوال الامتداد بدلاً من التحويل اليدوي:

<div dir="ltr">

```csharp
using Jeninnet.Localization.Abstractions.Store;

var admin = store.RequireAdmin(); // throws StoreReadOnlyException when unsupported
```

</div>

العمليات هي إنشاء/تحديث/حذف الإدخال؛ و<code dir="ltr">RenameKeyAsync</code> (ذرّية عبر
الثقافات)؛ و<code dir="ltr">DeleteResourceSetAsync</code>؛ و
<code dir="ltr">RenameResourceSetAsync</code> (ذرّية عبر الثقافات)؛ والحصول على الملاحظات
وتعيينها وحذفها. الملاحظات على مستوى المفتاح، ولا تخص ثقافة، وتنتقل أو تزول مع دورة حياة المفتاح.

يقبل كل تعديل كائن <code dir="ltr">ResourceAdminOptions</code> اختيارياً مع
<code dir="ltr">DryRun</code> و<code dir="ltr">Actor</code>. التشغيل التجريبي (dry run) لا يغيّر
شيئاً ولا يرفع إدخالات تدقيق؛ ويُسجَّل الفاعل في كل إدخال تدقيق.

تعارضات النطاق المتوقعة **تُعاد**، لا تُرمى. يبلّغ
<code dir="ltr">ResourceAdminResult</code> عن <code dir="ltr">Applied</code> و
<code dir="ltr">DryRun</code> و<code dir="ltr">AffectedEntries</code>، وعن
<code dir="ltr">Conflict</code> اختياري بنوع <code dir="ltr">AlreadyExists</code> أو
<code dir="ltr">NotFound</code> أو <code dir="ltr">TargetExists</code>. أما إخفاقات التحقق
(معرّفات غير صالحة، ثقافات مجهولة، قيم null) فتُرمى من عائلة
<code dir="ltr">ArgumentException</code>. ويرمي المخزن للقراءة فقط
<code dir="ltr">StoreReadOnlyException</code> قبل إنتاج نتيجة.

<div dir="ltr">

```csharp
var result = await admin.CreateEntryAsync("Bookstore", "en", "App.Title", "Bookstore",
    new ResourceAdminOptions { Actor = "seed" });

if (!result.Applied) {
    Console.WriteLine($"{result.Conflict!.Kind}: {result.Conflict.Detail}");
}
```

</div>

ملاحظات المترجمين على مستوى المفتاح ولا تخص ثقافة. يتطلب
<code dir="ltr">SetNoteAsync</code> أن يوجد المفتاح في ثقافة واحدة على الأقل؛ فلا توجد ملاحظات
يتيمة. إعادة تسمية المفتاح تنقل الملاحظة. وحذف آخر صف ثقافة للمفتاح يحذف الملاحظة. الملاحظات
ظاهرة على السطح المحكوم؛ وليست جزءاً من قالب الرسالة.

## المزامنة القائمة على الكود

يطبّق <code dir="ltr">ResourceSynchronizer</code> قيم
<code dir="ltr">ResourceDeclaration</code> عبر المسار المحكوم نفسه.

<div dir="ltr">

```csharp
var declarations = new[] {
    new ResourceDeclaration("Bookstore", "App.Title", "Bookstore"),
    new ResourceDeclaration("Bookstore", "Cart.Items", "You have {count} items.")
};

var synchronizer = new ResourceSynchronizer(admin);
_ = await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "deploy"
    });
```

</div>

الدلالات:

- **الإضافة فقط افتراضياً.** قيم الثقافة الافتراضية المختلفة تكون
  <code dir="ltr">Skipped</code> ما لم يُعيَّن <code dir="ltr">AllowUpdates</code>؛ ولا تُحدَّث
  قيم الثقافات غير الافتراضية أبداً.
- **ثقافات الهدف محافظة.** تُتخطى إدخالات الهدف المفقودة كي يظل الارتداد يعمل. وتنشئ
  <code dir="ltr">CreateMissingTargetCultures</code> عناصر نائبة فارغة، وهي تكبت الارتداد.
- **إعادة التسمية صريحة.** تطبّق <code dir="ltr">Renames</code> فقط حيث يُصرَّح بالمفتاح الجديد،
  ولا يُصرَّح بالقديم، ويوجد القديم، عبر إعادة التسمية الذرّية عبر الثقافات.
- **الملاحظات تتبع التصريحات.** تُطبَّق عند الإنشاء؛ ولا تُدرَج أو تُحدَّث إلا مع
  <code dir="ltr">AllowUpdates</code>.
- **لا حذف، والتعارضات لا توقف التشغيل.** تُبلَّغ التعارضات لكل مفتاح وتستمر المفاتيح الأخرى.
  والتشغيل الثاني متطابق الأثر (idempotent): <code dir="ltr">Unchanged</code>، بلا تعديلات وبلا
  إدخالات تدقيق.

يسرد التقرير نتائج كل إدخال وأعدادها. تعامل معه كدليل نشر، لا كقيمة منطقية.
ويستطيع <code dir="ltr">LocalizedResourceDiscovery</code> العثور على سمات
<code dir="ltr">[LocalizedResource]</code>، لكنه قائم على الانعكاس ويحمل
<code dir="ltr">RequiresUnreferencedCode</code>؛ وتُمرّر عمليات نشر trimmed/AOT (Ahead-of-Time) التصريحات
صراحةً.

## توقعات التزامن

- المخزن singleton. يستخدم القراء سياقات مستقلة لكل نداء؛ لا تحتفظ بكائن مجموعة موارد كذاكرة
  مؤقتة طويلة العمر.
- يسلسل مخزن EF التعديلات المحكومة لكل نسخة مخزن، ويستخدم تحديثاً شرطياً بإعادة محاولة محدودة
  لعدّاد الإصدار، فلا يستطيع الكتاب داخل العملية مشاركة إصدار.
- لا ينسّق ذلك البوابة كتاباً خارج العملية. يستخدم عدّاد الإصدار تحديثاً شرطياً، لكن تفرد
  المفاتيح وترتيبها يعتمدان على مخططك؛ أضف قيوداً أو سلسل الكتاب خارجياً. القيم المحفوظة
  <code dir="ltr">Sequence</code> و<code dir="ltr">Version</code>، لا الوقت الفعلي، هي المرجع.
- CRUD الخام لـ <code dir="ltr">ILocalizationResourceSet</code> غير محكوم كالمسار المحكوم؛ لا
  تخلطهما.
- مزخرفات الذاكرة المؤقتة لكل عملية: يستخدم
  <code dir="ltr">CachedLocalizationStoreDecorator</code> الواجهة
  <code dir="ltr">IMemoryCache</code>، ويستخدم
  <code dir="ltr">DistributedLocalizationStoreDecorator</code> الواجهة
  <code dir="ltr">IDistributedCache</code> و
  <code dir="ltr">DistributedStoreCacheOptions</code>. وليس لأي منهما امتداد
  <code dir="ltr">Add...</code>؛ اربطهما صراحةً.

## مثال عملي: معاينة ثم بذر

<div dir="ltr">

```csharp
var options = new ResourceSynchronizationOptions {
    DefaultCulture = "en", TargetCultures = ["ar"], Actor = "release-2026-10"
};

var preview = await synchronizer.SynchronizeAsync(declarations, new ResourceSynchronizationOptions {
    DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "release-2026-10"
});

if (preview.Conflicts > 0) {
    throw new InvalidOperationException("Catalog seed conflicts: " + preview.Conflicts);
}

await synchronizer.SynchronizeAsync(declarations, options);
```

</div>

المعاينة لا تنشئ ولا تدقق شيئاً؛ والتشغيل المطبّق ينشئ الإدخالات المفقودة فقط، والتشغيل الثاني
متطابق الأثر. أبقِ خيارات المعاينة والتطبيق متطابقة إلا في <code dir="ltr">DryRun</code>.

## مآزق

**مأزق:** توقع أن يغذّي المخزن <code dir="ltr">IMessageLocalizer</code>؛ أبقِ المسارين متوافقين
عن قصد.

**مأزق:** استخدام CRUD الخام لـ <code dir="ltr">ILocalizationResourceSet</code> في التعديلات
الإدارية. إنه يتجاوز التحقق والإبلاغ عن التعارضات والتدقيق والإصدار. استخدم
<code dir="ltr">IResourceAdminStore</code> أو المزامن للتغييرات المحكومة.

**مأزق:** إنشاء ثقافات هدف مفقودة كسلاسل فارغة "لاستكمال" المصفوفة. القيم الفارغة قيم مخزّنة
صالحة وتكبت الارتداد، لذا قد تعرض الواجهة فراغاً بدلاً من اللغة الافتراضية. اتركها غائبة ما لم
يكن العنصر النائب الفارغ مقصوداً.

## إلى أين بعد ذلك

- [التدقيق والتاريخ](07-audit-and-history.md) لنموذج الأحداث والتاريخ المحفوظ.
- [ASP.NET Core وMVC](08-aspnet-core-and-mvc.md) لمستهلكي الجسر.
- [واجهة التخزين](../03-reference/02-storage-api.md) لسطح العقد الكامل.

</div>
