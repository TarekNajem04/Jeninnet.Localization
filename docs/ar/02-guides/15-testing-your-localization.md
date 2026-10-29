<div dir="rtl">

# اختبار التعريب (testing your localization)

تفشل اختبارات التعريب في اتجاهين. قد تنجح بينما التطبيق غير مترجم، وقد تفشل لأن اختباراً سرّب
<code dir="ltr">CurrentUICulture</code> إلى الاختبار التالي. المجموعة المفيدة تفحص خوارزمية البحث،
وعقد الكتالوج، وحدود الثقافة كلاً على حدة.

مجموعة المستودع مشروع MSTest واحد يستخدم <code dir="ltr">Jeninnet.Assertions</code> لتأكيدات
<code dir="ltr">Should()</code>. والأنماط التالية تعمل مع أي إطار اختبار يدعم عدم التزامن.

## اختبر الخوارزمية، لا النتيجة فقط

اختبار ارتداد يؤكد السلسلة المعادة فقط قد ينجح لسبب خاطئ. أكّد التسلسل الدقيق لاستعلامات المزوّد
بدلاً من ذلك:

<div dir="ltr">

```csharp
sealed class RecordingProvider(IReadOnlyDictionary<string, string> templates) : IMessageProvider {
    public List<string> QueriedCultures { get; } = [];
    public ValueTask<string?> GetMessageTemplateAsync(
        string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        QueriedCultures.Add(culture.Name);
        return ValueTask.FromResult(
            templates.TryGetValue($"{culture.Name}:{key}", out var value) ? value : null);
    }
}

var provider = new RecordingProvider(new Dictionary<string, string> { ["de:Greeting"] = "Hallo" });
var localizer = new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en" });

await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("de-CH"));
provider.QueriedCultures.Should().Equal("de-CH", "de");
```

</div>

غطِّ الحالات الست التي تثبّت خوارزمية الارتداد الواحدة: الإصابة التامة، ومشي الآباء متعدد
المستويات، واستبعاد الثقافة الثابتة، ووصول الثقافة الافتراضية، والغياب الكلي الذي يعيد
<code dir="ltr">null</code>، واستعلام الافتراضية المكرر عندما تتساوى الافتراضية مع الثقافة
المطلوبة. بعد ذلك لا تحتاج اختبارات المزوّد إلا لإثبات سلوك المطابقة التامة.

## مزوّدون ومخازن وهميون

استخدم <code dir="ltr">MemoryMessageProvider</code> لخريطة ثابتة مسطحة. واستخدم مزوّد تسجيل عندما
يجب أن يلاحظ الاختبار الاستعلامات. واستخدم <code dir="ltr">CultureAwareMessageProvider</code>
عندما يحتاج الاختبار توزيعاً بالاسم الدقيق ثم بحرفي اللغة.

لاختبارات المخزن، نفّذ <code dir="ltr">ILocalizationStore</code> مباشرة. والتفصيل المهم هو المسبار
المتزامن: تنادي <code dir="ltr">CustomStringLocalizerFactory</code> و
<code dir="ltr">StoreBackedStringLocalizer</code> الدالة
<code dir="ltr">TryGetResourceSet</code>. والمخزن الوهمي الذي يرمي من ذلك العضو بحجة أن "المسار
غير المتزامن هو الحقيقي" يجعل كل اختبار <code dir="ltr">IStringLocalizer</code> يفشل بطريقة
محيّرة. أبقِ وهمياً مسنوداً بقاموس ونفّذ <code dir="ltr">TryGetResourceSet</code> و
<code dir="ltr">TryGetString</code> والأعضاء غير المتزامنة فوق الخريطة نفسها.

وإذا احتاج اختبار إلى الإدارة، فنفّذ وهمياً صغيراً لـ
<code dir="ltr">IResourceAdminStore</code> بدلاً من اللجوء إلى مخزن EF. اختبارات وقت التشغيل لمخزن
EF مقيدة بالبيئة لأنها تحتاج SQL Server حقيقياً.

## اختبارات مستقلة عن الثقافة

مرّر الثقافة صراحةً. <code dir="ltr">LocalizeAsync(key, parameters, culture)</code> بلا اعتماد على
الثقافة المحيطة، ومعظم الاختبارات يجب أن تستخدمه.

وعندما يجب أن يستخدم اختبار التحميل المريح، عيّن الثقافة المحيطة وأعدها في
<code dir="ltr">finally</code>:

<div dir="ltr">

```csharp
var previous = CultureInfo.CurrentUICulture;
try {
    CultureInfo.CurrentUICulture = new CultureInfo("ar-SY");
    (await localizer.LocalizeAsync("key", parameters)).Should().BeNull();
} finally { CultureInfo.CurrentUICulture = previous; }
```

</div>

