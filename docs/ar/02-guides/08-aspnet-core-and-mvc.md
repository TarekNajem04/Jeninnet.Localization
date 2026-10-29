<div dir="rtl">

# ASP.NET Core وMVC

يستطيع تطبيق ASP.NET Core استهلاك التعريب عبر سطحين. يعيد خط أنابيب الرسائل سلاسل منسّقة من
<code dir="ltr">IMessageLocalizer</code>؛ ويعيد جسر مجموعات الموارد قيم
<code dir="ltr">LocalizedString</code> من <code dir="ltr">IStringLocalizer</code> و
<code dir="ltr">IStringLocalizer&lt;T&gt;</code>. وتستخدم MVC وDataAnnotations وعروض Razor
وبيانات التحقق الوصفية السطح الثاني. يربط هذا الفصل السطحين، ويشرح اختيار ثقافة الطلب، ويكشف
مآزق العمر (lifetime traps) خلف إخفاقات "يعمل محلياً، لكن اللغة خاطئة في الإنتاج".

## السطحان

يملك <code dir="ltr">IMessageLocalizer</code> ارتداد الثقافة، واستعلامات المزوّد المطابقة تماماً،
والتنسيق عبر <code dir="ltr">IMessageFormatter</code>. ولا يعرف شيئاً عن مجموعات الموارد أو MVC
أو <code dir="ltr">IStringLocalizer</code>.

<code dir="ltr">IStringLocalizer</code> هو عقد Microsoft.Extensions.Localization. وهنا يسنده
<code dir="ltr">CustomStringLocalizerFactory</code> فوق
<code dir="ltr">ILocalizationStore</code>. يربط المصنع اسم نوع (أو اسماً أساسياً صريحاً) باسم
مجموعة موارد، ويلتقط ثقافة واجهة المستخدم الحالية عند إنشاء المُعرِّب، ويعيد فحص المخزن في كل بحث.
ولا يمشي في ثقافات الآباء، ويعيد <code dir="ltr">[MISSING:key]</code> مع
<code dir="ltr">ResourceNotFound = true</code> للإدخال الغائب.

لا يتشارك السطحان البيانات. أبقِ الكتالوجين متوافقين عن قصد. راجع
[مجموعات الموارد والمخزن](06-resource-sets-and-store.md).

## تركيب عامل

<div dir="ltr">

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddLocalization(options => options.DefaultCulture = "en");
builder.Services.AddEntityFrameworkLocalizationStore(
    builder.Configuration, connectionStringName: "LocalizationDb");
builder.Services.AddMvcLocalizedValidation();
builder.Services.AddLocalizationRequestCultures();

