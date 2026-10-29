<div dir="rtl">

# مرجع المزوّدين (provider reference)

كل مصدر رسائل ينفّذ <code dir="ltr">IInnerMessageProvider</code>. ومزوّد واحد محلول بالضبط يخدم
تركيباً (composition). يجمع هذا الفصل حزم المزوّدين حسب العائلة، ويسجّل أسماء الخيارات
والقيم الافتراضية الموجودة فعلاً، ويوضح ما لا تفعله كل حزمة.

هوية الحزمة هي اسم المشروع: <code dir="ltr">Jeninnet.Localization.Providers.*</code> لمزوّدي وقت
التشغيل، و<code dir="ltr">Jeninnet.Localization.Translation.*</code> للترجمة الآلية، وحزمة مخزن
EF Core لمخزن مجموعات الموارد.
و<code dir="ltr">Jeninnet.Localization.Providers.Database.Abstractions</code> مكتبة أساس مشتركة،
لا مصدر قابل للتسجيل.

عقود المزوّد المولّدة: <xref:Jeninnet.Localization.Abstractions.IInnerMessageProvider> و
<xref:Jeninnet.Localization.Abstractions.IMessageProvider>.

## الملخص

| الحزمة | المصدر | نموذج الثقافة | إعادة التحميل | القيد الأساسي |
|---|---|---|---|---|
| <code dir="ltr">Jeninnet.Localization.Providers.Memory</code> | قاموس في الذاكرة | خريطة مسطحة واحدة؛ تتجاهل الثقافة | لا شيء | لا تحرير خارجي |
| <code dir="ltr">Jeninnet.Localization.Providers.Json</code> | ملف JSON واحد | ملف لكل ثقافة؛ موزّع لتعدد الثقافات | مراقبة اختيارية + <code dir="ltr">RefreshAsync</code> | سلاسل مسطحة فقط |
| <code dir="ltr">Jeninnet.Localization.Providers.Xml</code> | ملف XML واحد | ملف لكل ثقافة؛ موزّع لتعدد الثقافات | مراقبة اختيارية + <code dir="ltr">RefreshAsync</code> | سلاسل مسطحة، ومفاتيح بأسماء XML |
| <code dir="ltr">Jeninnet.Localization.Providers.Po</code> | نص PO (Portable Object) أو ثنائي MO (Machine Object) | كتالوج واحد لكل ثقافة | مراقبة اختيارية + <code dir="ltr">RefreshAsync</code> | يتطلب انضباط أدوات |
| <code dir="ltr">Jeninnet.Localization.Providers.ResX</code> | <code dir="ltr">.resources</code> مترجمة عبر <code dir="ltr">ResourceManager</code> | ثقافة دقيقة، بلا استكشاف آباء | لا شيء | سلاسل فقط؛ الاسم الأساسي المفقود يرمي |
| <code dir="ltr">Jeninnet.Localization.Providers.Satellite</code> | موارد محايدة + حزم satellite | ثقافة دقيقة، بلا استكشاف آباء | لا شيء | سلاسل فقط؛ الحزم خارجية |
| <code dir="ltr">Jeninnet.Localization.Providers.Pseudo</code> | مصدر في الذاكرة محوَّل | يخدم ثقافة اصطناعية واحدة | لا شيء | أداة ضمان جودة، لا ترجمة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.SqlServer</code> | جدول SQL Server | صفوف <code dir="ltr">(key, culture)</code> دقيقة | لا شيء | رحلة قاعدة بيانات لكل خطوة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.MySql</code> | جدول MySQL | صفوف <code dir="ltr">(key, culture)</code> دقيقة | لا شيء | رحلة قاعدة بيانات لكل خطوة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.PostgreSQL</code> | جدول PostgreSQL | صفوف <code dir="ltr">(key, culture)</code> دقيقة | لا شيء | رحلة قاعدة بيانات لكل خطوة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.SQLite</code> | جدول SQLite | صفوف <code dir="ltr">(key, culture)</code> دقيقة | لا شيء | رحلة قاعدة بيانات لكل خطوة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code> | مخزن مجموعات موارد SQL Server | مجموعة + ثقافة دقيقة | إعادة تحميل يملكها المخزن | ليس مزوّد رسائل؛ وليس AOT (Ahead-of-Time) |
| <code dir="ltr">Jeninnet.Localization.Translation.Azure</code> | Azure Translator v3 | الثقافة الهدف؛ والمفتاح نص المصدر | <code dir="ltr">RefreshAsync</code> | شبكة وكلفة وقاعدة المزوّد الواحد |
| <code dir="ltr">Jeninnet.Localization.Translation.Google</code> | Google Translate v2 | الثقافة الهدف؛ والمفتاح نص المصدر | لا شيء | شبكة وكلفة، بلا ذاكرة أو تجميع |

