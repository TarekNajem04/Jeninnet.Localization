<div dir="rtl">

# 2. التثبيت (installation) وأول تسجيل (registration)

بعد أن عرفت لماذا يخرج التعريب عن كونه قاموساً، تبدأ الخطوة العملية: أي حزمة (package) تضيف إلى مشروعك،
وكيف تسجّلها، وما الذي يجب أن يكون جاهزاً قبل أول استدعاء. هذا الفصل يبني تسجيلاً عاملاً، ويشرح
القرارات التي تُتخذ عادةً في هذه المرحلة.

## المتطلبات

- **.NET 8 SDK** أو أحدث. مشاريع المكتبة تستهدف <code dir="ltr">net8.0</code> و<code dir="ltr">net9.0</code> و<code dir="ltr">net10.0</code>؛ أما حزم مولّد
  Roslyn والمحلل (analyzer) فتستهدف <code dir="ltr">netstandard2.0</code> لأنها تعمل داخل المترجم،
  لا داخل تطبيقك.
- مشروع يستطيع استخدام <code dir="ltr">Microsoft.Extensions.DependencyInjection</code>. تطبيقات
  الكونسول (console applications) وتطبيقات ASP.NET Core وخدمات worker ومضيفات Blazor ومضيفات
  سطح المكتب كلها مؤهلة.
- بالنسبة لمزوّدي الملفات (file providers)، مجلد محتوى موجود عند بدء التشغيل ويُنشر مع التطبيق.

## التوفر والإصدار

المستودع **غير منشور** وما زال قيد التطوير النشط. لم تُنشر أي حزم على NuGet. إصدار المنتج الذي
يعلنه البناء هو <code dir="ltr">1.0.0-preview.1</code>، وينتج <code dir="ltr">dotnet pack</code> المحلي
حزماً بالإصدار <code dir="ltr">1.0.0-preview.1</code> داخل مجلد <code dir="ltr">artifacts/</code>.

لذلك تصف الأوامر التالية سير النشر المقصود، بينما النسخة التي تعمل فعلاً اليوم هي نسخة المخزن
المحلي (local feed):

<div dir="ltr">

```powershell
# Produce the packages locally.
dotnet pack Jeninnet.Localization.slnx -c Release

# Consume the local feed from an application project (adjust the relative path).
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
dotnet add package Jeninnet.Localization.Providers.Json --source ..\artifacts
```

</div>

بعد نشر الحزم، تعمل الأوامر نفسها دون <code dir="ltr">--source</code>:

<div dir="ltr">

```powershell
dotnet add package Jeninnet.Localization.Core
dotnet add package Jeninnet.Localization.Providers.Json
```

</div>

لا تخلط حزمة محلية مع إصدار NuGet مستقبلي في الحل نفسه. الإصدار ثابت عمداً عند
<code dir="ltr">1.0.0-preview.1</code> أثناء المعاينة، ولذلك تصبح الحزمة المحلية القديمة خطراً حقيقياً. امسح
ذاكرة NuGet المؤقتة أو استخدم مجلد حزم جديداً عند إعادة بناء المخزن المحلي.

## اختيار الحزم (package selection)

هناك 35 مشروعاً قابلاً للتغليف. معظم التطبيقات تحتاج الثلاثي الأساسي، ثم مزوّد رسائل (message provider) واحداً
بالضبط، ثم التكاملات التي تستخدمها فعلاً.

### الثلاثي الأساسي

| الحزمة | متى تضيفها |
|---|---|
| <code dir="ltr">Jeninnet.Localization.Abstractions</code> | إذا كنت تنفّذ مزوّداً أو منسّقاً أو مخزناً أو أداة، وتحتاج العقود فقط. وهي اعتماد لكل ما يقارب بقية الحزم. |
| <code dir="ltr">Jeninnet.Localization.Core</code> | إذا أردت خط أنابيب الرسائل (message pipeline) والمُعرِّب (localizer) والمنسّقات (formatters) وجسر (bridge) <code dir="ltr">IStringLocalizer</code> المدعوم بالمخزن وتسجيل DI (Dependency Injection). هذا هو المرجع المعتاد للتطبيق. |
| <code dir="ltr">Jeninnet.Localization.Storage</code> | إذا كنت تنفّذ مزوّداً أو أداة تربط ملفات الكتالوج (catalog) بنموذج الرسائل القانوني. عادةً يصل إلى كود التطبيق بشكل غير مباشر. |