الاختبارات التي تعدّل الثقافة المحيطة يجب ألا تعمل بالتوازي مع اختبارات تقرأها. ويضع المستودع
عليها السمة <code dir="ltr">[DoNotParallelize]</code>. لا تعتمد على ترتيب الاختبارات.

لا تؤكد رقماً من <code dir="ltr">DefaultMessageFormatter</code> مقابل سلسلة ثابتة ما لم يمرّر
الاختبار الثقافة صراحةً. فالمنسّق يستخدم <code dir="ltr">value?.ToString()</code> والثقافة
المحيطة <code dir="ltr">CurrentCulture</code> للأرقام والتواريخ. والاختبار الذي ينجح في
<code dir="ltr">en-US</code> ويفشل على جهاز بـ <code dir="ltr">de-DE</code> يختبر الجهاز.

## اختبارات جسر IStringLocalizer

<div dir="ltr">

```csharp
var localizer = new CustomStringLocalizerFactory(store).Create(typeof(HomeController));
localizer["Missing"].Value.Should().Be("[MISSING:Missing]");
localizer["Missing"].ResourceNotFound.Should().BeTrue();
localizer.GetAllStrings(includeParentCultures: false).Select(s => s.Name)
    .Should().NotContain("Missing");
```

</div>

غطِّ ربط اسم النوع، والتقاط الثقافة الدقيقة عند الإنشاء، وإعادة الفحص في كل بحث، وعلامة الغياب،
والتعداد الذي يستبعد الإدخالات المفقودة، والتنسيق المركب (<code dir="ltr">{0}</code>)، وتركيباً
بلا مخزن يفشل كخطأ تكوين لا كغياب.

الغلاف العام عابر. اختبره عبر DI (Dependency Injection) عندما يكون الاختبار عن التركيب؛ واختبر المصنع مباشرة عندما يكون
عن الربط.

## التعريب الزائف والمفاتيح المفقودة

يكشف التعريب الزائف السلاسل المضمّنة والتخطيطات الضيقة. استخدم مزوّد Pseudo وأكّد الشكل الحتمي:

<div dir="ltr">

```csharp
var pseudo = new PseudoMessageProvider(new PseudoLocalizationOptions {
    SourceTemplates = new Dictionary<string, string> { ["Title"] = "Bookstore" }
});
var localizer = new DefaultMessageLocalizer(
    pseudo, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "qps-ploc" });

var text = await localizer.LocalizeAsync(
    "Title", new Dictionary<string, object?>(), new CultureInfo("qps-ploc"));
text.Should().StartWith("[!! ").And.EndWith(" !!]");
```

</div>

مصبات المفاتيح المفقودة قابلة للاختبار بلا بنية تحتية. غطِّ إزالة التكرار حسب
<code dir="ltr">(key, culture)</code>، والسقف و<code dir="ltr">DroppedCount</code>، واحتواء مصب
يرمي، وسلوك الحالة المعطلة.

<div dir="ltr">

```csharp
var collector = new MissingKeyCollector(maxEntries: 10);
var localizer = new DefaultMessageLocalizer(
    provider, new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en", MissingKeySink = collector });

await localizer.LocalizeAsync("Absent", parameters, new CultureInfo("fr"));
collector.GetSnapshot().Should().ContainSingle();
collector.GetSnapshot()[0].HitCount.Should().Be(1);
```

</div>

## تأكيدات التكافؤ

محرك التكافؤ داخلي في أداة سطر الأوامر. اختبره كما يفعل المستودع: شغّل الأداة المبنية خارج
العملية مقابل نسخة تجهيز، وأكّد رمز الخروج ومخرجات الطرفية وتقرير JSON.

<div dir="ltr">

```csharp
var run = await RunToolAsync(fixture,
    ["--catalog", "catalogs", "--source", "src", "--default-culture", "en"]);

run.ExitCode.Should().Be(1);
run.Stdout.Should().Contain("[missing-key] Cart.Add [ar]");
```

</div>

تجهيزات مفيدة: نظيف (خروج <code dir="ltr">0</code>)، ومفتاح محذوف يُحل عبر الافتراضية
(<code dir="ltr">fallback</code>، خروج <code dir="ltr">0</code>)، ومفتاح محذوف بلا ارتداد
(<code dir="ltr">missing-key</code>، خروج <code dir="ltr">1</code>)، وصيغتان بقوالب مختلفة
(<code dir="ltr">template-drift</code>)، ومفتاح غائب من صيغة خدمة واحدة
(<code dir="ltr">format-gap</code>)، وكتالوج مشوّه (<code dir="ltr">catalog-error</code> مع سطر)،
واستبعاد يُسكت نتيجة، وإسقاط عنصر نائب (<code dir="ltr">placeholder-drift</code> مع المعرّف
المفقود).

تعمل فحوص نمط اللقطة لأن الإخراج حتمي. وحّد نهايات الأسطر وأبقِ مسارات التجهيز نسبية كي يبقى النص
الذهبي مستقراً.

