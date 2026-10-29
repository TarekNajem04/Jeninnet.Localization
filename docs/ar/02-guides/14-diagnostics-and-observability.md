<div dir="rtl">

# التشخيص والرصد (diagnostics and observability)

إخفاقات التعريب صامتة افتراضياً. يعيد المفتاح المفقود <code dir="ltr">null</code>، ويحل الارتداد
اللغة الخاطئة، ويختفي عنصر نائب للمترجم. وعندما يبلّغ مستخدم عن نص خاطئ، يكون الطلب قد مضى منذ
وقت طويل. يوجد الرصد لجعل تلك الإخفاقات مرئية وهي ما زالت رخيصة الإصلاح.

يكشف الإطار ثلاثة أسطح متكاملة:

- **التتبعات والمقاييس** عبر <code dir="ltr">ActivitySource</code> و
  <code dir="ltr">Meter</code> في BCL (Base Class Library)، باسم <code dir="ltr">Jeninnet.Localization</code>.
- **الإبلاغ عن المفاتيح المفقودة** عبر <code dir="ltr">IMissingKeySink</code> الاختياري.
- **فحوص الصحة** للمزوّد والمخزن، في تكامل التشخيص.

لا توجد تبعية على حزمة OpenTelemetry. والخطافات محمية بمستمع، لذا لا يدفع مضيف بلا تشخيص إلا فحص
حماية واحداً، ويتصرف كما كان سيتصرف بدونه تماماً.

## التتبعات

يصدر <code dir="ltr">LocalizationDiagnostics.Source</code> اسمي نشاط:

- <code dir="ltr">localization.lookup</code> — نشاط واحد لكل نداء مُعرِّب.
- <code dir="ltr">localization.fallback.step</code> — نشاط فرعي واحد لكل استعلام مزوّد على طول
  سلسلة الارتداد المرجعية.

يحمل النشاط الجذر:

| الوسم | المعنى |
|---|---|
| <code dir="ltr">localization.key</code> | مفتاح البحث. في بحث الجمع، يرسّخ المفتاح المفرد النشاط. وفي بحث السياق، قد يحتوي المفتاح المركب فاصل EOT (End of Transmission). |
| <code dir="ltr">localization.culture</code> | الثقافة المطلوبة. |
| <code dir="ltr">localization.found</code> | هل حُلّ قالب. يُعيَّن عند الاكتمال؛ ويغيب في بحث فاشل. |

يحمل كل خطوة ارتداد الوسمين <code dir="ltr">localization.culture</code> و
<code dir="ltr">localization.found</code>. وأب الخطوة هو نشاط البحث، لذا يُظهر التتبع التسلسل
الدقيق <code dir="ltr">de-CH</code> ثم <code dir="ltr">de</code> ثم <code dir="ltr">en</code>
دون تخمين.

البحث الذي يرمي يترك النشاط في حالة خطأ (<code dir="ltr">ActivityStatusCode.Error</code>) ولا
يسجّل إصابة أو غياباً. هذا يطابق عقد وقت التشغيل: فشل المزوّد ليس غياباً، والاستثناء ينتشر.

لا يُوسم نص القالب أبداً. المفاتيح والثقافات معرّفات مطوّرين؛ أما القوالب فقد تحتوي بيانات شخصية
أو أسراراً. لا ترمّز بيانات شخصية في المفاتيح أيضاً، لأن المفاتيح تُوسم.

يعمل أخذ العينات كما هو متوقع: عندما يستبعد مستمع نشاطاً، لا يُنشأ نشاط وتظل عمليات البحث ناجحة.
ولا يفرض خط الأنابيب أخذ العينات.

## المقاييس

يكشف <code dir="ltr">LocalizationDiagnostics.Meter</code> خمس أدوات قياس لخط الأنابيب وأربعاً
لترجمة Azure:

