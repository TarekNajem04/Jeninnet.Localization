<div dir="rtl">

# واجهة التخزين (storage API)

طبقة التخزين (storage layer) هي النطاق الثاني في الإطار. تدير مجموعات موارد (resource sets)
مسمّاة وقابلة للتغيير، وإدارتها المحكومة (governed administration)، وأثر التدقيق (audit trail)،
والتاريخ المؤصَّل (versioned history). وهي ليست خط أنابيب الرسائل (message pipeline) ولا تنفّذ
<code dir="ltr">IMessageProvider</code>.

تعيش العقود في <code dir="ltr">...Abstractions.Store</code>؛ وتنفيذ EF في
<code dir="ltr">...Database.EntityFrameworkCore</code>؛ ومزخرفات الذاكرة المؤقتة في Core.

صفحات الأعضاء المولّدة: <xref:Jeninnet.Localization.Abstractions.Store.ILocalizationStore> و
<xref:Jeninnet.Localization.Abstractions.Store.IResourceAdminStore>.

## ILocalizationStore

<div dir="ltr">

```csharp
public interface ILocalizationStore {
    Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture,
        CancellationToken cancellationToken = default);
    Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture,
        CancellationToken cancellationToken = default);
    bool TryGetResourceSet(string resourceSet, string culture,
        [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet);
    Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet,
        string culture, CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture,
        CancellationToken cancellationToken = default);
    bool TryGetString(string resourceSet, string culture, string name,
        [NotNullWhen(true)] out LocalizedStringEntry? entry);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default);
}
```

</div>

- يحل المخزن زوج <code dir="ltr">(resourceSet, culture)</code> **الدقيق**؛ ولا يوجد مشي في ثقافة
  الأب.
- تعيد <code dir="ltr">GetResourceSetAsync</code> القيمة <code dir="ltr">null</code> عندما لا
  توجد المجموعة. ولا توجد المجموعة إلا ما دامت لها إدخالات محفوظة.
- لا تعيد <code dir="ltr">GetStringAsync</code> القيمة <code dir="ltr">null</code> أبداً لمفتاح
  مفقود. إنها تعيد <code dir="ltr">LocalizedStringEntry.Missing(...)</code>، مع
  <code dir="ltr">Value = "[MISSING:key]"</code> و
  <code dir="ltr">ResourceNotFound = true</code>.
- <code dir="ltr">GetAllStringsForCultureAsync</code> خريطة مسطحة من الاسم إلى الإدخال عبر كل
  المجموعات؛ ويُدمج الاسم المكرر، ويسمح مخزن EF للإدخالات اللاحقة بالكتابة فوق السابقة. وهي ليست
  مؤهلة بالمجموعة.
- <code dir="ltr">TryGetResourceSet</code> و<code dir="ltr">TryGetString</code> مسباران
  متزامنان. ويستخدم جسر <code dir="ltr">IStringLocalizer</code> الدالة
  <code dir="ltr">TryGetResourceSet</code>؛ ويجب أن تنفّذها البدائل الاختبارية.
- تعيد <code dir="ltr">ReloadAsync</code> اكتساب المصدر؛ وتمررها مزخرفات الذاكرة المؤقتة وتبطل
  الحالة المخزّنة.
- تغذّي <code dir="ltr">GetSupportedCulturesAsync</code> اختيار ثقافة الطلب وفحص صحة المخزن.

## ILocalizationResourceSet

يكشف <code dir="ltr">ILocalizationResourceSet</code> الخصائص
<code dir="ltr">ResourceSet</code> و<code dir="ltr">Culture</code> و
<code dir="ltr">GetCultureInfo</code>، وعمليات البحث، والدوال
<code dir="ltr">AddAsync</code>/<code dir="ltr">UpdateAsync</code>/
<code dir="ltr">DeleteAsync</code> في تحميلات إدخال واحد وقابلة للتعداد.

هذا سطح دورة الحياة منخفض المستوى للبذر والاستيراد الجماعي والاختبارات. وهو **ليس** المسار المحكوم:
فـ CRUD (Create, Read, Update, Delete) الخام لا يتحقق عبر <code dir="ltr">ResourceAdminValidation</code>، ولا يبلّغ عن التعارضات،
ولا يرفع أحداث تدقيق، وليس مؤصَّلاً.

<code dir="ltr">EmptyLocalizationResourceSet</code> قيمة حارسة للقراءة فقط لغياب مؤقت؛ تعيد
عمليات البحث إدخالات مفقودة، وترمي عمليات التعديل
<code dir="ltr">NotSupportedException</code>.

## LocalizedStringEntry