var app = builder.Build();
app.UseRequestLocalization();
app.UseRouting();
app.MapDefaultControllerRoute();
app.Run();
```

</div>

تسجّل <code dir="ltr">AddLocalization</code> خط الأنابيب والمنسّق ومحدد المزوّد ومصنع
<code dir="ltr">IStringLocalizerFactory</code> المسنود بالمخزن. لكنها لا تسجّل
<code dir="ltr">ILocalizationStore</code>؛ وتسجيل المخزن أعلاه هو ما يجعل
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> قابلاً للحل. كما تستبدل
<code dir="ltr">AddMvcLocalizedValidation</code> المصنع كشبكة أمان، لكنها لا تنشئ مخزناً.

إذا كان التطبيق يستخدم <code dir="ltr">IMessageLocalizer</code> فقط، فلا يحتاج إلى مخزن. وإضافة
<code dir="ltr">IStringLocalizer</code> هي النقطة التي يصبح فيها المخزن إلزامياً.

## اختيار ثقافة الطلب

تملأ <code dir="ltr">AddLocalizationRequestCultures</code> خيارات وسيط الإطار:

- **الثقافات المخدومة** من
  <code dir="ltr">LocalizationRequestCultureOptions.SupportedCultures</code> الصريحة عند تعيينها؛
  وإلا من <code dir="ltr">ILocalizationStore.GetSupportedCulturesAsync()</code>. وتُضم الثقافة
  الافتراضية إلى المجموعة دائماً.
- **ثقافة الطلب الافتراضية** من
  <code dir="ltr">LocalizationRequestCultureOptions.DefaultCulture</code>، ثم
  <code dir="ltr">LocalizationOptions.DefaultCulture</code> المسجّلة، ثم
  <code dir="ltr">"en"</code>.
- **المزوّدات** بترتيب قيمة المسار، ثم سلسلة الاستعلام، ثم ملف تعريف الارتباط، ثم
  <code dir="ltr">Accept-Language</code>. ومصدر المسار هو
  <code dir="ltr">RouteValueRequestCultureProvider</code> بمفتاح قابل للتكوين (افتراضياً
  <code dir="ltr">"culture"</code>).
- **التضييق** عبر <code dir="ltr">ServedCultureRequestCultureProvider</code>: الاسم المخدوم
  الدقيق أولاً، ثم ارتداد الآباء المحدود المكوَّن في الإطار.

يقرأ الجسر المخزن، لا مزوّد الرسائل. فالتطبيق الذي يملك كتالوجات JSON لـ
<code dir="ltr">en</code> و<code dir="ltr">ar</code> و<code dir="ltr">de</code> بلا مخزن يظل
يعمل، لكن المجموعة المخدومة تنكمش إلى الثقافة الافتراضية ويُتجاهل
<code dir="ltr">Accept-Language: ar</code>. سجّل المخزن، أو صرّح بالثقافات المخدومة صراحةً.

المخزن المتاح الذي يبلّغ عن صفر ثقافات يفشل بدء التشغيل. والمخزن غير المتاح أو الغائب يتراجع إلى
الثقافة الافتراضية وحدها ويسجّل تحذيراً عند توفر التسجيل. واسم الثقافة غير المعروف يفشل بدء
التشغيل. والثقافة الافتراضية الغائبة من المجموعة المخدومة تُضاف إليها.

أبقِ <code dir="ltr">app.UseRequestLocalization()</code> في خط الأنابيب. فالجسر يكوّن الوسيط؛
ولا يثبّته.

## جسر IStringLocalizer

يربط المصنع نوعاً باسم مجموعة موارد عبر <code dir="ltr">Type.Name</code>:

<div dir="ltr">

```csharp
public class HomeController : Controller {
    private readonly IStringLocalizer<HomeController> _localizer;
    public HomeController(IStringLocalizer<HomeController> localizer) => _localizer = localizer;
    public IActionResult Index() => View(_localizer["Home.Title"]);
}
```

</div>

يحل البحث مجموعة الموارد <code dir="ltr">HomeController</code> لثقافة واجهة المستخدم الملتقطة عند
الإنشاء. ويستخدم <code dir="ltr">Create(baseName, location)</code> الاسم الأساسي حرفياً ويتحقق من
<code dir="ltr">location</code> فقط؛ فالموقع لا يحدّ نطاق البحث. واسم مورد Microsoft المؤهل بمساحة
أسماء لا يُربط تلقائياً.

الغلاف العام (generic wrapper) عابر. يحصل على نتيجة مصنع جديدة لكل حل، لذا حقن
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> في متحكم أو صفحة Razor آمن: يُنشأ المُعرِّب بعد
أن يطبّق تعريب الطلب ثقافة الطلب. والمصنع والمخزن singleton.

**مأزق:** حقن <code dir="ltr">IStringLocalizer&lt;T&gt;</code> في singleton. التقط المُعرِّب ثقافة
الطلب الأول، ويخدم الـ singleton كل الطلبات. حلّه لكل طلب، أو مرّر الثقافة صراحةً إلى
<code dir="ltr">IMessageLocalizer</code>.

**مأزق:** حل <code dir="ltr">IStringLocalizer&lt;T&gt;</code> بلا
<code dir="ltr">ILocalizationStore</code>. قد يكون خط الأنابيب مسجّلاً بالكامل ومع ذلك يفشل هنا.

## تكامل DataAnnotations

تحتوي <code dir="ltr">Integrations.DataAnnotations</code> بدائل معرَّبة لسمات التحقق القياسية:
<code dir="ltr">LocalizedRequiredAttribute</code> و
<code dir="ltr">LocalizedStringLengthAttribute</code> و
<code dir="ltr">LocalizedRangeAttribute</code> و
<code dir="ltr">LocalizedRegularExpressionAttribute</code> و
<code dir="ltr">LocalizedCompareAttribute</code> و
<code dir="ltr">LocalizedCustomValidationAttribute</code>، وبقية العائلة. تأخذ كل سمة مفتاح
تعريب وتحلّه وقت التحقق عبر <code dir="ltr">IStringLocalizerFactory</code> الموجود على
<code dir="ltr">ValidationContext</code>.

<div dir="ltr">

```csharp
using System.ComponentModel.DataAnnotations;
using Jeninnet.Localization.Integrations.DataAnnotations;

public class CheckoutModel {
    [LocalizedRequired("Checkout.Name.Required")]
    public string Name { get; set; } = string.Empty;

