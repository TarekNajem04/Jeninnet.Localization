<div dir="rtl">

# مرجع التشخيص (diagnostics reference)

يجمع هذا الفصل أسماء الرصد (observability) وتشخيص البناء (build diagnostics) المستقرة في
مكان واحد. الأسماء عقود: تغيير معرّف أو اسم نشاط أو اسم أداة قياس أو وسم مقياس تغيير كاسر
للوحات المعلومات والتنبيهات والكبت.

## تشخيصات Roslyn

### المولّد: Jeninnet.Localization.Generators.Keys

الستة كلها أخطاء بناء مع ملف وسطر عندما يوفرهما المدخل. ولا يوجد تخطٍّ صامت.

| المعرّف | المعنى | الإصلاح |
|---|---|---|
| <code dir="ltr">JLNKEY001</code> | الكتالوج ليس JSON سليم الشكل، أو الجذر ليس كائن JSON. | صحح صيغة JSON أو شكل الجذر. |
| <code dir="ltr">JLNKEY002</code> | إدخال لا يربط مفتاحاً نصياً بقالب نصي. | استخدم مفاتيح نصية وقيماً نصية فقط. |
| <code dir="ltr">JLNKEY003</code> | المفتاح نفسه يتكرر أكثر من مرة في ملف كتالوج واحد. | احذف التكرار؛ والرسالة تسمّي السطر الأول. |
| <code dir="ltr">JLNKEY004</code> | مفتاحان متمايزان يُعقَّمان إلى معرّف C# نفسه. | أعد تسمية أحد المفتاحين أو غيّر الحد الذي يسبب التصادم. |
| <code dir="ltr">JLNKEY005</code> | مفتاح لا يحتوي محارف معرّف. | أعد تسمية المفتاح ليشمل حرفاً أو رقماً أو شرطة سفلية. |
| <code dir="ltr">JLNKEY006</code> | مساحة الأسماء المكوَّنة ليست معرّف C# منقطاً صالحاً. | صحح <code dir="ltr">LocalizationKeysNamespace</code> أو احذف التجاوز. |

### المحلل: Jeninnet.Localization.Analyzers.Localizability

| المعرّف | الخطورة الافتراضية | المعنى | الإصلاح |
|---|---|---|---|
| <code dir="ltr">JLNLOC001</code> | مخفية | سلسلة موجهة للمستخدم ومضمّنة مباشرة خارج خط أنابيب الرسائل. | انقل النص إلى كتالوج وحلّه عبر <code dir="ltr">IMessageLocalizer</code>؛ وعندما يكون النص الحرفي مقصوداً، اكتمه مع تبرير. |

يمكن رفع <code dir="ltr">JLNLOC001</code> إلى تحذير أو خطأ عبر
<code dir="ltr">LocalizabilitySeverity</code> (المعلنة مرئية بـ
<code dir="ltr">CompilerVisibleProperty</code>) أو
<code dir="ltr">dotnet_diagnostic.JLNLOC001.severity</code>. ويفوز
<code dir="ltr">.editorconfig</code> عند التعارض. واكتمه بـ
<code dir="ltr">#pragma warning disable JLNLOC001</code> أو
<code dir="ltr">[SuppressMessage]</code> أو قسم مجلد.

يُقدَّم إصلاح الكود فقط عندما يطابق النص الحرفي تماماً ثابت
<code dir="ltr">const string</code> في نوع ينتهي اسمه بـ <code dir="ltr">Keys</code>. ويستبدل
النص الحرفي بمرجع ثابت مؤهل بالكامل. ولا يوجد موفّر إصلاح الكل ولا استخراج تلقائي إلى كتالوج.

### نتائج أداة التكافؤ

أداة التكافؤ ليست محلل Roslyn، لذا فهذه أنواع نتائج لا معرّفات تشخيص. وهي سلاسل مستقرة في تقرير
JSON:

