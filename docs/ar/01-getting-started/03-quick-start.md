<div dir="rtl">

# 3. ابدأ بتطبيق صغير يوضح الارتداد (fallback)

أسرع طريقة لفهم الارتداد ليست حفظ تعريفه، بل بناء تطبيق صغير يجعل الثقافة (culture) الثانية تغيب عمداً.
سنبني تطبيق كونسول (console application) بثقافتين، ثم نطلب مفتاحاً (key) موجوداً في العربية، وآخر غائباً عنها، ونرى أي نص
يعود ولماذا.

## 1. أنشئ المشروع

<div dir="ltr">

```powershell
dotnet new console -n LocalizationDemo
cd LocalizationDemo
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
dotnet add package Jeninnet.Localization.Providers.Json --source ..\artifacts
```

</div>

المسار <code dir="ltr">--source</code> يشير إلى مخزن الحزم المحلي. عدّله إذا كان المخزن في مكان
آخر، واحذفه إذا كانت الحزم قد نُشرت.

## 2. اكتب كتالوجين (catalogs)

أنشئ مجلد <code dir="ltr">Localization</code> في جذر المشروع. ضع فيه ملفين: واحداً للإنجليزية
وواحداً للعربية.

<div dir="ltr">

```json
{
  "App.Title": "Bookstore",
  "Greeting": "Hello, {name}!",
  "Cart.Items": "You have {count} items."
}
```

</div>

<div dir="ltr">

```json
{
  "App.Title": "مكتبة",
  "Greeting": "مرحبا، {name}!"
}
```

</div>

لاحظ أن الكتالوج العربي لا يحتوي <code dir="ltr">Cart.Items</code>. هذا الغياب مقصود؛ هو ما
سيجعل الارتداد مرئياً بعد قليل. كل ملف كائن مسطح: المفاتيح نصوص، والقيم قوالب (templates). لا تداخل ولا
بيانات وصفية ولا تسلسل ثقافات داخل الملف.

تأكد من نسخ الملفين إلى مجلد الإخراج:

<div dir="ltr">

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

</div>

## 3. اربط الخط والمزوّد (provider)

استبدل <code dir="ltr">Program.cs</code> بهذا البرنامج:

<div dir="ltr">

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Json.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// The canonical pipeline. The default culture is the last fallback step.
services.AddLocalization(options => options.DefaultCulture = "en");

// One provider registration. The multi-culture overload wraps one JSON provider
// per culture in a CultureAwareMessageProvider.
services.AddJsonLocalizationProviders(
    AppContext.BaseDirectory,
    ["en", "ar"]);

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var noParameters = new Dictionary<string, object?>();

// Exact culture: English.
Console.WriteLine(await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en")));

// Exact culture: Arabic.
Console.WriteLine(await localizer.LocalizeAsync(
    "App.Title",
    noParameters,
    new CultureInfo("ar")));

// Missing in Arabic, present in the default culture: fallback to English.
Console.WriteLine(await localizer.LocalizeAsync(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = 3 },
    new CultureInfo("ar")));

// Switch the ambient UI culture and use the convenience overload.
CultureInfo.CurrentUICulture = new CultureInfo("ar");
Console.WriteLine(await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?> { ["name"] = "Ada" }));

