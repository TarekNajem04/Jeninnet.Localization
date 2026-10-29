<div dir="rtl">

# دليل الرصد (observability cookbook)

يشرح [دليل التشخيص](../02-guides/14-diagnostics-and-observability.md) الإشارات التي تصدرها
المكتبة. وتحوّل هذه الصفحة تلك الإشارات إلى إعداد مشغّل: ربط OpenTelemetry، ورسم مجمّع، ولوحات
معلومات، ومرشحات تنبيه، وتدفق تحقيق.

لا تملك المكتبة تبعية على حزمة OpenTelemetry. كل ما هنا تكوين من جهة المضيف. وأسماء الأدوات
والأنشطة مستقرة؛ والمُصدِّر والخلفية تختارهما أنت.

## ما الذي يُجمع

| الإشارة | النوع | الوسوم / التسميات | الاستخدام |
|---|---|---|---|
| <code dir="ltr">localization.lookup</code> | نشاط | <code dir="ltr">localization.key</code> و<code dir="ltr">localization.culture</code> و<code dir="ltr">localization.found</code> | تشخيص كل مفتاح وسلسلة الارتداد |
| <code dir="ltr">localization.fallback.step</code> | نشاط فرعي | <code dir="ltr">localization.culture</code> و<code dir="ltr">localization.found</code> | أي ثقافة في السلسلة أجابت أو غابت |
| <code dir="ltr">localization.lookup.hits</code> | عدّاد | <code dir="ltr">localization.culture</code> | حجم الإصابات المجمع |
| <code dir="ltr">localization.lookup.misses</code> | عدّاد | <code dir="ltr">localization.culture</code> | فجوات التغطية وضغط الارتداد |
| <code dir="ltr">localization.lookup.duration</code> | مدرج تكراري (ms) | <code dir="ltr">localization.culture</code> | زمن البحث والذيل |
| <code dir="ltr">localization.fallback.steps</code> | عدّاد | <code dir="ltr">localization.culture</code> | استعلامات المزوّد لكل بحث |
| <code dir="ltr">localization.translation.requests</code> | عدّاد | <code dir="ltr">localization.culture</code> | حجم طلبات Azure الصادرة |
| <code dir="ltr">localization.translation.cache.hits</code> | عدّاد | <code dir="ltr">localization.culture</code> | فعالية ذاكرة Azure |
| <code dir="ltr">localization.translation.cache.misses</code> | عدّاد | <code dir="ltr">localization.culture</code> | غيابات ذاكرة Azure |
| <code dir="ltr">localization.translation.duration</code> | مدرج تكراري (ms) | <code dir="ltr">localization.culture</code> | زمن طلب Azure |
| <code dir="ltr">jeninnet-localization-provider</code> | فحص صحة | الحالة + البيانات | إتاحة المزوّد ونتيجة المسبار |
| <code dir="ltr">jeninnet-localization-store</code> | فحص صحة | الحالة + البيانات | إتاحة المخزن والثقافات المخدومة |
| مصب المفاتيح المفقودة | سجل / لقطة | المفتاح والثقافة وموضع النداء | جمع الفجوات في حلقة مغلقة |

تظهر المفاتيح في التتبعات فقط. وتحمل المقاييس الثقافة فقط، وهذا يبقي التعددية محدودة ويُبقي
المحتوى خارج خلفية المقاييس. ولا يصدر الإطار نص قالب أبداً.

## ربط OpenTelemetry الأدنى

أضف حزم SDK ومُصدِّر OpenTelemetry إلى المضيف؛ فالمكتبة لا تشير إليها:

<div dir="ltr">

```csharp
using Jeninnet.Localization.Abstractions.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("checkout"))
    .WithTracing(tracing => tracing
        .AddSource(LocalizationDiagnostics.SOURCE_NAME)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(LocalizationDiagnostics.SOURCE_NAME)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

</div>

فضّل الثوابت (<code dir="ltr">LocalizationDiagnostics.SOURCE_NAME</code> و
<code dir="ltr">LOOKUP_ACTIVITY_NAME</code> و
<code dir="ltr">FALLBACK_STEP_ACTIVITY_NAME</code> وثوابت أسماء الأدوات) على النصوص الحرفية عندما
يشير المضيف إلى Abstractions. والأسماء الحرفية مستقرة أيضاً.

أضف فحوص الصحة بعد تسجيل المزوّد والمخزن:

<div dir="ltr">

```csharp
services.AddLocalizationHealthChecks(options => {
    options.ProviderProbeKey = "App.Title";
    options.ProviderProbeCulture = "en";
});