| النوع | المعنى |
|---|---|
| <code dir="ltr">missing-key</code> | مفتاح مشار إليه غائب من ثقافة وسلسلة ارتدادها. |
| <code dir="ltr">unused-key</code> | مفتاح كتالوج لا يُشار إليه في المصدر. |
| <code dir="ltr">template-drift</code> | ثقافة/مفتاح واحد لهما قوالب مختلفة عبر الصيغ. |
| <code dir="ltr">format-gap</code> | مفتاح مخدوم في صيغة وغائب من صيغة خدمة أخرى. |
| <code dir="ltr">placeholder-drift</code> | مجموعة العناصر النائبة لثقافة تختلف عن الثقافة المرجعية. |
| <code dir="ltr">unresolved-reference</code> | مرجع <code dir="ltr">*Keys</code> بلا تعريف ثابت. |
| <code dir="ltr">definition-conflict</code> | اسم ثابت واحد له قيم مختلفة. |
| <code dir="ltr">catalog-error</code> | كتالوج لا يمكن قراءته أو تحليله. |

تخرج الأداة <code dir="ltr">0</code> نظيفاً، و<code dir="ltr">1</code> مع نتائج، و
<code dir="ltr">2</code> عند أخطاء الاستخدام أو البيئة. وليس لها مُصدر SARIF.

## التتبعات

اسم المصدر: <code dir="ltr">Jeninnet.Localization</code>. النوع:
<code dir="ltr">System.Diagnostics.ActivitySource</code>. ولا تُشار إلى حزمة OpenTelemetry.

| اسم النشاط | الأب | الوسوم | المعنى |
|---|---|---|---|
| <code dir="ltr">localization.lookup</code> | النشاط الحالي | <code dir="ltr">localization.key</code> و<code dir="ltr">localization.culture</code> و<code dir="ltr">localization.found</code> | نداء مُعرِّب واحد. |
| <code dir="ltr">localization.fallback.step</code> | نشاط البحث | <code dir="ltr">localization.culture</code> و<code dir="ltr">localization.found</code> | استعلام مزوّد واحد على سلسلة الارتداد. |

دلالات الوسوم:

- <code dir="ltr">localization.key</code> — المفتاح المطلوب. ويستخدم بحث الجمع المفتاح المفرد.
  وقد يحمل بحث السياق المفتاح المركب EOT (End of Transmission).
- <code dir="ltr">localization.culture</code> — ثقافة البحث أو الخطوة.
- <code dir="ltr">localization.found</code> — يُعيَّن عند الاكتمال؛ ويغيب أثناء التشغيل وفي بحث
  فاشل.

لا يُوسم نص القالب أبداً. والبحث الفاشل يعيّن
<code dir="ltr">ActivityStatusCode.Error</code> ولا يسجّل إصابة أو غياباً أو مدة. ويُحترم أخذ
العينات: النشاط المستبعد بالعينة لا يُنشأ.

اشترك بـ <code dir="ltr">AddSource("Jeninnet.Localization")</code>.

## المقاييس

اسم المقياس: <code dir="ltr">Jeninnet.Localization</code>. النوع:
<code dir="ltr">System.Diagnostics.Metrics.Meter</code>.

| الأداة | النوع | الوحدة | الوسوم | المعنى |
|---|---|---|---|---|
| <code dir="ltr">localization.lookup.hits</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{lookup}</code> | culture | عمليات بحث مكتملة حلّت قالباً. |
| <code dir="ltr">localization.lookup.misses</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{lookup}</code> | culture | عمليات بحث مكتملة لم تحل شيئاً. |
| <code dir="ltr">localization.lookup.duration</code> | <code dir="ltr">Histogram&lt;double&gt;</code> | <code dir="ltr">ms</code> | culture | مدة بحث مكتمل. |
| <code dir="ltr">localization.fallback.steps</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{step}</code> | culture | استعلامات المزوّد على سلسلة الارتداد. |
| <code dir="ltr">localization.translation.requests</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{request}</code> | culture | طلبات ترجمة Azure الصادرة. |
| <code dir="ltr">localization.translation.cache.hits</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{lookup}</code> | culture | إصابات ذاكرة Azure المؤقتة. |
| <code dir="ltr">localization.translation.cache.misses</code> | <code dir="ltr">Counter&lt;long&gt;</code> | <code dir="ltr">{lookup}</code> | culture | غيابات ذاكرة Azure المؤقتة. |
| <code dir="ltr">localization.translation.duration</code> | <code dir="ltr">Histogram&lt;double&gt;</code> | <code dir="ltr">ms</code> | culture | مدة طلب Azure الصادر. |