## Memory

تنسخ <code dir="ltr">AddMemoryLocalization</code> القاموس المقدَّم
<code dir="ltr">Messages</code> إلى لقطة إنشاء غير قابلة للتغيير. وعمليات البحث قراءات قاموس
بمقارنة ترتيبية حساسة لحالة الأحرف. ويتجاهل المزوّد وسيطة الثقافة، لذا تخدم نسخة واحدة خريطة
مسطحة واحدة. وتستخدم التراكيب متعددة الثقافات
<code dir="ltr">CultureAwareMessageProvider</code> مع خريطة واحدة لكل ثقافة.

- **الخيارات:** <code dir="ltr">MemoryLocalizationOptions.Messages</code> (من النوع
  <code dir="ltr">IReadOnlyDictionary&lt;string,string&gt;</code>، وقيمته الافتراضية فارغة؛ ويجب ألا يكون
  null).
- **إعادة التحميل:** لا شيء. تتطلب التغييرات تسجيلاً جديداً أو إعادة تشغيل العملية.
- **WASM (WebAssembly):** يعلن <code dir="ltr">IWasmCompatibleMessageProvider</code>.
- **اختره لـ:** الاختبارات والنماذج الأولية والكتالوجات الثابتة الصغيرة والمضيفات المتوافقة مع
  المتصفح.

## JSON

تسجّل <code dir="ltr">AddJsonLocalization</code> مزوّداً واحداً
<code dir="ltr">JsonMessageProvider</code>. يُحمَّل الملف كسولاً عند أول بحث ضمن تهيئة متزامنة
لمرة واحدة. ويبقى الجدول غير قابل للتغيير طوال عمر المزوّد ما لم تُفعَّل إعادة التحميل الساخنة.

- **الخيارات:** <code dir="ltr">FilePath</code> (مطلوب؛ ويجب أن يوجد عند التسجيل)، و
  <code dir="ltr">WatchForChanges</code> (افتراضياً <code dir="ltr">false</code>)، و
  <code dir="ltr">ReloadDebounce</code> (افتراضياً 500 ms؛ ويجب أن يكون موجباً عند المراقبة).
- **الشكل:** كائن JSON مسطح يربط مفاتيح نصية بقوالب نصية. يفكّ المزوّد التسلسل إلى قاموس؛ ولا
  يستخدم كائنات متداخلة للمفاتيح.
- **إعادة التحميل:** مراقبة ملف اختيارية. ويبدّل التغيير الجدول كله ذرّياً. وتحتفظ إعادة التحميل
  الفاشلة بآخر جدول سليم وترفع <code dir="ltr">AcquisitionFailed</code>؛ والنجاح يرفع
  <code dir="ltr">TranslationAcquired</code>. وتعيد <code dir="ltr">RefreshAsync</code> التحميل
  فوراً وترمي عند الفشل.
- **تعدد الثقافات:** تتوقع
  <code dir="ltr">AddJsonLocalizationProviders(baseDirectory, cultures)</code> المسار
  <code dir="ltr">{baseDirectory}/{subdirectory}/{culture}.json</code>، أو استخدم تحميل المنشئ
  لمسارات صريحة. ويُلف كل مزوّد في
  <code dir="ltr">CultureAwareMessageProvider</code>.
- **الحدود:** لا سياقات ولا جداول جمع ولا قيود أسماء XML تقلق بشأنها، لكن لا بيانات وصفية ولا
  ملاحظات أيضاً.