يحمل <code dir="ltr">LocalizedStringEntry</code> الخصائص المطلوبة
<code dir="ltr">Id</code> و<code dir="ltr">ResourceSet</code> و
<code dir="ltr">Culture</code> و<code dir="ltr">Name</code>، والقيمة القابلة للتعيين
<code dir="ltr">Value</code>، إضافة إلى <code dir="ltr">ResourceNotFound</code> والدالة
<code dir="ltr">Missing(resourceSet, culture, name)</code>.

يكون <code dir="ltr">Id</code> مساوياً <code dir="ltr">Guid.Empty</code> للقيمة الحارسة
للمفقود؛ والمساواة حسب <code dir="ltr">Id</code>. وتضيف
<code dir="ltr">ToString()</code> البادئة <code dir="ltr">[MISSING] </code> فقط عندما لا تحتوي
القيمة بالفعل على <code dir="ltr">MISSING</code>؛ ويعيد التحويل الضمني
<code dir="ltr">Value</code>.

تملك <code dir="ltr">ResourceAdminValidation</code> قواعد الشكل: أسماء مجموعات الموارد والمفاتيح
غير بيضاء؛ ويجب أن تُحل الثقافات عبر <code dir="ltr">CultureInfo.GetCultureInfo</code> وتُخزَّن
حرفياً؛ وقد تكون القوالب والملاحظات فارغة لكن ليست null. وإخفاقات التحقق تُرمى من عائلة
<code dir="ltr">ArgumentException</code>.

## IResourceAdminStore

<div dir="ltr">

```csharp
public interface IResourceAdminStore : ILocalizationStore {
    bool SupportsAdministration { get; }
    event EventHandler<ResourceAdminAuditEventArgs>? Audited;

    // Entry CRUD and renames:
    Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name,
        string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name,
        string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    // Notes: reads are un-gated; mutations take ResourceAdminOptions and a token.
    Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
}
```

</div>

يحمل <code dir="ltr">ResourceAdminOptions</code> الخيار <code dir="ltr">DryRun</code>
(افتراضياً <code dir="ltr">false</code>) و<code dir="ltr">Actor</code> (افتراضياً فارغ). ويحسب
التشغيل التجريبي النتيجة دون تغيير أي شيء ولا يرفع إدخالات تدقيق. ويُسجَّل الفاعل في كل إدخال
تدقيق.

- **تُعاد التعارضات المتوقعة، لا تُرمى.** يكشف
  <code dir="ltr">ResourceAdminResult</code> الخصائص <code dir="ltr">Applied</code> و
  <code dir="ltr">DryRun</code> و<code dir="ltr">AffectedEntries</code> و
  <code dir="ltr">Conflict</code>. والنوع هو <code dir="ltr">AlreadyExists</code> أو
  <code dir="ltr">NotFound</code> أو <code dir="ltr">TargetExists</code>.
- **إخفاقات التحقق تُرمى** من عائلة <code dir="ltr">ArgumentException</code> قبل وجود نتيجة.
- **المخزن للقراءة فقط يرمي <code dir="ltr">StoreReadOnlyException</code>** قبل إنتاج نتيجة.
- **إعادات التسمية ذرّية عبر الثقافات.** إعادة تسمية مفتاح أو مجموعة إما تُثبَّت في كل ثقافة
  متأثرة أو تبلّغ عن تعارض ولا تغيّر شيئاً.
- **الملاحظات على مستوى المفتاح ولا تخص ثقافة.** تتطلب الملاحظة وجود المفتاح في ثقافة واحدة على
  الأقل؛ فلا توجد ملاحظات يتيمة. وإعادة التسمية تنقل الملاحظة؛ وحذف آخر صف ثقافة للمفتاح يحذفها.

<code dir="ltr">store.RequireAdmin()</code> بوابة القدرة وترمي
<code dir="ltr">StoreReadOnlyException</code> عند عدم الدعم؛ وتجيب
<code dir="ltr">store.SupportsAdministration()</code> بالقيمة المنطقية دون رمي.

## التدقيق والتاريخ

<code dir="ltr">Audited</code> هو الحدث الحي المتزامن. يُطلق مرة واحدة لكل صف معدَّل بعد التثبيت،
وبترتيب الثقافات الترتيبي للعمليات متعددة الصفوف. والتشغيل التجريبي أو التعارض لا يرفع شيئاً.
والمعالج الذي يرمي ينتشر استثناؤه رغم أن التعديل مثبَّت؛ أبقِ المعالجات سريعة وغير رامية.