لا تظهر المفاتيح في وسوم المقاييس أبداً. وتصدر أدوات الترجمة من مزوّد Azure، لا من Core. وبلا
مستمع مقياس، لا تُكتب الأدوات.

اشترك بـ <code dir="ltr">AddMeter("Jeninnet.Localization")</code>.

## الأحداث ومعرّفات الأحداث

### أحداث السجل

| معرّف الحدث | الاسم أو الرسالة | المستوى | المصدر | المعنى |
|---|---|---|---|---|
| <code dir="ltr">1001</code> | <code dir="ltr">Localization miss for key '{Key}' in culture '{Culture}' at '{CallSite}'.</code> | تحذير | <code dir="ltr">LoggerMissingKeySink</code> | غياب بحث واحد بعد سلسلة الارتداد الكاملة. |
| <code dir="ltr">1</code> | <code dir="ltr">LocalizationStoreUnreachable</code> | تحذير | جسر تعريب الطلب | كان المخزن غير متاح؛ فتراجعت المجموعة المخدومة إلى الثقافة الافتراضية وحدها. |

لا تُسجَّل إلا المعرّفات. ولا يعبر نص القالب حد المفتاح المفقود أبداً.

### أحداث المزوّد

يعلن <code dir="ltr">IProviderEventSource</code> حدثين في .NET، لا أحداث ETW/EventSource:

| الحدث | الحمولة | متى |
|---|---|---|
| <code dir="ltr">TranslationAcquired</code> | <code dir="ltr">ProviderAcquisitionEventArgs</code> (<code dir="ltr">Templates</code>، <code dir="ltr">Culture</code>) | نجح الاكتساب الخارجي. وإصابات الذاكرة المؤقتة لا ترفعه. |
| <code dir="ltr">AcquisitionFailed</code> | <code dir="ltr">ProviderAcquisitionFailedEventArgs</code> (<code dir="ltr">Culture</code>، <code dir="ltr">Exception</code>) | فشل الاكتساب الخارجي. وإلغاء المستدعي لا يرفعه. |

ترفع مزوّدات الملفات هذين الحدثين للتحميل الأولي وإعادة التحميل. ويرفعهما Azure للاكتساب الخارجي.
واستثناءات المعالج تنتشر إلى المستدعي.

### مصب المفاتيح المفقودة

<code dir="ltr">IMissingKeySink.ReportMiss(key, culture, callSite)</code> نقطة الامتداد. ويوفّر
Core الأنواع <code dir="ltr">MissingKeyCollector</code> و
<code dir="ltr">LoggerMissingKeySink</code> و<code dir="ltr">CompositeMissingKeySink</code>.
ويحتوي المُعرِّب استثناءات المصبات.

## فحوص الصحة

الحزمة: <code dir="ltr">Jeninnet.Localization.Integrations.Diagnostics</code>. التسجيل:
<code dir="ltr">AddLocalizationHealthChecks</code>.