JSON هو الصيغة المرجعية لمولّد المفاتيح (<code dir="ltr">*.loc.json</code>). ويعامل تحميل الاصطلاح
جذر الملف كرمز ثقافة، لذا فملف باسم <code dir="ltr">en.loc.json</code> ليس ثقافة
<code dir="ltr">en</code>.

## XML

لـ <code dir="ltr">AddXmlLocalization</code> دورة الحياة وأسماء الخيارات نفسها في JSON، مع
<code dir="ltr">XmlLocalizationOptions</code>. والملف مستند XML مسطح تكون عناصره الفرعية المباشرة
إدخالات: الاسم المحلي للعنصر هو المفتاح، وقيمته هي القالب.

- **الخيارات:** <code dir="ltr">FilePath</code> (مطلوب؛ ويوجد عند التسجيل)، و
  <code dir="ltr">WatchForChanges</code> (افتراضياً <code dir="ltr">false</code>)، و
  <code dir="ltr">ReloadDebounce</code> (افتراضياً 500 ms؛ وموجب عند المراقبة).
- **الشكل:** <code dir="ltr">&lt;messages&gt;&lt;App.Title&gt;Bookstore&lt;/App.Title&gt;&lt;/messages&gt;</code>.
  ويجب أن تكون المفاتيح أسماء XML صالحة. وأسماء العناصر المكررة تفشل التحميل.
- **إعادة التحميل:** مطابقة لـ JSON، بما فيها الأحداث و<code dir="ltr">RefreshAsync</code> الرامية.
- **تعدد الثقافات:** تستخدم <code dir="ltr">AddXmlLocalizationProviders</code> المسار
  <code dir="ltr">{culture}.xml</code> تحت الدليل الفرعي.
- **الحدود:** سلاسل مسطحة فقط؛ لا سياقات ولا جداول جمع. وقواعد أسماء عناصر XML تقيّد المفاتيح.

## PO وMO

تخدم <code dir="ltr">AddPoLocalization</code> كتالوجات gettext. والكتالوج قوالب مطابقة تماماً
إضافة إلى جداول صيغ الجمع والسياقات. وقد يكون الملف نص PO أو ثنائي GNU MO.

- **الخيارات:** <code dir="ltr">FilePath</code> (مطلوب؛ ويوجد عند التسجيل)، و
  <code dir="ltr">Format</code> (<code dir="ltr">PoCatalogFormat.Auto</code> افتراضياً؛ ويختار
  <code dir="ltr">Auto</code> تنسيق MO بامتداد <code dir="ltr">.mo</code>، وإلا PO)، و
  <code dir="ltr">WatchForChanges</code> (افتراضياً <code dir="ltr">false</code>)، و
  <code dir="ltr">ReloadDebounce</code> (افتراضياً 500 ms).
- **تعدد الثقافات:** تتوقع
  <code dir="ltr">AddPoLocalizationProviders(baseDirectory, cultures)</code> المسار
  <code dir="ltr">{culture}.po</code>. ولا يستكشف مزوّد الموقع الافتراضي
  <code dir="ltr">.mo</code>؛ استخدم مسارات صريحة أو
  <code dir="ltr">IPoFileLocationProvider</code> مخصصاً أو المنشئ.
- **صيغ الجمع:** ينفّذ <code dir="ltr">IPluralFormMessageProvider</code>. وعندما يحمل الكتالوج
  ترويسة <code dir="ltr">Plural-Forms</code>، يختار المزوّد فهرس
  <code dir="ltr">msgstr[n]</code> الدقيق. وإلا يستخدم تعيين الزوج في CLDR (Common Locale Data Repository).
- **السياقات:** تُعنون إدخالات <code dir="ltr">msgctxt</code> عبر المفتاح المركب EOT (End of Transmission) الذي ينتجه
  <code dir="ltr">LocalizeWithContextAsync</code>.
- **إعادة التحميل:** مراقبة اختيارية؛ و<code dir="ltr">RefreshAsync</code> يرمي عند الفشل. وتُعاد
  تحميل كتالوجات MO بالطريقة نفسها.