يحمل <code dir="ltr">ResourceHistoryRecord</code> الخصائص <code dir="ltr">Sequence</code> (ترتيب
الإلحاق)، و<code dir="ltr">Version</code> (إصدار الدفعة الرتيب)، و
<code dir="ltr">Timestamp</code>، و<code dir="ltr">Actor</code>، و
<code dir="ltr">Operation</code>، و<code dir="ltr">ResourceSet</code>، و
<code dir="ltr">Culture</code>، و<code dir="ltr">Key</code>، و
<code dir="ltr">NewKey</code>، و<code dir="ltr">EntryId</code>، و
<code dir="ltr">OldValueHash</code>، و<code dir="ltr">NewValueHash</code>.

<div dir="ltr">

```csharp
public interface IResourceHistoryStore : ILocalizationStore {
    bool SupportsHistory { get; }
    Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(
        string resourceSet, string culture, long version, CancellationToken cancellationToken = default);
    Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default);
}
```

</div>

- يغطي التاريخ المسار المحكوم فقط. وCRUD الخام لمجموعات الموارد غير مؤصَّل وغير مدقَّق.
- الإصدارات رتيبة ويتشاركها كل سجل في دفعة واحدة. و<code dir="ltr">Sequence</code> مستقل
  ويتزايد بصرامة.
- تستخدم <code dir="ltr">GetHistoryAsync</code> حداً أدنى حصرياً: السجلات ذات
  <code dir="ltr">Version &gt; sinceVersion</code> فقط. و<code dir="ltr">0</code> يقرأ الأثر
  كله.
- <code dir="ltr">GetStringsAsOfVersionAsync</code> مطابقة لإعادة التشغيل. والإصدار
  <code dir="ltr">0</code> هو الحالة الفارغة قبل التاريخ.
- تحفظ سجلات التدقيق تجزئات SHA-256 (Secure Hash Algorithm, 256-bit) السادسة عشرية الصغيرة لبايتات القالب بترميز UTF-8 (Unicode Transformation Format, 8-bit)، لا نص
  القالب. ويحفظ يوميات إعادة التشغيل القيم الفعلية، لذا احمها كبيانات كتالوج.
- يعيد <code dir="ltr">VerifyHistoryAsync</code> حساب سلسلة تجزئة غير مرساة. ويكشف التعديل
  والإدراج والإزالة العاديين؛ ولا يستطيع إثبات الأصالة ضد مهاجم يعيد كتابة السلسلة.

<code dir="ltr">store.RequireHistory()</code> بوابة القدرة وترمي
<code dir="ltr">StoreHistoryUnsupportedException</code> عند غياب التاريخ.

## المزامنة

تعيد <code dir="ltr">IResourceSynchronizer.SynchronizeAsync(declarations, options, token)</code>
كائن <code dir="ltr">ResourceSynchronizationReport</code>. ويحمل
<code dir="ltr">ResourceDeclaration</code> مجموعة موارد ومفتاحاً وقالباً افتراضياً وملاحظة
اختيارية. ويحمل <code dir="ltr">ResourceSynchronizationOptions</code> الخيار
<code dir="ltr">DefaultCulture</code> (مطلوب)، و<code dir="ltr">TargetCultures</code>، و
<code dir="ltr">DryRun</code>، و<code dir="ltr">AllowUpdates</code>، و
<code dir="ltr">CreateMissingTargetCultures</code>، و<code dir="ltr">Renames</code>، و
<code dir="ltr">Actor</code>.

- يضيف فقط افتراضياً؛ وقيم الثقافة الافتراضية المختلفة تكون
  <code dir="ltr">Skipped</code> ما لم يُعيَّن <code dir="ltr">AllowUpdates</code>. ولا تُحدَّث
  قيم الثقافات غير الافتراضية أبداً.
- تُتخطى ثقافات الهدف المفقودة كي ينطبق الارتداد، ما لم تنشئ
  <code dir="ltr">CreateMissingTargetCultures</code> عناصر نائبة فارغة. والقيم الفارغة صالحة
  وتكبت الارتداد.
- إعادة التسمية صريحة عبر <code dir="ltr">Renames</code>؛ والملاحظات تتبع التصريحات (تُطبَّق عند
  الإنشاء، ولا تُدرَج أو تُحدَّث إلا مع <code dir="ltr">AllowUpdates</code>).
- لا حذف. والتعارضات لكل مفتاح لا توقف التشغيل. والتشغيل الثاني متطابق الأثر ولا يدقق شيئاً.

يحتوي التقرير عدادات <code dir="ltr">Created</code> و<code dir="ltr">Updated</code> و
<code dir="ltr">Unchanged</code> و<code dir="ltr">Skipped</code> و
<code dir="ltr">Conflict</code> و<code dir="ltr">Renamed</code>، إضافة إلى تفاصيل كل إدخال.
تعامل معه كدليل نشر، لا كقيمة منطقية.

## مزخرفات الذاكرة المؤقتة

