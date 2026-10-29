<div dir="rtl">

# تكوين المزوّدين (configuring providers)

يبدو تكوين المزوّد (provider configuration) كتكوين خيارات عادي، لكن هناك فرقاً مهماً واحداً: معظم
خيارات المزوّد هي **لقطات إنشاء** (construction snapshots)، وليست خدمات
<code dir="ltr">IOptions&lt;T&gt;</code>. يلتقط نداء التسجيل ما يحتاجه، ولا يرى المزوّد أي تعديل لاحق
على كائن الخيارات.

## أسلوبا التكوين

### خيارات مباشرة (معظم المزوّدين والمخزن)

تأخذ <code dir="ltr">AddMemoryLocalization</code> و<code dir="ltr">AddJsonLocalization</code>
و<code dir="ltr">AddXmlLocalization</code> و<code dir="ltr">AddResXLocalization</code>
و<code dir="ltr">AddSatelliteLocalization</code> و<code dir="ltr">AddPoLocalization</code>
و<code dir="ltr">AddPseudoLocalization</code> ومزوّدات رسائل قواعد البيانات الأربعة، إضافة إلى
<code dir="ltr">AddEntityFrameworkLocalizationStore</code>، كائن خيارات مكتوب النوع أو مفوّض
تكوين (configuration delegate). ينشئ الامتداد المزوّد فوراً ويسجّله كـ singleton.

<div dir="ltr">