## رحلات تبادل الذهاب والعودة

اختبر الثابت القوي مباشرةً: رحلة بلا تغيير متطابقة بايتياً.

1. انسخ كتالوجي المصدر والهدف إلى دليل مؤقت.
2. صدّر إلى PO (Portable Object) أو CSV أو ResX أو XLIFF (XML Localization Interchange File Format).
3. استورد بـ <code dir="ltr">DryRun = true</code>؛ وأكّد أن شيئاً لم يتغير.
4. استورد عادةً؛ وأكّد عدم وجود إدخالات <code dir="ltr">Updated</code> وبقاء بايتات الهدف دون
   تغيير.
5. غيّر سلسلة هدف واحدة، واستورد، وأكّد أن ذلك المفتاح وحده
   <code dir="ltr">Updated</code>.

واختبر حد الفشل أيضاً: مستند مشوّه يرمي استثناء الصيغة المحددة مع ملف وسطر، ولا يُعاد كتابة الملف
الهدف. والتشغيل التجريبي لا يكتب أبداً، حتى عند وجود تحديثات.

## اختبارات التشخيص

تستخدم تأكيدات الأنشطة مستمعاً في الذاكرة:

<div dir="ltr">

```csharp
var activities = new List<Activity>();
using var listener = new ActivityListener {
    ShouldListenTo = source => source.Name == LocalizationDiagnostics.SOURCE_NAME,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activities.Add,
};
ActivitySource.AddActivityListener(listener);

await localizer.LocalizeAsync("heading", parameters, new CultureInfo("fr-CA"));
activities.Single(a => a.OperationName == "localization.lookup")
    .GetTagItem("localization.culture").Should().Be("fr-CA");
```

</div>

أكّد أن نص القالب لا يظهر في أي وسم أبداً، وأن خطوات الارتداد أبناء للبحث، وأن البحث الفاشل لا
يسجّل إصابة أو غياباً. وللمقاييس، استخدم <code dir="ltr">MeterListener</code> ورشّح باسم الأداة.

## مثال عملي: مجموعة انحدار لرسالة جمع واحدة

يضيف فريق رسالة جمع. وتغطي مجموعة الاختبار أوضاع الفشل:

1. العدد المفرد بالإنجليزية يختار القالب المفرد.
2. العدد صفر بالعربية يختار فئة الصفر، لا المفرد.
3. المفتاح المفضل المفقود يرتد إلى المفتاح الآخر.
4. الزوج المفقود يعيد <code dir="ltr">null</code> ويبلّغ عن المفتاحين إلى المصب.
5. يُستعلم المزوّد بالثقافة المطلوبة أولاً، ثم بآبائها.
6. يعرض منسّق ICU (International Components for Unicode) <code dir="ltr">#</code> بالثقافة المطلوبة.
7. تحتوي تجهيزة التكافؤ المفتاح في كل ثقافة مخدومة وتنجح في مقارنة العناصر النائبة.

كل الاختبارات تستخدم ثقافات صريحة، ومزوّد تسجيل، ومنسّقاً حتمياً. ولا يعتمد أي منها على لغة الجهاز
أو على ترتيب الاختبارات.

## مآزق

**مأزق:** تأكيد السلسلة المعادة فقط في اختبار ارتداد. قد يخفي سلسلة استعلام خاطئة.

**مأزق:** نسيان <code dir="ltr">TryGetResourceSet</code> في مخزن وهمي. يستخدم الجسر المسبار
المتزامن.

**مأزق:** تسريب <code dir="ltr">CurrentUICulture</code> بين الاختبارات. أعدها في
<code dir="ltr">finally</code> وسلسل اختبارات الثقافة المحيطة.

**مأزق:** مقارنة رقم من <code dir="ltr">DefaultMessageFormatter</code> بسلسلة ثابتة. إنه يستخدم
<code dir="ltr">CurrentCulture</code> المحيطة.

**مأزق:** اختبار <code dir="ltr">IStringLocalizer</code> singleton عبر ثقافات. لقد التقط الثقافة عند
إنشائه؛ أنشئ مُعرِّباً جديداً للثقافة الجديدة.

**مأزق:** استخدام مخزن الإنتاج في اختبار وحدة. الوهمي الصغير أسرع ولا يحتاج قاعدة بيانات.

**مأزق:** التعامل مع مجموعة اختبارات وحدة خضراء كتغطية ترجمة. شغّل أداة التكافؤ أيضاً.

## إلى أين بعد ذلك

- [التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md) لفحص CI ومخطط التقرير.
- [الثقافات والارتداد](03-cultures-and-fallback.md) للسلسلة التي يجب أن تثبّتها الاختبارات.
- [التشخيص والرصد](14-diagnostics-and-observability.md) للأسماء التي يجب تأكيدها.

</div>