| الأداة | الوحدة | المعنى |
|---|---|---|
| <code dir="ltr">localization.lookup.hits</code> | <code dir="ltr">{lookup}</code> | عمليات بحث حلّت قالباً. |
| <code dir="ltr">localization.lookup.misses</code> | <code dir="ltr">{lookup}</code> | عمليات بحث لم تحل شيئاً. |
| <code dir="ltr">localization.lookup.duration</code> | <code dir="ltr">ms</code> | مدة بحث مكتمل. |
| <code dir="ltr">localization.fallback.steps</code> | <code dir="ltr">{step}</code> | استعلامات المزوّد على سلسلة الارتداد. |
| <code dir="ltr">localization.translation.requests</code> | <code dir="ltr">{request}</code> | طلبات ترجمة Azure الصادرة. |
| <code dir="ltr">localization.translation.cache.hits</code> | <code dir="ltr">{lookup}</code> | إصابات ذاكرة Azure المؤقتة. |
| <code dir="ltr">localization.translation.cache.misses</code> | <code dir="ltr">{lookup}</code> | غيابات ذاكرة Azure المؤقتة. |
| <code dir="ltr">localization.translation.duration</code> | <code dir="ltr">ms</code> | مدة طلب Azure الصادر. |

تُوسم المقاييس بالثقافة فقط. لا تظهر المفاتيح في وسوم المقاييس أبداً؛ وهذا يبقي التعددية محدودة
ويمنع تسرب المحتوى إلى خلفية مقاييس. وتصدر أدوات الترجمة من مزوّد Azure، لا من Core.

تعدّ <code dir="ltr">localization.fallback.steps</code> استعلامات المزوّد، لا الثقافات المتمايزة.
فالغياب الذي يستعلم عن الثقافة المطلوبة ثم عن الثقافة الافتراضية يساهم بخطوتين. ويظهر هنا
استعلام الثقافة الافتراضية المكرر عندما تتساوى الافتراضية مع الثقافة المطلوبة.

البحث الفاشل لا يسجّل إصابة ولا غياباً ولا مدة. ويحمل النشاط الخطأ؛ وتبقى العدادات نظيفة كي لا
يُخلط بين انقطاع المزوّد وفجوة تغطية.

## تفعيل التتبعات والمقاييس

يشترك المستهلكون من جهة SDK بالأسماء الثابتة:

<div dir="ltr">

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Jeninnet.Localization"))
    .WithMetrics(metrics => metrics.AddMeter("Jeninnet.Localization"));
```

</div>

الأسماء الحرفية متاحة أيضاً كـ <code dir="ltr">LocalizationDiagnostics.SOURCE_NAME</code>
و<code dir="ltr">LOOKUP_ACTIVITY_NAME</code> و
<code dir="ltr">FALLBACK_STEP_ACTIVITY_NAME</code> وثوابت أسماء الأدوات. فضّل الثوابت عندما يشير
المشروع المستهلك إلى Abstractions.

بلا مستمع، تكون <code dir="ltr">ActivitySource.HasListeners()</code> خاطئة ولا يُنشأ نشاط. وبلا
مستمع مقياس، تكون راية <code dir="ltr">Enabled</code> لكل أداة خاطئة ولا يُكتب قياس. ولهذا تكون
الخطافات آمنة للترك في مكتبة.

يوجد ربط جهة التشغيل ورسم المجمّع واستعلامات لوحة المعلومات ومرشحات التنبيه في
[دليل الرصد](../04-operations/08-observability-cookbook.md).

## المفاتيح المفقودة

يبلّغ المُعرِّب عن غياب فقط بعد أن تحل سلسلة الارتداد كاملة لا شيء. ولا تبلّغ خطوات الارتداد
الوسيطة أبداً، لذا فمفتاح يُحل عبر ثقافة أب ليس غياباً. والمصب <code dir="ltr">null</code> هو
الافتراضي ويكلّف فحص null واحداً.

يجمع <code dir="ltr">MissingKeyCollector</code> حسب <code dir="ltr">(key, culture)</code> مع عدّاد
إصابات وأول موضع نداء. وهو محدود: <code dir="ltr">maxEntries</code> افتراضياً 1000، والغيابات
بعد السقف تزيد <code dir="ltr">DroppedCount</code> بدلاً من زيادة الذاكرة. وتعيد
<code dir="ltr">GetSnapshot()</code> الإدخالات بترتيب ترتيبي؛ وتعيد
<code dir="ltr">Clear()</code> تعيين الإدخالات وعدّاد المُسقَط معاً.

يسجّل <code dir="ltr">LoggerMissingKeySink</code> تحذيراً منظماً واحداً لكل غياب، بمعرّف حدث
<code dir="ltr">1001</code> والرسالة
<code dir="ltr">Localization miss for key '{Key}' in culture '{Culture}' at '{CallSite}'.</code>.
ولا تُسجَّل إلا المعرّفات؛ ولا يعبر نص القالب الحدود أبداً.

يوزّع <code dir="ltr">CompositeMissingKeySink</code> على عدة مصبات بالترتيب، فيستطيع جامع ومسجّل
مشاركة مُعرِّب واحد:

<div dir="ltr">

```csharp
var collector = new MissingKeyCollector(maxEntries: 500);