- **الحدود:** يلزم انضباط أدوات المترجمين والملفات. وتعتمد دلالات الجمع على الترويسة التي يحملها
  الكتالوج؛ ولا يختلق الإطار تعبيرات gettext.

اختر JSON/XML للكتالوجات التي يملكها المستودع وتُنشر مع الكود؛ وPO لأدوات gettext واحتياجات الجمع
أو السياق.

## ResX وsatellite

تستخدم <code dir="ltr">AddResXLocalization(baseName, assembly)</code> و
<code dir="ltr">AddSatelliteLocalization(baseName, assembly)</code> كلتاهما
<code dir="ltr">ResourceManager</code>. وتختلفان فيما تتوقعانه على القرص.

- **خيارات ResX:** <code dir="ltr">BaseName</code> (مطلوب وغير فارغ)، و
  <code dir="ltr">ResourceAssembly</code> (وقيمته الافتراضية تجميعة المزوّد، لا تجميعة التطبيق؛ عيّنه
  صراحةً).
- **خيارات Satellite:** الاسمان نفسهما والقيمة الافتراضية نفسها. والاسم الأساسي هو اسم مورد البيان
  بلا ثقافة أو امتداد؛ وتُستكشف حزم satellite بجوار التجميعة.
- **الثقافة:** تستدعي كلتاهما
  <code dir="ltr">GetResourceSet(culture, createIfNotExists: true, tryParents: false)</code>.
  واستكشاف الآباء معطّل كي يبقى المُعرِّب مالك الارتداد الوحيد.
- **سلوك الغياب:** يرمي ResX الاستثناء
  <code dir="ltr">MissingManifestResourceException</code> عندما يكون أساس المورد نفسه مفقوداً.
  ويرمي Satellite لغياب الأساس، لكنه يعامل حزمة satellite المفقودة كـ
  <code dir="ltr">null</code> نظيف. ويحوّل Satellite المورد غير النصي إلى
  <code dir="ltr">null</code>.
- **إعادة التحميل:** لا شيء. أعد البناء والنشر.
- **اختر ResX** عندما تؤلف ملفات <code dir="ltr">.resx</code> في المشروع نفسه؛ واختر
  **Satellite** عندما تصل السلاسل كحزم مترجمة مسبقاً، بما فيها حزم مؤلَّفة في مكان آخر.

## التعريب الزائف

تخدم <code dir="ltr">AddPseudoLocalization</code> ثقافة اصطناعية بتحويل قوالب المصدر. إنها تضيف
علامات نطق إلى الحروف اللاتينية عبر خريطة ثابتة، وتنسخ امتدادات
<code dir="ltr">{...}</code> المتوازنة حرفياً،
وتلف النتيجة بـ <code dir="ltr">[!! ... !!]</code>، وتلحق حشواً من
<code dir="ltr">~</code>.

- **الخيارات:** <code dir="ltr">SourceTemplates</code> (مطلوب وغير null)، و
  <code dir="ltr">PseudoCulture</code> (افتراضياً <code dir="ltr">qps-ploc</code>)، و
  <code dir="ltr">ExpansionPercent</code> (افتراضياً 35؛ وصالح 0–100).
- **الثقافة:** تُخدم الثقافة الاصطناعية المكوَّنة فقط؛ وكل ثقافة أخرى تعيد
  <code dir="ltr">null</code>.
- **الحتمية:** التحويل دالة نقية للقالب والنسبة. ولا توجد طوابع زمنية أو عشوائية أو اعتماد على
  ثقافة المضيف.
- **WASM:** يعلن <code dir="ltr">IWasmCompatibleMessageProvider</code>.
- **اختره لـ:** فائض التخطيط، وتسريبات السلاسل المضمّنة، والعروض. إنه ليس ترجمة.

## مزوّدات رسائل قواعد البيانات

تسجّل <code dir="ltr">AddSqlServerLocalization</code> و
<code dir="ltr">AddMySqlLocalization</code> و<code dir="ltr">AddPostgreSqlLocalization</code> و
<code dir="ltr">AddSqliteLocalization</code> مزوّدي ADO.NET يستعلمون جدولاً واحداً في كل بحث.