app.MapHealthChecks("/health");
```

</div>

يسجّل فحص المزوّد فقط عند تكوين مفتاح مسبار. ويسجّل فحص المخزن فقط عندما يكون
<code dir="ltr">ILocalizationStore</code> مسجّلاً بالفعل.

## رسم المجمّع

هذه نقطة بداية، لا أثر نشر مدعوماً. استبدل نقطة نهاية الخلفية وخيارات المُصدِّر بما توفره منصتك.

<div dir="ltr">

```yaml
receivers:
  otlp:
    protocols:
      grpc:
      http:

processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 256
  batch:

exporters:
  prometheus:
    endpoint: "0.0.0.0:9464"
  otlp/backend:
    endpoint: "backend:4317"
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp/backend]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [prometheus]
```

</div>

لإعداد أصغر، أرسل الإشارتين مباشرةً إلى الخلفية وتجاوز المجمّع. والمجمّع مفيد عندما تحتاج إلى
تجميع أو أخذ عينات أو تنقيح أو نقطة خروج واحدة.

## لوحات المعلومات

تفترض الاستعلامات أدناه التطبيع المعتاد للأسماء في مُصدِّر Prometheus لـ OpenTelemetry .NET: تتحول
النقاط إلى شرطات سفلية وتكتسب العدادات لاحقة <code dir="ltr">_total</code>. افحص الأسماء الفعلية
في خلفيتك قبل نسخها إلى التنبيهات.

| اللوحة | نقطة بداية PromQL | لماذا تهم |
|---|---|---|
| نسبة الإصابة حسب الثقافة | <code dir="ltr">sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) / (sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) + sum by (localization_culture) (rate(localization_lookup_misses_total[5m])))</code> | التغطية وضغط الارتداد |
| معدل الغياب حسب الثقافة | <code dir="ltr">sum by (localization_culture) (rate(localization_lookup_misses_total[5m])) / (sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) + sum by (localization_culture) (rate(localization_lookup_misses_total[5m])))</code> | البيانات نفسها، وأسهل للتنبيه |
| p95 لزمن البحث | <code dir="ltr">histogram_quantile(0.95, sum by (le, localization_culture) (rate(localization_lookup_duration_bucket[5m])))</code> | زمن الذيل؛ والمدة بالمللي ثانية |
| خطوات الارتداد لكل بحث | <code dir="ltr">sum by (localization_culture) (rate(localization_fallback_steps_total[5m])) / sum by (localization_culture) (rate(localization_lookup_hits_total[5m]) + rate(localization_lookup_misses_total[5m]))</code> | قيم فوق 1 تُظهر كتالوجات ناقصة أو غيابات |
| نسبة إصابة ذاكرة Azure | <code dir="ltr">sum by (localization_culture) (rate(localization_translation_cache_hits_total[5m])) / (sum by (localization_culture) (rate(localization_translation_cache_hits_total[5m])) + sum by (localization_culture) (rate(localization_translation_cache_misses_total[5m])))</code> | هل تكسب ذاكرة الترجمة كلفتها |
| p95 لزمن الترجمة | <code dir="ltr">histogram_quantile(0.95, sum by (le, localization_culture) (rate(localization_translation_duration_bucket[5m])))</code> | زمن الخدمة الخارجية |
| حالة الصحة | سلسلة الحالة في مُصدِّر فحص الصحة لديك | إتاحة المزوّد/المخزن |

تعدّ <code dir="ltr">localization.fallback.steps</code> استعلامات المزوّد، لذا فالإصابة الدقيقة
العادية يجب أن تكون <code dir="ltr">1</code> لكل بحث. والقيم فوق <code dir="ltr">1</code> تعني
أن الثقافة المطلوبة لم تُجب: <code dir="ltr">2</code> خطوة إضافية واحدة (أب أو الافتراضية)، و
<code dir="ltr">3</code> سلسلة آباء أعمق أو استعلام افتراضي مكرر. والشكل الدقيق يعتمد على الثقافة
المطلوبة والافتراضية المكوَّنة.

لا تُحتسب الأعطال كإصابات أو غيابات. استخدم خلفية التتبعات للاستعلام عن الأنشطة ذات
<code dir="ltr">ActivityStatusCode.Error</code>، أو استخدم أدوات HTTP/قاعدة البيانات للتبعية
الفاشلة. وتبقى المقاييس نظيفة عمداً كي لا يُبلَّغ عن انقطاع كفجوة تغطية.

## مرشحات التنبيه

هذه نقاط بداية، لا عتبات عالمية. أسّس خط أساس لها مقابل حركتك واكتمال كتالوجك.

| التنبيه | شرط البداية | التفسير |
|---|---|---|
| انحدار نسبة الغياب | نسبة الغياب حسب الثقافة فوق خط أساسها لسبعة أيام لمدة 15 دقيقة، أو فوق 5% في ثقافة يُتوقع اكتمالها | انحدر إدخال كتالوج أو نشر أو مسار مزوّد |
| انحدار عمق الارتداد | خطوات الارتداد لكل بحث فوق 1.5 لمدة 15 دقيقة | ثقافة تفتقد مفاتيح وترتد أكثر |
| زمن البحث | p95 للمدة فوق ميزانية زمن الخدمة لمدة 15 دقيقة | إدخال/إخراج المزوّد أو عمق الارتداد أو كتالوج بارد |
| زمن الترجمة | p95 لمدة الترجمة فوق النطاق المتوقع للمزوّد لمدة 15 دقيقة | تدهور خدمة خارجية أو شبكة |
| انحدار ذاكرة الترجمة | نسبة إصابة الذاكرة تهبط بشدة مقابل خط أساسها | تغيّر حجم الذاكرة أو دوران المفاتيح أو سلوك إعادة التشغيل |
| صحة المزوّد/المخزن | أحد فحصي الصحة <code dir="ltr">Unhealthy</code> أو يبقى <code dir="ltr">Degraded</code> لمدة 10 دقائق | تبعية غير متاحة، أو مفتاح/ثقافة المسبار خاطئان |
| ارتفاع سجل المفاتيح المفقودة | معدل الحدث <code dir="ltr">1001</code> يرتفع بشدة | مفاتيح جديدة غير مترجمة، أو ثقافة خاطئة، أو كتالوج مفقود |
| إسقاطات جامع المفاتيح المفقودة | <code dir="ltr">DroppedCount</code> يرتفع | حجم الغياب يتجاوز سقف الجامع؛ ارفعه أو أصلح المصدر |

فحص صحة مزوّد <code dir="ltr">Degraded</code> بمفتاح مسبار غائب عمداً خلل في المسبار، لا انقطاع
مزوّد. استخدم مفتاحاً يُتوقع أن يُحل في ثقافة المسبار.

## تدفق التحقيق

1. **ابدأ بمعدل الغياب، لا بعدد الإصابات الخام.** انخفاض الحركة يخفض الإصابات والغيابات؛
   وانحدار التغطية يرفع النسبة. فصّلها حسب
   <code dir="ltr">localization_culture</code>.
2. **افحص عمق الارتداد.** إذا ارتفعت الخطوات لكل بحث مع الغيابات، فالثقافة المطلوبة ناقصة. وإذا
   ارتفعت الغيابات دون تغير في الخطوات، فالمزوّد نفسه يعيد لا شيء.
3. **افتح تتبعاً لمفتاح/ثقافة واحدة.** يُظهر
   <code dir="ltr">localization.lookup</code> المفتاح والثقافة ووسم
   <code dir="ltr">found</code>؛ ويظهر كل طفل
   <code dir="ltr">localization.fallback.step</code> أي ثقافة غابت أو أجابت. والتتبع أسرع طريق
   للتمييز بين الارتداد وفشل المزوّد.
4. **افحص مصب المفاتيح المفقودة.** يعطي الجامع أو الحدث
   <code dir="ltr">1001</code> المفتاح والثقافة وموضع النداء. وبعد الإصلاح، امسح الجامع أو أعد
   ضبطه وأكّد هبوط المعدل.
5. **أكّد بأداة التكافؤ.** إذا قال التتبع إن المفتاح مفقود من الثقافة الافتراضية، فشغّل فحص
   التكافؤ وأضف إدخال الكتالوج؛ لا تغيّر تكوين الارتداد لإخفائه.
6. **للزمن، افصل زمن المزوّد عن زمن خط الأنابيب.** بحث بخطوة واحدة وزمن مرتفع يشير إلى المزوّد.
   وبحث بعدة خطوات يشير إلى عمق الارتداد أو اكتمال الكتالوج. وزمن الترجمة كلفة تبعية، لا كلفة
   Core.
7. **للأعطال، استخدم حالة خطأ التتبع وأدوات التبعية.** البحث الفاشل لا إصابة له ولا غياب ولا مدة؛
   والاستثناء ينتشر. لا تنبّه على مقياس مفقود بينما يقول التتبع إن التبعية رمت.

## التعددية وأخذ العينات والكلفة

- **تبقى المفاتيح في التتبعات.** لا تضف وسوم مفتاح أو مستخدم أو مستأجر أو معرّف طلب إلى المقاييس.
  فالمقاييس المدمجة تحمل <code dir="ltr">localization.culture</code> فقط، لذا فالتعددية محدودة
  بالثقافات المدعومة.
- **تعددية الثقافة محدودة بحكم التصميم.** إذا قبل مضيف وسوم ثقافة عشوائية، فتحقق منها مقابل
  المجموعة المدعومة قبل البحث؛ وإلا فقد ينمو وسم الثقافة.
- **جمع المفاتيح المفقودة مسقوف.** يُسقط <code dir="ltr">MissingKeyCollector</code> بعد
  <code dir="ltr">maxEntries</code> ويزيد <code dir="ltr">DroppedCount</code>؛ راقب الاثنين.
  لا ترمّز بيانات شخصية في المفاتيح، لأن المفاتيح تُوسم في التتبعات وقد تظهر في السجلات.
- **أخذ العينات يؤثر في التتبعات، لا العدادات.** النشاط المستبعد بالعينة لا يُنشأ؛ وتظل المقاييس
  تسجّل الإصابات والغيابات والمدة وخطوات الارتداد. استخدم المقاييس للتنبيهات المجمعة والتتبعات
  لتشخيص كل مفتاح.
- **المراقبون يدفعون كلف التسجيل.** مع مستمعين مرتبطين، يكون تسجيل الأنشطة والمقاييس أغلى من مسار
  عدم الأثر المحمي بالمستمع؛ واللقطة المقاسة في
  [دليل أداء](07-performance-evidence.md). خصص لها ميزانية عند تفعيل تتبع عالي التعددية في
  الإنتاج.

## الأمان والخصوصية

- لا يوسم الإطار نص القالب أبداً. أبقِ الأمر كذلك في الأدوات والسجلات المخصصة.
- المفاتيح معرّفات مطوّرين، لا مكان آمن لمدخلات المستخدم. لا تبنِ مفاتيح من أسماء أو بريد
  إلكتروني أو بيانات حساب.
- تحمل أوصاف فحوص الصحة وبياناتها معرّفات وأعداداً ومدداً وأسماء أنواع استثناءات، لا قوالب أو
  رسائل استثناءات. راجع الفحوص المخصصة قبل كشف نقطة النهاية.
- يسجّل مسجّل المفاتيح المفقودة المفتاح والثقافة وموضع النداء. تعامل معها كبيانات وصفية تشغيلية
  وطبّق ضوابط الوصول نفسها التي تطبقها على سجلات التطبيق الأخرى.

## توثيق ذو صلة

- [التشخيص والرصد](../02-guides/14-diagnostics-and-observability.md)
- [مرجع التشخيص](../03-reference/06-diagnostics-reference.md)
- [الأداء](03-performance.md)
- [دليل أداء](07-performance-evidence.md)
- [استكشاف الأخطاء](04-troubleshooting.md)
- [الأمان](02-security.md)

</div>