services.AddMemoryLocalization(messages);
services.AddLocalization(options => {
    options.DefaultCulture = "en";
    options.MissingKeySink = new CompositeMissingKeySink(
        collector,
        new LoggerMissingKeySink(loggerFactory.CreateLogger("Localization")));
});
```

</div>

إخفاقات المصبات محتواة. المصب الذي يرمي لا يكسر البحث، بحكم التصميم: يجب ألا تصبح مراقبات
التشخيص تبعية إنتاجية أبداً.

## أحداث المزوّد وتسجيل تعريب الطلب

المزوّدون الذين يعلنون <code dir="ltr">IProviderEventSource</code> يرفعون:

- <code dir="ltr">TranslationAcquired</code> مع القوالب المحلولة والثقافة الهدف.
- <code dir="ltr">AcquisitionFailed</code> مع الثقافة والاستثناء.

ترفع مزوّدات الملفات هذه الأحداث للتحميل الأولي وإعادة التحميل. ويرفعها Azure للاكتساب الخارجي؛
ولا ترفع إصابات الذاكرة المؤقتة وذاكرة الترجمة شيئاً. وإلغاء المستدعي لا يرفع حدث فشل.

يسجّل جسر تعريب الطلب تحذيراً واحداً عندما يكون المخزن غير متاح وتتراجع المجموعة المخدومة إلى
الثقافة الافتراضية وحدها. ويستخدم معرّف الحدث <code dir="ltr">1</code> واسم الحدث
<code dir="ltr">LocalizationStoreUnreachable</code>. وسطر السجل ذلك هو أسرع تفسير لعبارة "الموقع
يتجاهل <code dir="ltr">Accept-Language</code> في هذه البيئة".

## فحوص الصحة

تسجّل <code dir="ltr">AddLocalizationHealthChecks</code> حتى فحصين:

- <code dir="ltr">jeninnet-localization-provider</code> — يحل مفتاح مسبار صريحاً
  <code dir="ltr">ProviderProbeKey</code> لثقافة
  <code dir="ltr">ProviderProbeCulture</code> عبر
  <code dir="ltr">IMessageProvider</code> المرجعي. والمحلول
  <code dir="ltr">Healthy</code>؛ والمتاح لكن غير المخدوم
  <code dir="ltr">Degraded</code>؛ والذي يرمي <code dir="ltr">Unhealthy</code> مع نوع الاستثناء
  في البيانات.
- <code dir="ltr">jeninnet-localization-store</code> — يستكشف
  <code dir="ltr">ILocalizationStore.GetSupportedCulturesAsync</code>. والمتاح
  <code dir="ltr">Healthy</code>؛ والمتاح بصفر ثقافات
  <code dir="ltr">Degraded</code>؛ والذي يرمي <code dir="ltr">Unhealthy</code>.

<div dir="ltr">

```csharp
services.AddLocalizationHealthChecks(options => {
    options.ProviderProbeKey = "App.Title";
    options.ProviderProbeCulture = "en";
});