- **الخيارات:** <code dir="ltr">ConnectionString</code> (مطلوب وغير فارغ) و
  <code dir="ltr">TableName</code> (افتراضياً
  <code dir="ltr">LocalizationMessages</code>). ويقبل
  <code dir="ltr">TableName</code> حتى ثلاثة مقاطع تفصلها نقاط؛ ويبدأ كل مقطع بحرف أو شرطة سفلية
  ويكون بطول 1–128 محرفاً. <code dir="ltr">dbo.LocalizationMessages</code> صالح؛ والمسافات
  وعلامات الترقيم تُرفض عند تعيين الخاصية.
- **المخطط:** الأعمدة <code dir="ltr">MessageKey</code> و<code dir="ltr">Culture</code> و
  <code dir="ltr">Template</code>. والاستعلام ذو وسائط؛ وتُهرَّب المعرّفات حسب المورّد.
- **الثقافة:** صفوف <code dir="ltr">(key, culture)</code> دقيقة. وتتبع حساسية حالة المفتاح
  ترتيب العمود (collation).
- **إعادة التحميل:** لا شيء. تظهر الكتابة في البحث التالي؛ ولا توجد ذاكرة رسائل مدمجة.
- **الكلفة:** فتح اتصال واحد واستعلام واحد لكل خطوة ارتداد. أضف تخزيناً مؤقتاً فوق المزوّد إذا
  كانت تلك الكلفة مهمة.
- **اختره لـ:** كتالوج مركزي يتغير دون نشر. وتنتمي واجهات الإدارة المحكومة والتاريخ إلى مخزن EF،
  لا إلى هؤلاء المزوّدين.

تحتوي <code dir="ltr">Jeninnet.Localization.Providers.Database.Abstractions</code> النوع
<code dir="ltr">DatabaseMessageProviderBase&lt;TOptions&gt;</code> ونوع الخيارات المشترك. وليس
فيها تسجيل <code dir="ltr">Add...</code> وليست مزوّداً بذاتها.

## مخزن مجموعات موارد EF Core

تسجّل <code dir="ltr">AddEntityFrameworkLocalizationStore(connectionString)</code> أو تحميل
<code dir="ltr">IConfiguration</code> بسلسلة اتصال مسماة (افتراضياً
<code dir="ltr">LocalizationDb</code>) النوع <code dir="ltr">SqlDbLocalizationStore</code>
كـ singleton <code dir="ltr">ILocalizationStore</code>. ويستهدف SQL Server.

- **ليس <code dir="ltr">IMessageProvider</code>.** لا يغذّي
  <code dir="ltr">IMessageLocalizer</code>؛ بل يغذّي
  <code dir="ltr">IStringLocalizer</code> عبر
  <code dir="ltr">CustomStringLocalizerFactory</code>.
- **المخطط:** <code dir="ltr">LocalizedStrings</code>، وسجلات التدقيق، ويوميات إعادة التشغيل، وصف
  الإصدار، والملاحظات.
- **القدرات:** الإدارة والتاريخ؛ راجع [واجهة التخزين](02-storage-api.md).
- **إدارة المخطط:** مسؤولية المستهلك. يضم <code dir="ltr">EnsureCreated</code> النموذج الحالي؛
  وتحتاج قواعد البيانات الأقدم إلى ترحيل.
- **AOT:** مستبعد من عقد trim/AOT لأن EF Core ليس آمناً للتقليص بالكامل.
- **اختره لـ:** مجموعات موارد قابلة للتعديل وقت التشغيل، والإدارة المدققة، والملاحظات، والمزامنة
  القائمة على الكود.

## Azure Translator

تسجّل <code dir="ltr">AddAzureTranslationLocalization</code> مزوّد الترجمة القادر.