تعتمد <code dir="ltr">Core</code> على <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Storage</code>
تجميعة طرفية تحتوي عقود بيانات الرسائل. إضافة <code dir="ltr">Core</code> تمنحك خط الأنابيب،
لكنها لا تمنحك مصدر رسائل؛ يجب إضافة مزوّد واحد.

### مزوّدو الرسائل

| الحزمة | نموذج المصدر | الاستخدام النموذجي |
|---|---|---|
| <code dir="ltr">Jeninnet.Localization.Providers.Memory</code> | قاموس واحد مسطح <code dir="ltr">key -> template</code> | الاختبارات والنماذج الأولية والكتالوجات الصغيرة وتجميعات WASM (WebAssembly) |
| <code dir="ltr">Jeninnet.Localization.Providers.Json</code> | كائن JSON مسطح، ملف لكل ثقافة (culture) عند التسجيل متعدد الثقافات | كتالوجات ملفات بسيطة ومحتوى وقت البناء وإعادة تحميل (hot reload) اختيارية |
| <code dir="ltr">Jeninnet.Localization.Providers.Xml</code> | عناصر XML أسماؤها مفاتيح | خطوط محتوى XML موجودة وإعادة تحميل (hot reload) اختيارية |
| <code dir="ltr">Jeninnet.Localization.Providers.ResX</code> | مجموعات موارد <code dir="ltr">.resx</code> مضمّنة | استثمارات ResX الموجودة داخل تجميعة واحدة |
| <code dir="ltr">Jeninnet.Localization.Providers.Satellite</code> | موارد مترجمة مع حزم satellite | حزم موارد مسبقة الترجمة أو من طرف ثالث |
| <code dir="ltr">Jeninnet.Localization.Providers.Po</code> | نص Gettext PO (Portable Object) أو ثنائي GNU MO (Machine Object) | أدوات المترجمين والسياق <code dir="ltr">msgctxt</code> وصيغ الجمع |
| <code dir="ltr">Jeninnet.Localization.Providers.Pseudo</code> | قوالب مصدر محوّلة لثقافة اصطناعية | كشف الفائض النصي والنصوص غير المترجمة |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.SQLite</code> | جدول <code dir="ltr">LocalizationMessages</code> | تخزين علائقي محلي أو مضمّن |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.SqlServer</code> | جدول <code dir="ltr">LocalizationMessages</code> | كتالوج SQL Server مركزي |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.MySql</code> | جدول <code dir="ltr">LocalizationMessages</code> | كتالوج MySQL مركزي |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.PostgreSQL</code> | جدول <code dir="ltr">LocalizationMessages</code> | كتالوج PostgreSQL مركزي |
| <code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code> | مخزن مجموعات موارد فوق EF Core وSQL Server | مجموعات موارد (resource sets) مُدارة وتاريخ (history) ومزامنة (synchronization) |
| <code dir="ltr">Jeninnet.Localization.Translation.Azure</code> | Azure Translator v3 REST | ترجمة آلية (machine translation) وقت التشغيل مع تخزين مؤقت وتجميع |
| <code dir="ltr">Jeninnet.Localization.Translation.Google</code> | Google Translate v2 REST | ترجمة آلية وقت التشغيل بواجهة مختصرة |

تحتوي <code dir="ltr">Jeninnet.Localization.Providers.Database.Abstractions</code> على الأنواع
الأساسية المشتركة لمزوّدي قواعد البيانات. وهي اعتماد لمزوّدي قواعد البيانات الفعليين؛ لا تشر إليها
مباشرة إلا إذا كنت تكتب مزوّداً.

حزمة EF Core **ليست مزوّد رسائل**. إنها تنفّذ <code dir="ltr">ILocalizationStore</code>، لا
<code dir="ltr">IInnerMessageProvider</code>. إضافتها لا تمنح <code dir="ltr">IMessageLocalizer</code>
مصدر رسائل. هذا التمييز جوهري في النموذج الذهني، ومشروح في
[مجموعات الموارد والمخزن](../../en/02-guides/06-resource-sets-and-store.md).

### التكاملات والأدوات