```csharp
services.AddJsonLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

</div>

يعمل المفوّض أثناء التسجيل. يفشل التحقق عند بدء التشغيل، لا عند أول بحث، باستثناء وجود موارد ResX
وsatellite، الذي لا يستطيع <code dir="ltr">ResourceManager</code> اكتشافه إلا عند حدوث بحث.

### خط أنابيب الخيارات (مزوّدو الترجمة)

تحتوي <code dir="ltr">AddAzureTranslationLocalization()</code> و
<code dir="ltr">AddGoogleTranslationLocalization()</code> على تحميل بلا وسائط يسجّل المزوّد عبر
<code dir="ltr">IOptions&lt;T&gt;</code>:

<div dir="ltr">

```csharp
services.AddAzureTranslationLocalization();
services.Configure<AzureTranslationOptions>(configuration.GetSection("AzureTranslator"));
```

</div>

يتحقق المزوّد من الخيارات المحلولة عند إنشائه، لذا يرمي التكوين غير الصالح
<code dir="ltr">InvalidOperationException</code> عند حل المزوّد. أما تحميل المفوّض فيتحقق فوراً:

<div dir="ltr">

```csharp
services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.Region = configuration["AzureTranslator:Region"];
});
```

</div>

## ما يُلتقط، ومتى

يلتقط المزوّد القيم التي يحتاجها عند إنشائه. تنسخ الذاكرة القاموس؛ وتلتقط JSON وXML وPO المسار
وراية المراقبة وزمن التهدئة (debounce) والصيغة؛ وتلتقط ResX وsatellite الاسم الأساسي
(base name) والتجميعة؛ وتلتقط مزوّدات قواعد البيانات سلسلة الاتصال واسم الجدول؛ وينسخ التعريب
الزائف قوالبه؛ وتحل مزوّدات الترجمة خياراتها عند الإنشاء. تعيد إعادة التحميل اكتساب المصدر، ولا
تقرأ الخيارات مرة أخرى.

**مأزق:** <code dir="ltr">services.Configure&lt;JsonLocalizationOptions&gt;(...)</code> لا تفعل
شيئاً مع <code dir="ltr">AddJsonLocalization</code>. مزوّد JSON لا يُحلّ عبر
<code dir="ltr">IOptions&lt;T&gt;</code>.

## التحقق عند التسجيل

| المزوّد | ما يُتحقق منه عند التسجيل | ما يُؤجَّل |
|---|---|---|
| Memory | الخيارات، <code dir="ltr">Messages</code>، القاموس غير الفارغ | محتوى المفاتيح |
| JSON | <code dir="ltr">FilePath</code> غير فارغ، الملف موجود، تهدئة موجبة عند المراقبة | صيغة JSON وأنواع القيم |
| XML | <code dir="ltr">FilePath</code> غير فارغ، الملف موجود | صيغة XML |
| PO (Portable Object) | <code dir="ltr">FilePath</code> غير فارغ، الملف موجود | صيغة الكتالوج، المفاتيح المكررة، <code dir="ltr">Plural-Forms</code> |
| ResX | <code dir="ltr">BaseName</code> غير فارغ، <code dir="ltr">ResourceAssembly</code> غير فارغ | وجود المورد في البيان |
| Satellite | <code dir="ltr">BaseName</code> غير فارغ، <code dir="ltr">ResourceAssembly</code> غير فارغ | وجود المورد المترجم |
| Pseudo | القوالب، ثقافة زائفة غير فارغة، توسّع 0-100 | محتوى المفاتيح |
| Database | سلسلة اتصال واسم جدول غير فارغين؛ شكل المعرّف | الاتصال ووجود الجدول |
| Azure | <code dir="ltr">ApiKey</code>، عنوان URI، مدة الذاكرة المؤقتة، المهلة، إعادة المحاولة، القاطع، تحديد المعدل | إخفاقات النداء البعيد |
| Google | <code dir="ltr">ApiKey</code>، عنوان URI | إخفاقات النداء البعيد |
| EF store | سلسلة اتصال غير فارغة أو سلسلة اتصال مسماة موجودة | مخطط قاعدة البيانات |

تقبل <code dir="ltr">DatabaseLocalizationOptions.TableName</code> المحارف الأبجدية الرقمية
والشرطات السفلية، حتى ثلاثة مقاطع تفصلها نقاط، ويبدأ كل مقطع بحرف أو شرطة سفلية ويحتوي 1-128
محرفاً. <code dir="ltr">dbo.LocalizationMessages</code> صالح؛ أما المسافات وعلامات الترقيم فتُرفض
عند تعيين الخاصية.

تحمّل JSON وXML وPO بشكل كسول، لكنها تتحقق من وجود الملف أثناء التسجيل. والملف الذي يُولَّد لاحقاً
أثناء بدء التشغيل سيفشل التسجيل. تؤجّل ResX وsatellite المورد المفقود إلى أول بحث، حيث يرمي
<code dir="ltr">MissingManifestResourceException</code>.

## سلوك إعادة التحميل الموجود

مزوّدات الملفات وحدها تملك سلوك إعادة تحميل مدمجاً، وهو اختياري.

| المزوّد | <code dir="ltr">WatchForChanges</code> | <code dir="ltr">ReloadDebounce</code> | <code dir="ltr">RefreshAsync</code> | أحداث الاكتساب |
|---|---|---|---|---|
| JSON | نعم، الافتراضي <code dir="ltr">false</code> | 500 ms افتراضياً؛ موجب عند المراقبة | نعم | <code dir="ltr">TranslationAcquired</code>، <code dir="ltr">AcquisitionFailed</code> |
| XML | نعم، الافتراضي <code dir="ltr">false</code> | 500 ms افتراضياً؛ موجب عند المراقبة | نعم | نفسه |
| PO | نعم، الافتراضي <code dir="ltr">false</code> | 500 ms افتراضياً؛ موجب عند المراقبة | نعم | نفسه |
| Memory وResX وsatellite وقواعد البيانات | لا | لا | لا | لا |
| ترجمة Azure | لا | لا | نعم، إبطال الذاكرة المؤقتة | نعم |
| ترجمة Google | لا | لا | لا | لا |

يبدّل التغيير المراقَب جدول القوالب أو الكتالوج كله ذرّياً. وتحتفظ إعادة التحميل الفاشلة بآخر جدول
سليم وتُطلق <code dir="ltr">AcquisitionFailed</code>؛ أما النجاح فيُطلق
<code dir="ltr">TranslationAcquired</code>. ولا ترمي المراقبة أبداً على مسار البحث. تعيد
<code dir="ltr">RefreshAsync</code> التحميل فوراً و**ترمي** عند الفشل، لذا تناسب أمراً صريحاً أو
فحص صحة، لا استخداماً خلفياً بلا انتظار.

تُسجَّل مزوّدات الملفات كـ <code dir="ltr">IInnerMessageProvider</code>. لنداء
<code dir="ltr">RefreshAsync</code> أو الاشتراك في الأحداث، حلّ المزوّد المحدد أو احتفظ بالنسخة
التي سجّلتها. العقد القانوني لا يكشف هذه القدرات.

**مأزق:** لا يُرفض <code dir="ltr">ReloadDebounce</code> غير الموجب إلا عند تفعيل
<code dir="ltr">WatchForChanges</code>. وعند إيقاف المراقبة تُقبل القيمة ولا تُستخدم.

## أمثلة لكل عائلة

الذاكرة خريطة مسطحة واحدة تتجاهل الثقافة. وتتطلب الذاكرة متعددة الثقافات
<code dir="ltr">CultureAwareMessageProvider</code>؛ ولا يوجد تحميل متعدد الثقافات.

<div dir="ltr">

```csharp
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["App.Title"] = "Bookstore",
    ["Greeting"] = "Hello, {name}!"
});
```

</div>

يتحقق JSON من وجود الملف عند التسجيل ويدعم المراقبة:

<div dir="ltr">

```csharp
services.AddJsonLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

</div>

يتوقع تحميل الاصطلاح <code dir="ltr">{baseDirectory}/{subdirectory}/{culture}.json</code>؛ ولـ XML
شكل الخيارات نفسه، مع <code dir="ltr">XmlLocalizationOptions</code> وملفات
<code dir="ltr">.xml</code>.

تستخدم ResX وsatellite اسماً أساسياً وتجميعة. عيّن التجميعة صراحةً؛ فالافتراضي هو تجميعة المزوّد،
لا تجميعة التطبيق. وكلاهما يعطّل استكشاف الآباء.

<div dir="ltr">

```csharp
services.AddResXLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
services.AddSatelliteLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
```

</div>