app.MapHealthChecks("/health");
```

</div>

يسجّل فحص المزوّد فقط عند تكوين مفتاح مسبار. ولا يفرّغ المسبار مفاتيح غير معرّفة ولا يعيد نص
قالب. ويسجّل فحص المخزن فقط عندما يكون <code dir="ltr">ILocalizationStore</code> مسجّلاً بالفعل،
فنادِ التركيب بعد تسجيل المخزن.

تحمل أوصاف فحوص الصحة وبياناتها معرّفات وأعداداً ومدداً وأسماء أنواع استثناءات. ولا تحمل قوالب أو
رسائل استثناءات. وهذا يجعل نقطة النهاية آمنة للكشف للمشغّلين.

## مثال عملي: تشخيص غياب في الإنتاج

يبلّغ مستخدم عن زر دفع غير مترجم. يُظهر التتبع عملية
<code dir="ltr">localization.lookup</code> للمفتاح <code dir="ltr">Checkout.Pay</code> في
<code dir="ltr">fr-CA</code> مع <code dir="ltr">localization.found=false</code>، وخطوات ارتداد
لـ <code dir="ltr">fr-CA</code> و<code dir="ltr">fr</code> والافتراضية
<code dir="ltr">en</code>، وكلها غيابات.

ويُظهر جامع المفاتيح المفقودة، المجموع من نقطة تشخيص في التدريج، المفتاح والثقافة نفسيهما مع موضع
نداء في <code dir="ltr">CheckoutController</code>. وهذا يستبعد انقطاع المزوّد ويشير إلى إدخال
<code dir="ltr">en</code> مفقود. وتؤكد أداة التكافؤ أن
<code dir="ltr">Checkout.Pay</code> مفقود من الكتالوج الإنجليزي، لذا فالإصلاح إدخال كتالوج، لا
تكوين ارتداد.

وتُظهر لوحة مقاييس ارتفاع <code dir="ltr">localization.lookup.misses</code> لـ
<code dir="ltr">fr-CA</code>؛ ويشرح التتبع لماذا لا تستطيع السلسلة التعافي.

## مآزق

**مأزق:** وسم القوالب أو قيم الوسائط. الإطار لا يفعل ذلك أبداً؛ ويجب ألا تفعله أدوات القياس
المخصصة أيضاً. المفاتيح والثقافات كافية.

**مأزق:** افتراض أن المقاييس تحمل المفاتيح. إنها لا تحملها. استخدم التتبعات لتشخيص مفتاح بعينه
والمقاييس للمعدلات المجمعة.

**مأزق:** التعامل مع بحث فاشل كغياب. إنه لا يسجّل إصابة أو غياباً؛ والنشاط في حالة خطأ والاستثناء
ينتشر.

**مأزق:** تسجيل فحص صحة المخزن قبل المخزن. ببساطة لا يُسجَّل الفحص؛ ولا يوجد خطأ عند بدء التشغيل.

**مأزق:** توجيه مسبار المزوّد إلى مفتاح غائب عمداً. فتصبح نتيجة
<code dir="ltr">Degraded</code> دليلاً على أن المسبار خاطئ، لا المزوّد.

**مأزق:** توقع <code dir="ltr">AcquisitionFailed</code> لإلغاء المستدعي. الإلغاء ليس فشل مزوّد ولا
يرفع حدثاً.

**مأزق:** كشف بيانات فحص الصحة دون مراجعتها. يبقي الإطار القوالب خارجها، لكن فحوصك المخصصة قد لا
تفعل ذلك.

## إلى أين بعد ذلك

- [دليل الرصد](../04-operations/08-observability-cookbook.md) لوصفات المجمّع ولوحة المعلومات
  والتنبيه.
- [مرجع التشخيص](../03-reference/06-diagnostics-reference.md) لكل اسم ومعرّف وخطورة في مكان
  واحد.
- [استكشاف الأخطاء](../04-operations/04-troubleshooting.md) للتشخيص القائم على الأعراض.
- [اختبار التعريب](15-testing-your-localization.md) لتأكيدات الأنشطة والمقاييس.
- [الترجمة الآلية](12-machine-translation.md) لأدوات القياس الخاصة بالترجمة.

</div>