| الحزمة | متى تضيفها |
|---|---|
| <code dir="ltr">Jeninnet.Localization.Integrations.Avalonia</code> | إذا كنت تشغّل تطبيق Avalonia مكتبياً وتحتاج حفظ الثقافة واتجاه RTL. |
| <code dir="ltr">Jeninnet.Localization.Integrations.Maui</code> | إذا كنت تشغّل تطبيق MAUI وتحتاج حفظ الثقافة في التفضيلات واتجاه RTL. |
| <code dir="ltr">Jeninnet.Localization.Integrations.WinUI</code> | إذا كنت تشغّل تطبيق WinUI 3 وتحتاج حفظ الثقافة في بيانات التطبيق واتجاه RTL في XAML. |
| <code dir="ltr">Jeninnet.Localization.Integrations.Mvc.Validation</code> | إذا احتجت بيانات تحقق وعرض لـ MVC، أو مُعرِّبات آمنة لـ HTML، أو تعريب العروض، أو tag helper باسم <code dir="ltr">&lt;loc&gt;</code>. |
| <code dir="ltr">Jeninnet.Localization.Integrations.RequestLocalization</code> | إذا أردت ثقافات الطلب مأخوذة من المخزن ومقصورة على الثقافات المخدومة. |
| <code dir="ltr">Jeninnet.Localization.Integrations.Blazor</code> | إذا احتجت إدارة ثقافة بنطاق الدائرة (circuit) أو النطاق الثابت (static scope)، وحماية مزوّد WASM. |
| <code dir="ltr">Jeninnet.Localization.Integrations.JavaScript</code> | إذا احتجت تقديم قوالب رسائل من قائمة سماح إلى عميل المتصفح. |
| <code dir="ltr">Jeninnet.Localization.Integrations.Diagnostics</code> | إذا أردت فحوص صحة (health checks) للمزوّد والمخزن. |
| <code dir="ltr">Jeninnet.Localization.Generators.Keys</code> | إذا أردت ثوابت مفاتيح وقت الترجمة مولّدة من كتالوجات <code dir="ltr">*.loc.json</code>. |
| <code dir="ltr">Jeninnet.Localization.Analyzers.Localizability</code> | إذا أردت اقتراحات وقت التأليف للنصوص الموجّهة للمستخدم والمكتوبة مباشرة في الكود. |
| <code dir="ltr">Jeninnet.Localization.Tools.Keys</code> | إذا أردت أداة التكافؤ (parity) <code dir="ltr">jeninnet-localization-keys</code> في CI. |
| <code dir="ltr">Jeninnet.Localization.Interchange.Abstractions</code> و<code dir="ltr">.Csv</code> و<code dir="ltr">.Po</code> و<code dir="ltr">.ResX</code> و<code dir="ltr">.Xliff</code> | إذا احتجت قراءة أو كتابة صيغ تبادل الترجمة. |

## إعداد المشروع

مشروع كونسول بسيط لا يحتاج أكثر من إعدادات SDK الافتراضية:

<div dir="ltr">

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Jeninnet.Localization.Core" Version="1.0.0-preview.1" />
    <PackageReference Include="Jeninnet.Localization.Providers.Memory" Version="1.0.0-preview.1" />
  </ItemGroup>
</Project>
```

</div>

إذا استخدمت مزوّد JSON مع ملفات تحت <code dir="ltr">Localization/</code>، فتأكد من وصولها إلى
مجلد الإخراج. الملف الموجود في شجرة المصدر فقط قد يجده <code dir="ltr">dotnet run</code> (لأنه
يستخدم مجلد المشروع كدليل عمل) ويغيب بعد النشر. حلّ المسار بشكل متسق، عادةً من
<code dir="ltr">AppContext.BaseDirectory</code>:

<div dir="ltr">

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

</div>

## أول تسجيل (registration) يعمل

البرنامج التالي يسجّل خط الأنابيب، ويسجّل مزوّد الذاكرة، ويحلّ المُعرِّب، وينسّق رسالة واحدة.
يستخدم صيغة العناصر النائبة (placeholders) بلا معاملات كما يدعمها المنسّق الافتراضي.

<div dir="ltr">

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;
using Microsoft.Extensions.DependencyInjection;

var messages = new Dictionary<string, string> {
    ["Greeting"] = "Hello, {name}!"
};

var services = new ServiceCollection();

// The pipeline: localizer, formatter, provider selector, and the store-backed bridge.
services.AddLocalization(options => options.DefaultCulture = "en");

// Exactly one message source.
services.AddMemoryLocalization(messages);

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var greeting = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(greeting); // Hello, Ada!
```

</div>