- **المطلوب:** <code dir="ltr">ApiKey</code>. **الافتراضيات:** <code dir="ltr">Endpoint</code>
  <code dir="ltr">https://api.cognitive.microsofttranslator.com</code>، و
  <code dir="ltr">Region</code> فارغ، و<code dir="ltr">FromLanguage</code> فارغ، و
  <code dir="ltr">CachingEnabled</code> صحيح، و<code dir="ltr">CacheExpiration</code> ساعة، و
  <code dir="ltr">RequestTimeout</code> 30 ثانية، و<code dir="ltr">RetryMaxAttempts</code> 3، و
  <code dir="ltr">RetryBaseDelay</code> 200 ms، و<code dir="ltr">RetryMaxDelay</code> 5 ثوانٍ،
  و<code dir="ltr">BreakerFailureThreshold</code> 5، و
  <code dir="ltr">BreakerOpenDuration</code> 30 ثانية، و
  <code dir="ltr">RateLimitRequestsPerSecond</code> 0 (معطّل)، و
  <code dir="ltr">RateLimitBurstSize</code> 1.
- **الاختياري:** <code dir="ltr">Glossary</code> و<code dir="ltr">TranslationMemory</code>؛
  وكلاهما يُلتقط أو تُؤخذ لقطة له عند الإنشاء.
- **القدرات:** تجميع متعدد المفاتيح (100 مفتاح)، وتخزين مؤقت، وتحميل مسبق عند تفعيل التخزين
  المؤقت، وتحديث، وأحداث اكتساب.
- **الحدود:** شبكة وكلفة عند غياب الذاكرة المؤقتة؛ ومزوّد واحد فقط؛ والمفتاح نص المصدر.
- **اختره لـ:** المحتوى الديناميكي، أو التعبئة، أو سير عمل دفعات مُراجَع. ولمعظم المنتجات،
  ثبّت النتائج في مزوّد ملفات أو قاعدة بيانات.

## Google Translate

تسجّل <code dir="ltr">AddGoogleTranslationLocalization</code> المزوّد الأدنى.

- **المطلوب:** <code dir="ltr">ApiKey</code>. **الافتراضي:** <code dir="ltr">Endpoint</code>
  <code dir="ltr">https://translation.googleapis.com/language/translate/v2</code>.
- **القدرات:** تعيد <code dir="ltr">Supports</code> القيمة false لكل ميزة. لا تجميع ولا ذاكرة
  مؤقتة ولا تحميل مسبق ولا تحديث ولا حدث اكتساب.
- **السلوك:** المفتاح نص المصدر؛ واللغة الهدف هي اسم الثقافة؛ وتُكتشف اللغة المصدر تلقائياً.
  والثقافة الثابتة تعيد <code dir="ltr">null</code> دون طلب. والفشل البعيد ينتشر.
- **اختره لـ:** محتوى ديناميكي منخفض الحجم يكون فيه نداء مدفوع واحد لكل بحث مقبولاً.

## مثال عملي: اختيار مصدر واحد

يخدم كتالوج يملكه المستودع <code dir="ltr">IMessageLocalizer</code> من JSON متعدد الثقافات. ويعمل
فريق مترجمين على المحتوى نفسه عبر تبادل PO، لكن وقت التشغيل يقرأ JSON. ويستخدم كتالوج مُدار مخزن
EF لـ <code dir="ltr">IStringLocalizer</code> ومزوّد قاعدة بيانات لخط الأنابيب فقط إذا كان
المساران مقصودين. ويسجّل كل مضيف مصدراً واحداً؛ والصيغة المستخدمة للتبادل لا تغيّر ذلك.

## مآزق

**مأزق:** تسجيل مزوّدين لدمج مصادر. يرمي المحدد؛ وحّد البيانات.

**مأزق:** تعديل خيارات المزوّد بعد التسجيل. الخيارات لقطات إنشاء.

**مأزق:** التعامل مع مراقبة الملفات كآلية نشر. إنها اختيارية وتحتفظ بآخر جدول سليم عند الفشل؛
انشر المحتوى عمداً.

## إلى أين بعد ذلك

- [اختيار مزوّد](../02-guides/01-choosing-a-provider.md) للقرار على مستوى السيناريو.
- [تكوين المزوّدين](../02-guides/02-configuring-providers.md) للتحقق واللقطات.
- [الترجمة الآلية](../02-guides/12-machine-translation.md) لنموذج المرونة والدفعات.
- [التوافق والإصدارات](07-compatibility-and-versions.md) لاستثناءات AOT.

</div>