| اسم الفحص | المسبار | سليم | متدهور | غير سليم |
|---|---|---|---|---|
| <code dir="ltr">jeninnet-localization-provider</code> | <code dir="ltr">ProviderProbeKey</code> لـ <code dir="ltr">ProviderProbeCulture</code> عبر <code dir="ltr">IMessageProvider</code> | المفتاح محلول | المزوّد متاح والمفتاح غير مخدوم | المسبار رمى |
| <code dir="ltr">jeninnet-localization-store</code> | <code dir="ltr">ILocalizationStore.GetSupportedCulturesAsync</code> | ثقافة واحدة أو أكثر | متاح بصفر ثقافات | المسبار رمى |

تحمل بيانات النتيجة معرّفات وأعداداً ومدداً وأسماء أنواع استثناءات. ولا تحمل نص قالب أو رسائل
استثناءات أبداً. ويسجّل فحص المزوّد فقط عند تكوين مفتاح مسبار؛ ويسجّل فحص المخزن فقط عندما يكون
المخزن مسجّلاً بالفعل.

الخيارات: <code dir="ltr">ProviderProbeKey</code> و
<code dir="ltr">ProviderProbeCulture</code> و<code dir="ltr">ProviderCheckName</code> (افتراضياً
<code dir="ltr">jeninnet-localization-provider</code>) و
<code dir="ltr">StoreCheckName</code> (افتراضياً
<code dir="ltr">jeninnet-localization-store</code>).

## حدود التشخيص

- **الغياب ليس خطأ.** لا يُرفع معرّف تشخيص أو حالة غير سليمة لغياب عادي.
- **فشل المزوّد ليس غياباً.** إنه ينتشر ويترك نشاط التتبع في حالة خطأ.
- **الإلغاء ليس حدث فشل.** ينتشر كـ <code dir="ltr">OperationCanceledException</code> ولا يرفع
  حدث اكتساب.
- **المصب المعطّل يكلّف فحص null واحداً.** بلا تخصيص وبلا تغيير سلوك.
- **لا يوجد موفّر EventSource/ETW.** استخدم <code dir="ltr">ActivitySource</code> و
  <code dir="ltr">Meter</code> و<code dir="ltr">ILogger</code> وفحوص الصحة والمصب.

## مثال عملي: قراءة تتبع واحد

ينتج طلب <code dir="ltr">de-CH</code> يُحل عبر <code dir="ltr">de</code> نشاط
<code dir="ltr">localization.lookup</code> بقيمة
<code dir="ltr">localization.culture=de-CH</code> و
<code dir="ltr">localization.found=true</code>، إضافة إلى طفلي
<code dir="ltr">localization.fallback.step</code>: <code dir="ltr">de-CH</code> بقيمة
<code dir="ltr">found=false</code> و<code dir="ltr">de</code> بقيمة
<code dir="ltr">found=true</code>. وتُظهر المقاييس إصابة واحدة وخطوتي ارتداد وعينة مدة واحدة
موسومة <code dir="ltr">de-CH</code>.

## مآزق

**مأزق:** وسم نص القالب أو قيم الوسائط. المفاتيح والثقافات هي الوسوم المقصودة.

**مأزق:** التعامل مع بحث فاشل كغياب. إنه لا يسجّل إصابة أو غياباً؛ والنشاط في حالة خطأ والاستثناء
ينتشر.

**مأزق:** توقع SARIF من أداة التكافؤ. إنها تصدر مخرجات طرفية وJSON مؤصَّلاً فقط.

## إلى أين بعد ذلك

- [التشخيص والرصد](../02-guides/14-diagnostics-and-observability.md) للاستخدام.
- [دليل الرصد](../04-operations/08-observability-cookbook.md) لوصفات المجمّع ولوحة المعلومات
  والتنبيه.
- [مرجع الأدوات](05-tooling-reference.md) لـ CLI التكافؤ وتكوين المولّد.
- [التوافق والإصدارات](07-compatibility-and-versions.md) لسياسة الاستقرار خلف
  هذه الأسماء.
- [استكشاف الأخطاء](../04-operations/04-troubleshooting.md) للتشخيص القائم على الأعراض.

</div>