    [LocalizedStringLength(3, 40, "Checkout.Name.Length")]
    public string DisplayName { get; set; } = string.Empty;
}
```

</div>

البحث هو مفهرس <code dir="ltr">IStringLocalizer</code> المسنود بالمخزن. وتستخدم الوسائط تنسيقاً
مركباً: توفّر <code dir="ltr">LocalizedStringLength</code> العنصر
<code dir="ltr">{0}</code> (الحد الأدنى) و<code dir="ltr">{1}</code> (الحد الأقصى)، ويفعل
<code dir="ltr">LocalizedRangeAttribute</code> الشيء نفسه لحدوده. ويجب أن تستخدم إدخالات الكتالوج
<code dir="ltr">{0}</code>/<code dir="ltr">{1}</code>، لا العناصر النائبة المسمّاة.

تعتمد الحزمة على BCL (Base Class Library) وDataAnnotations و
<code dir="ltr">Microsoft.Extensions.Localization.Abstractions</code> فقط. ومع ذلك يتطلب التركيب
وقت التشغيل المصنع والمخزن. وعند غياب التركيب، ترمي السمة
<code dir="ltr">InvalidOperationException</code> بدلاً من التراجع إلى نص مضمّن: إنه خطأ تكوين،
لا غياب تعريب.

## تحقق MVC وبيانات العرض الوصفية

تسجّل <code dir="ltr">AddMvcLocalizedValidation</code> مزوّدي بيانات وصفية بعد مزوّد
DataAnnotations في الإطار:

- يربط <code dir="ltr">LocalizedValidationMetadataProvider</code> رسالة معرَّبة بـ
  <code dir="ltr">RequiredAttribute</code> إطاري عادي بلا
  <code dir="ltr">ErrorMessage</code>، باستخدام اسم الخاصية كمفتاح. والسمات المشتقة من
  <code dir="ltr">RequiredAttribute</code>، بما فيها العائلة المعرَّبة، لا تُمس لأنها تملك
  تعريبها.
- يعرّب <code dir="ltr">LocalizedDisplayMetadataProvider</code> الخصائص
  <code dir="ltr">DisplayAttribute.Name</code> و<code dir="ltr">Description</code> و
  <code dir="ltr">Prompt</code>. والقيم الحرفية للسمة هي المفاتيح. ويعطّل
  <code dir="ltr">DisplayAttribute.ResourceType</code> المزوّد لتلك السمة، حفاظاً على آلية
  مدير الموارد المدمجة.

ترتيب التسجيل مهم: يجب إلحاق المزوّدين المعرَّبين بعد
<code dir="ltr">AddControllers()</code> أو <code dir="ltr">AddMvcCore()</code> كي يعملا بعد
مزوّد الإطار.

**بيانات العرض الوصفية مؤجَّلة.** تنشئ مفوّضات البيانات الوصفية مُعرِّباً جديداً في كل مرة تُستهلك
فيها القيمة، لذا تبقى بيانات MVC الوصفية المخزّنة صحيحة الثقافة عبر الطلبات. أما **بيانات
<code dir="ltr">RequiredAttribute</code> الإطارية فغير مؤجَّلة.** تُربط الرسالة عند إنشاء MVC
للبيانات الوصفية، وهي بيانات يخزّنها MVC طوال عمر التطبيق؛ فتعكس ثقافة واجهة المستخدم عند أول
إنشاء. استخدم عائلة DataAnnotations المعرَّبة عندما يجب أن تتبع رسائل التحقق كل ثقافة طلب.

لا وجهة لـ <code dir="ltr">DisplayAttribute.ShortName</code> في نموذج بيانات العرض الوصفية في
MVC على إصدارات ASP.NET Core المدعومة. ولا يمكن تعريبه عبر خط الأنابيب هذا؛ وهذا قيد إطار.

## عروض Razor

تستبدل <code dir="ltr">AddHtmlMessageLocalizer()</code> الواجهتين
<code dir="ltr">IHtmlLocalizer</code> و<code dir="ltr">IHtmlLocalizerFactory</code> بجسور آمنة
لـ HTML فوق خط الأنابيب. يمرّر ترميز القالب كمحتوى مطوّر موثوق؛ وتُرمَّز قيم الوسائط عبر
<code dir="ltr">HtmlSafeMessageFormatter.EncodeArgument</code>. ويتبع المفتاح المفقود اصطلاح
الإطار: القيمة هي الاسم و<code dir="ltr">IsResourceNotFound</code> صحيح. وترمي
<code dir="ltr">GetAllStrings</code> الاستثناء <code dir="ltr">NotSupportedException</code> لأن
خط أنابيب الرسائل لا يعدّد المفاتيح.

تستبدل <code dir="ltr">AddViewMessageLocalizer()</code> الواجهة
<code dir="ltr">IViewLocalizer</code>. ويصبح مسار العرض المنفّذ سياق gettext
(<code dir="ltr">/Views/Catalog/Index.cshtml</code> يصبح
<code dir="ltr">Views.Catalog.Index</code>). ويُحل المفتاح بنطاق العرض أولاً، ثم المشترك ثانياً،
كلٌّ عبر سلسلة الارتداد المرجعية. ويفوز
<code dir="ltr">ViewContext.ExecutingFilePath</code> على
<code dir="ltr">ViewContext.View.Path</code>، لذا تُحل العروض الجزئية والعروض ذات السمة أو
الموسّعة بالمنطقة تحت موقعها. وعمليات البحث الجمعية والسياقية ليست جزءاً من سطح
<code dir="ltr">IViewLocalizer</code> وترمي.

يعرّب مساعد الوسم <code dir="ltr">&lt;loc&gt;</code> محتوى مضمّناً بلا بحث في code-behind:

<div dir="ltr">

```razor
@addTagHelper *, Jeninnet.Localization.Integrations.Mvc.Validation