// A complete miss returns null. No key, placeholder, or empty string is fabricated.
var missing = await localizer.LocalizeAsync("Missing.Key", noParameters, new CultureInfo("ar"));
Console.WriteLine(missing is null ? "(missing)" : missing);
```

</div>

## 4. شغّل ولاحظ

<div dir="ltr">

```powershell
dotnet run
```

</div>

الإخراج المتوقع:

<div dir="ltr">

```text
Hello, Ada!
مكتبة
You have 3 items.
مرحبا، Ada!
(missing)
```

</div>

الأسطر الأولى واضحة: طلبنا الإنجليزية فجاءت الإنجليزية، وطلبنا العربية فجاءت العربية. السطر الثالث
هو موضع السؤال: طلبنا <code dir="ltr">Cart.Items</code> بالعربية، لكن النص عاد إنجليزياً. والسطر
الأخير يوضح أن الغياب الكامل يعيد <code dir="ltr">null</code>، لا رسالة مصطنعة.

يستبدل <code dir="ltr">DefaultMessageFormatter</code> العناصر النائبة: <code dir="ltr">{name}</code>
يقابل إدخال <code dir="ltr">name</code>، و<code dir="ltr">{count}</code> يقابل
<code dir="ltr">count</code>. العنصر النائب غير الموجود يبقى كما كُتب. التفصيل في
[التنسيق والجمع](../../en/02-guides/04-formatting-and-plurals.md).

## 5. لماذا ظهر النص الإنجليزي؟

الكتالوج العربي لا يعرف <code dir="ltr">Cart.Items</code>. لذلك يمرّ البحث في
<code dir="ltr">ar</code> بالخطوات التالية:

1. يسأل المزوّد عن <code dir="ltr">Cart.Items</code> في <code dir="ltr">ar</code>. المزوّد مطابق (exact-match)
   تماماً، فلا يجد شيئاً ويعيد <code dir="ltr">null</code>.
2. أب <code dir="ltr">ar</code> هو الثقافة الثابتة (invariant culture). المُعرِّب (localizer) لا
   يستعلم عن الثابتة، فيتوقف مشي الآباء (parent cultures) هنا.
3. يسأل المزوّد عن المفتاح نفسه في الثقافة الافتراضية (default culture) <code dir="ltr">en</code>، فيجد القالب
   الإنجليزي.
4. ينسّق المُعرِّب القالب الإنجليزي بوسائط المستدعي.

النتيجة نص إنجليزي لطلب عربي. هذا هو الارتداد الموثّق، وليس خطأً في المزوّد ولا في التطبيق. إن أردت
اكتشافه، فاستخدم أدوات التكافؤ (parity) أو التعريب الزائف (pseudo-localization) أو التشخيص في
[التشخيص والمراقبة](../../en/02-guides/14-diagnostics-and-observability.md).

لنوسّع الجدول قليلاً حتى تصبح السلسلة ملموسة:

| الثقافة المطلوبة | المفتاح | الاستعلامات بالترتيب | ما يعود |
|---|---|---|---|
| <code dir="ltr">ar</code> | <code dir="ltr">App.Title</code> | <code dir="ltr">ar</code> | عنوان عربي |
| <code dir="ltr">ar</code> | <code dir="ltr">Cart.Items</code> | <code dir="ltr">ar</code>، <code dir="ltr">en</code> | قالب إنجليزي منسّق |
| <code dir="ltr">fr-CA</code> | <code dir="ltr">App.Title</code> | <code dir="ltr">fr-CA</code>، <code dir="ltr">fr</code>، <code dir="ltr">en</code> | عنوان إنجليزي |
| <code dir="ltr">ar</code> | <code dir="ltr">Missing.Key</code> | <code dir="ltr">ar</code>، <code dir="ltr">en</code> | <code dir="ltr">null</code> |

صف <code dir="ltr">fr-CA</code> يستحق التوقف. الفرنسية غير مسجلة، ومع ذلك يسأل المُعرِّب عن
<code dir="ltr">fr-CA</code> ثم <code dir="ltr">fr</code> ثم الثقافة الافتراضية. المزوّد لا يفعل
ذلك؛ المُعرِّب هو من يمشي في السلسلة.

وصف <code dir="ltr">Missing.Key</code> يوضح أن الغياب في العربية لا ينهي البحث. يسأل المُعرِّب عن
الإنجليزية رغم أن المفتاح غائب في العربية، لأن غيابه هناك لا يقول شيئاً عن الإنجليزية. النتيجة
<code dir="ltr">null</code>، لا استثناء.

## 6. غيّر الثقافة أثناء التشغيل

التحميل المريح (convenience overload) يستخدم <code dir="ltr">CultureInfo.CurrentUICulture</code>، لا
<code dir="ltr">CultureInfo.CurrentCulture</code>. الفرق عملي: ثقافة الواجهة تختار الموارد،
والثقافة تتحكم في تنسيق الأرقام والتواريخ. في تطبيق ويب يضبط وسيط تعريب الطلب (request-localization middleware) الاثنين لكل طلب. في
تطبيق كونسول تضبط ثقافة الواجهة مباشرة:

<div dir="ltr">

```csharp
CultureInfo.CurrentUICulture = new CultureInfo("ar");
```

</div>

إذا كان الاستدعاء من مهمة خلفية أو خدمة طويلة العمر، فاستخدم التحميل الصريح للثقافة. الثقافة
المحيطة قد تتغير بين عمليتين غير متزامنتين، والبحث يجب أن يستخدم الثقافة التي قصدها المستدعي.

## 7. ما الذي ركّبه المثال؟

سجّل <code dir="ltr">AddLocalization</code> خط الأنابيب (pipeline): <code dir="ltr">DefaultMessageLocalizer</code>
و<code dir="ltr">DefaultMessageFormatter</code> و<code dir="ltr">LocalizationProviderSelector</code>
وجسر مُعرِّب Microsoft المدعوم بالمخزن.

ثم أنشأ <code dir="ltr">AddJsonLocalizationProviders</code> مزوّد
<code dir="ltr">JsonMessageProvider</code> واحداً لكل ثقافة، وغلّفها في
<code dir="ltr">CultureAwareMessageProvider</code> واحد سُجّل كـ
<code dir="ltr">IInnerMessageProvider</code> الوحيد.

أما <code dir="ltr">DefaultMessageLocalizer</code> فنفّذ سلسلة الارتداد (fallback chain)، ولم يستدع المنسّق (formatter) إلا
بعد أن وجد قالباً.

جسر المخزن (store bridge) سُجّل في هذا المثال لكنه لم يُستخدم. يحتاج إلى
<code dir="ltr">ILocalizationStore</code>، وهو مسار
<code dir="ltr">IStringLocalizer</code> وبيانات تحقق MVC وواجهات الإدارة. ليس مسار
<code dir="ltr">IMessageLocalizer</code>.

## 8. أخطاء تمنع الفهم

**لا تضف مزوّداً ثانياً لتغطية الفجوة العربية.** المزوّدان لا يندمجان. حلّ
<code dir="ltr">IMessageProvider</code> يرمي <code dir="ltr">InvalidOperationException</code>.
املأ الكتالوج، أو غيّر المزوّد الواحد، أو وحّد المصادر قبل وقت التشغيل.

**لا تفترض أن اسم الملف حر.** في اصطلاح تعدد الثقافات (multi-culture)، <code dir="ltr">ar.json</code> هو الثقافة
<code dir="ltr">ar</code>، و<code dir="ltr">ar-SY.json</code> هو الثقافة
<code dir="ltr">ar-SY</code>. يجرّب الموزّع (dispatcher) الاسم الكامل أولاً ثم اسم اللغة المكوّن من حرفين، لكنه
لا يمشي في الآباء. هذا يبقى عمل المُعرِّب. التفصيل في
[الثقافات والارتداد](../../en/02-guides/03-cultures-and-fallback.md).

**لا تخلط بين "الصفحة ظهرت" و"الترجمة اكتملت".** الارتداد قد يجعل الصفحة تعمل بينما نصف مفاتيحها
بالإنجليزية. استخدم أدوات التكافؤ والاختبارات لكشف ذلك.

مثال JSON في هذه الصفحة يُجمّع ويُنفّذ بواسطة
[<code dir="ltr">samples/Jeninnet.Localization.Docs</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Docs/README.md)
ضمن الحل وCI، لذلك يُتحقق من المقطع مقابل الحزم الحقيقية.

## 9. إلى أين بعد ذلك؟

- [النموذج الذهني](04-mental-model.md) لدورة البحث الكاملة وقاعدة المزوّد
  الواحد.
- [اختيار مزوّد](../../en/02-guides/01-choosing-a-provider.md) قبل تثبيت JSON كخيار إنتاجي.
- [مجموعات الموارد والمخزن](../../en/02-guides/06-resource-sets-and-store.md) إذا احتجت كتالوجات قابلة
  للتعديل وقت التشغيل؛ فهو مسار مختلف عن خط الرسائل.
- [المفاتيح المكتوبة بقوة](../../en/02-guides/05-strongly-typed-keys.md) إذا أردت ثوابت مفاتيح وقت
  الترجمة.

</div>