توجد طرق التسجيل في مساحات أسماء <code dir="ltr">Extensions</code> الخاصة بكل حزمة. طريقة خط
الأنابيب القانونية هي <code dir="ltr">AddLocalization</code>؛ أما طرق المزوّدين فتُسمى حسب
القدرة، مثل <code dir="ltr">AddMemoryLocalization</code> و
<code dir="ltr">AddJsonLocalization</code> و<code dir="ltr">AddPoLocalization</code> و
<code dir="ltr">AddSqlServerLocalization</code>.

تسجّل <code dir="ltr">AddLocalization()</code> دون وسائط كائن
<code dir="ltr">LocalizationOptions</code> افتراضياً مع
<code dir="ltr">DefaultCulture = "en"</code>. والتحميل الذي يقبل إعداداً هو البديل الصريح. لا
تستخدم الاثنين معاً: إذا كان كائن <code dir="ltr">LocalizationOptions</code> مسجلاً بالفعل، فإن
التحميل الذي يقبل إعداداً يرمي <code dir="ltr">InvalidOperationException</code> بدلاً من إنشاء
مسار تكوين ثانٍ بصمت.

يتجاهل مزوّد الذاكرة وسيطة الثقافة ويقدّم خريطة مسطحة واحدة. لذلك المثال عرض تنسيق، لا عرض
ثقافات. مزوّدات الملفات متعددة الثقافات مشروحة في
[البدء السريع](03-quick-start.md).

## ترتيب التسجيل والحل (resolution)

تسجّل <code dir="ltr">AddLocalization</code> كلاً من
<code dir="ltr">IMessageLocalizer</code> و<code dir="ltr">IMessageFormatter</code> و
<code dir="ltr">IMessageProvider</code> باستخدام <code dir="ltr">TryAddSingleton</code>. لذلك
يفوز أي منسّق مخصص مسجّل مسبقاً على المنسّق الافتراضي. أما
<code dir="ltr">IStringLocalizerFactory</code> المدعوم بالمخزن فيُسجّل باستخدام
<code dir="ltr">Replace</code>، لأن الإطار يجب ألا يحتفظ بمصنعه الافتراضي أمام الجسر.

يمكن أن يأتي تسجيل المزوّد قبل <code dir="ltr">AddLocalization</code> أو بعده؛ فالمحدّد يحلّ
خدمات <code dir="ltr">IInnerMessageProvider</code> المسجلة عند إنشائه. المهم أن يكون مزوّد واحد
بالضبط مسجلاً عند حلّ <code dir="ltr">IMessageProvider</code> أو
<code dir="ltr">IMessageLocalizer</code>. تسجيل مزوّدين ليس دمجاً: الحلّ يرمي
<code dir="ltr">InvalidOperationException</code> ويذكر أنواع المزوّدين التي حُلّت.

تسجّل <code dir="ltr">AddLocalization</code> أيضاً غلاف Microsoft
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> و<code dir="ltr">CustomStringLocalizerFactory</code>
الذي يقرأ من <code dir="ltr">ILocalizationStore</code>. تفشل هذه الخدمات عند الحلّ إن لم يكن هناك
مخزن مسجل. وإذا كنت تستخدم <code dir="ltr">IMessageLocalizer</code> وحده، فلا تحتاج إلى مخزن.

**مأزق:** تسجيل الملفات حريص على الوجود بالنسبة إلى JSON وXML وPO.
<code dir="ltr">AddJsonLocalization</code> و<code dir="ltr">AddPoLocalization</code> يتحققان من
المسار ويرميان <code dir="ltr">FileNotFoundException</code> أثناء التسجيل إذا لم يكن الملف موجوداً.
أما مزوّدا ResX وsatellite فلا يستطيعان التحقق من المورد المضمّن عند التسجيل؛ ويرمي أول بحث
<code dir="ltr">MissingManifestResourceException</code> إذا كان الاسم الأساسي خاطئاً. الفشل
المبكر أسهل في التشخيص عند بدء التشغيل، لكنه يعني أيضاً أن المحتوى يجب أن يكون في مكانه قبل
بناء مجموعة الخدمات.

## إلى أين بعد ذلك

يبني [البدء السريع](03-quick-start.md) تطبيقاً صغيراً متعدد الثقافات ويوضح الارتداد (fallback). وإذا كنت
تفضل فهم الآلية أولاً، فاقرأ [النموذج الذهني](04-mental-model.md). وللتفاصيل
حزمة بحزمة، راجع [خريطة الحزم](../appendix/02-package-map.md) و
[مرجع المزوّدين](../03-reference/03-provider-reference.md).

</div>