<loc key="Cart.Title">Shopping Cart</loc>
<loc key="Greeting" arguments="@(new object[] { userName })">Hello {0}!</loc>
```

</div>

عند الغياب يُعرض متن العنصر كبديل احتياطي. ويُزال عنصر <code dir="ltr">&lt;loc&gt;</code>
وخصائصه من الناتج. ويحتاج المساعد إلى <code dir="ltr">AddLocalization()</code> ومزوّد، لا إلى
<code dir="ltr">AddHtmlMessageLocalizer()</code>.

## مثال عملي: من الطلب إلى رسالة التحقق

يصل طلب يحمل <code dir="ltr">Accept-Language: ar-SY</code>. يختار الوسيط
<code dir="ltr">ar-SY</code>. ويحقن متحكم
<code dir="ltr">IStringLocalizer&lt;CheckoutController&gt;</code>؛ فيلتقط الغلاف العابر
<code dir="ltr">ar-SY</code>. ولا يملك المخزن إدخال
<code dir="ltr">CheckoutController</code>/<code dir="ltr">ar-SY</code> ولا ارتداداً، لذا يعيد
المفهرس <code dir="ltr">[MISSING:Checkout.Title]</code>. وتظهر العلامة في التدريج والمراجعة.

يتحقق نشر نموذج من حقلين. تحل
<code dir="ltr">LocalizedRequiredAttribute("Checkout.Name.Required")</code> المخزن نفسه بدقة
لـ <code dir="ltr">ar-SY</code>. ويُنتج الإدخال المفقود
<code dir="ltr">[MISSING:Checkout.Name.Required]</code>. وتبلّغ أداة التكافؤ عن الفجوة قبل
النشر. راجع [التكافؤ وبوابات الجودة](../../en/02-guides/13-parity-and-quality-gates.md).

وإذا أراد الفريق ارتداد ثقافة الأب، فعليه تخزين إدخالات لكل ثقافة مخدومة أو تمرير تلك السلاسل
عبر <code dir="ltr">IMessageLocalizer</code>.

## مآزق

**مأزق:** افتراض أن <code dir="ltr">AddLocalization()</code> تسجّل مخزناً أيضاً. إنها لا تفعل
ذلك؛ ويفشل حل <code dir="ltr">IStringLocalizer&lt;T&gt;</code> حتى يُسجَّل
<code dir="ltr">ILocalizationStore</code>.

**مأزق:** حقن <code dir="ltr">IStringLocalizer&lt;T&gt;</code> في singleton. لقد التقط ثقافة
الطلب الأول؛ حلّه لكل طلب.

**مأزق:** نداء طريقتي <code dir="ltr">AddLocalization()</code> دون التحكم في الترتيب. تسجيل المصنع
استبدال، لا دمج. أبقِ مالكاً واحداً. راجع
[الترحيل من Microsoft.Extensions.Localization](../03-reference/08-migration-from-ms-extensions.md).

## إلى أين بعد ذلك

- [مجموعات الموارد والمخزن](06-resource-sets-and-store.md) للجسر ومخزن EF.
- [Blazor](09-blazor.md) لنطاقات الدائرة والعميل الساكن.
- [RTL (Right-to-Left) والنص ثنائي الاتجاه](16-rtl-and-bidi.md) للاتجاه وCSS المنطقي وعزل
  الاتجاه.
- [واجهة التخزين](../../en/03-reference/02-storage-api.md) لسطح المخزن الكامل.

</div>