يخزّن <code dir="ltr">CachedLocalizationStoreDecorator(ILocalizationStore innerStore, IMemoryCache cacheService)</code>
مجموعات الموارد داخل العملية. وينفّذ <code dir="ltr">IResourceAdminStore</code> و
<code dir="ltr">IResourceHistoryStore</code> بالتمرير؛ وتبطل إعادات التحميل مجموعات الموارد
المخزّنة؛ وتُمرَّر قراءات التاريخ ولا تُخزَّن أبداً.

يعكس <code dir="ltr">DistributedLocalizationStoreDecorator(innerStore, distributedCache, options)</code>
الدلالات عبر العمليات. والخيارات: <code dir="ltr">Expiration</code> (افتراضياً 30 دقيقة؛ موجبة)
و<code dir="ltr">KeyPrefix</code> (افتراضياً <code dir="ltr">Jeninnet.Localization</code>؛ بلا نقطتين لاحقتين). وينفّذ
<code dir="ltr">IDistributedStoreCacheInvalidator</code> (الدوال
<code dir="ltr">InvalidateEntryAsync</code> و<code dir="ltr">InvalidateCultureAsync</code> و
<code dir="ltr">BumpGenerationAsync</code> و<code dir="ltr">GetGenerationAsync</code>)؛ والإبطال
قائم على طوابع، لذا تُتجاهل الحمولات الأقدم عبر العنقود. اربط المزخرفات صراحةً؛ وليس لأي منهما
امتداد <code dir="ltr">Add...</code>.

## مخزن EF Core

تسجّل <code dir="ltr">AddEntityFrameworkLocalizationStore(connectionString)</code> كائن
<code dir="ltr">LocalizationDataContext</code> عبر
<code dir="ltr">AddDbContextFactory</code> والنوع
<code dir="ltr">SqlDbLocalizationStore</code> كـ singleton
<code dir="ltr">ILocalizationStore</code>. ويقرأ تحميل
<code dir="ltr">IConfiguration</code> سلسلة اتصال مسماة، افتراضياً
<code dir="ltr">LocalizationDb</code>.

ينفّذ المخزن <code dir="ltr">ILocalizationStore</code> و
<code dir="ltr">IResourceAdminStore</code> و<code dir="ltr">IResourceHistoryStore</code>، مع
<code dir="ltr">LocalizedStrings</code> وسجلات التدقيق ويوميات إعادة التشغيل وصف الإصدار
والملاحظات. وإدارة المخطط مسؤولية المستهلك: يضم <code dir="ltr">EnsureCreated</code> النموذج
الحالي في قاعدة بيانات فارغة، وكل قاعدة بيانات قائمة تحتاج خطوة مخطط صريحة. راجع
[مخطط مخزن EF Core](../04-operations/06-ef-core-store-schema.md) لجرد الجداول وسلوك التوفير.

مخزن EF **ليس** <code dir="ltr">IMessageProvider</code>؛ وتستخدم مزوّدات رسائل قواعد البيانات
جدولاً مختلفاً (<code dir="ltr">LocalizationMessages</code>). ومخزن EF مستبعد من عقد Native AOT (Ahead-of-Time)؛
راجع [التوافق والإصدارات](07-compatibility-and-versions.md).

## مثال عملي

إنشاء بتشغيل تجريبي يعيد <code dir="ltr">Applied = false</code> و
<code dir="ltr">DryRun = true</code> وعدد الصفوف المتوقع؛ ويعيد النداء المطبَّق
<code dir="ltr">Applied = true</code>.

## مآزق

**مأزق:** CRUD الخام لمجموعات الموارد يتجاوز التحقق والتدقيق والتأصيل؛ والاحتفاظ بمجموعة موارد
كذاكرة مؤقتة يخفي إعادات التحميل.

## التزامن والتأصيل

- المخزن singleton؛ ويستخدم القراء سياقات مستقلة لكل نداء.
- يسلسل مخزن EF التعديلات المحكومة لكل نسخة مخزن، ويستخدم تحديثاً شرطياً بإعادة محاولة محدودة
  لعدّاد الإصدار، فلا يستطيع الكتاب داخل العملية مشاركة إصدار.
- لا تنسّق تلك البوابة كتاباً خارج العملية؛ أضف قيود مخطط أو سلسل الكتابات بينهم.
- القيم المحفوظة <code dir="ltr">Sequence</code> و<code dir="ltr">Version</code>، لا الوقت
  الفعلي، هي المرجع.
- CRUD الخام لمجموعات الموارد غير محكوم كالمسار المحكوم؛ لا تخلطهما.

## إلى أين بعد ذلك

- [مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md) لسير العمل.
- [التدقيق والتاريخ](../02-guides/07-audit-and-history.md) لنموذج المساءلة.

</div>