يستخدم PO الخيارات <code dir="ltr">FilePath</code> و<code dir="ltr">Format</code>
و<code dir="ltr">WatchForChanges</code> و<code dir="ltr">ReloadDebounce</code>. يتوقع التحميل
متعدد الثقافات <code dir="ltr">{culture}.po</code> ولا يستكشف <code dir="ltr">.mo</code>؛ وتستخدم
عمليات نشر MO (Machine Object) مسارات صريحة أو <code dir="ltr">IPoFileLocationProvider</code> مخصصاً.

يستخدم التعريب الزائف <code dir="ltr">SourceTemplates</code> و
<code dir="ltr">PseudoCulture</code> و<code dir="ltr">ExpansionPercent</code> (0-100) ويخدم
ثقافته الاصطناعية فقط. وتشترك مزوّدات قواعد البيانات في
<code dir="ltr">ConnectionString</code> و<code dir="ltr">TableName</code> (الأعمدة
<code dir="ltr">MessageKey</code> و<code dir="ltr">Culture</code> و
<code dir="ltr">Template</code>):

<div dir="ltr">

```csharp
services.AddSqlServerLocalization(options => {
    options.ConnectionString = configuration.GetConnectionString("Localization")!;
    options.TableName = "dbo.LocalizationMessages";
});
```

</div>

مخزن مجموعات موارد EF Core ليس مزوّد رسائل؛ وإدارة المخطط مسؤولية المستهلك:

<div dir="ltr">

```csharp
services.AddEntityFrameworkLocalizationStore(configuration, connectionStringName: "LocalizationDb");
```

</div>

يستخدم Azure الخيارات <code dir="ltr">ApiKey</code> و<code dir="ltr">Region</code>
و<code dir="ltr">CachingEnabled</code> و<code dir="ltr">CacheExpiration</code> وإعدادات المرونة؛
ويستخدم Google الخيارين <code dir="ltr">ApiKey</code> و<code dir="ltr">Endpoint</code>. راجع
[الترجمة الآلية](12-machine-translation.md).

<div dir="ltr">

```csharp
services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.CachingEnabled = true;
});
```

</div>

## تكوين خط الأنابيب والمنسّق

تقبل <code dir="ltr">AddLocalization</code> كائن <code dir="ltr">LocalizationOptions</code>، مثل
<code dir="ltr">services.AddLocalization(options => options.DefaultCulture = "en")</code>.
<code dir="ltr">DefaultCulture</code> هو خطوة الارتداد الأخيرة؛ وتعطيله يكون بقيمة فارغة أو null.
يتحقق التحميل المُكوِّن من الاسم ويرمي <code dir="ltr">CultureNotFoundException</code> لثقافة
غير معروفة. كما يرمي <code dir="ltr">InvalidOperationException</code> إذا كانت
<code dir="ltr">LocalizationOptions</code> مسجّلة بالفعل، لذا استخدم إما
<code dir="ltr">AddLocalization()</code> أو <code dir="ltr">AddLocalization(configure)</code>، لا
الاثنين. <code dir="ltr">MissingKeySink</code> هو خطاف التشخيص الاختياري، ويُبلَّغ عنه فقط بعد
غياب السلسلة كاملة.

تستخدم <code dir="ltr">AddLocalization</code> الدالة <code dir="ltr">TryAddSingleton</code> لـ
<code dir="ltr">IMessageFormatter</code>. سجّل منسّقاً مخصصاً قبل ندائها، أو استبدل الواصف بعدها:

<div dir="ltr">

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization();
```

</div>

## مثال عملي: مراقبة في التطوير ولقطة في الإنتاج

<div dir="ltr">

```csharp
var path = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");

builder.Services.AddJsonLocalization(options => {
    options.FilePath = path;
    options.WatchForChanges = builder.Environment.IsDevelopment();
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

</div>

تنتج البيئتان المزوّد نفسه وسلوك البحث نفسه؛ ولا تختلف إلا إعادة التحميل. يأتي المسار من
<code dir="ltr">AppContext.BaseDirectory</code>، لذا يحل التطوير والنشر التخطيط نفسه عندما تكون
قاعدة نسخ المحتوى صحيحة.

**مأزق:** افتراض أن إعادة التحميل ذرّية عبر الثقافات. كل ملف مراقَب يُعاد تحميله مستقلاً؛ ويمكن
أن يخدم نشر متعدد الملفات إصدارات مختلطة لفترة وجيزة. انشر الملفات المرتبطة معاً.

**مأزق:** تفعيل المراقبة على نظام ملفات حاوية للقراءة فقط؛ فالمراقبة للمحتوى القابل للتغيير.

## إلى أين بعد ذلك

- [اختيار مزوّد](01-choosing-a-provider.md) للقرار على مستوى السيناريو.
- [الثقافات والارتداد](03-cultures-and-fallback.md) لـ
  <code dir="ltr">DefaultCulture</code> وثقافات الطلب.
- [مرجع المزوّدين](../03-reference/03-provider-reference.md) لمصفوفة الخيارات الكاملة.

</div>
