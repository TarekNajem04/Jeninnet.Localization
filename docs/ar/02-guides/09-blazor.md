<div dir="rtl">

# Blazor

يغيّر Blazor معنى "الثقافة الحالية". فدائرة الخادم (server-side circuit) جلسة طويلة العمر
تتعدد مع جلسات كثيرة أخرى في العملية نفسها؛ ومضيف العميل عملية لمستخدم واحد تكون فيها الثقافة
المحيطة حالة تطبيق. ولا يمكن إعادة استخدام آلية ثقافة تعمل لطلب واحد كما هي في أي من الحالتين.

يجعل التكامل النطاق صريحاً. يحتفظ وضع الخادم بالثقافة لكل دائرة ولا يمس الثقافة المحيطة للعملية
أبداً. ويطبّق الوضع الساكن الثقافة المحيطة ويرفع حدثاً كي تعيد المكوّنات العرض. وخط أنابيب الرسائل
لا يُمس؛ ويبقى الارتداد في <code dir="ltr">DefaultMessageLocalizer</code>.

## اختيار النطاق

تختار <code dir="ltr">BlazorLocalizationOptions.Mode</code> النطاق:

- **<code dir="ltr">BlazorLocalizationMode.Server</code>** لـ Blazor Server ومكوّنات Server
  التفاعلية. خدمة الثقافة محدودة النطاق، نسخة واحدة لكل دائرة. يبذرها معالج الدائرة من ثقافة
  الطلب عند فتح الدائرة. ولا تُعيَّن الثقافة المحيطة أبداً، فلا تتسرب الدوائر المتزامنة إلى
  بعضها.
- **<code dir="ltr">BlazorLocalizationMode.Wasm</code>** لمضيفات العميل وBlazor Hybrid. التسجيل
  الافتراضي singleton ويطبّق <code dir="ltr">CultureInfo.CurrentCulture</code> و
  <code dir="ltr">CurrentUICulture</code> وخاصيتي
  <code dir="ltr">DefaultThreadCurrent*</code>. وهذا آمن لأن العملية تخدم مستخدماً واحداً. أما في
  WebView2 فسجّل الخدمة والمخزن محدودي النطاق بدلاً من ذلك (انظر قسم Hybrid)، لأن وقت تشغيل
  JavaScript يتبع نطاق WebView.

**ملاحظة:** تتطلب <code dir="ltr">AddBlazorWasmLocalization</code> إضافةً إلى ذلك أن يعلن كل
مزوّد مسجّل <code dir="ltr">IWasmCompatibleMessageProvider</code>. ولمضيف Blazor Hybrid على سطح
المكتب، استخدم
<code dir="ltr">AddBlazorLocalization(options => options.Mode = BlazorLocalizationMode.Wasm)</code>؛
فالنطاق نفسه، ويختلف الحارس فقط.

## تسجيل الخادم وعمر الدائرة

<div dir="ltr">

```csharp
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
services.AddRazorComponents().AddInteractiveServerComponents();

services.AddBlazorServerLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});

services.AddScoped<IBlazorCultureStore, BlazorCookieCultureStore>();

var app = builder.Build();
app.UseRequestLocalization();
```

</div>

يسجّل التركيب <code dir="ltr">BlazorCultureService</code> محدود النطاق و
<code dir="ltr">BlazorCircuitCultureHandler</code> كـ <code dir="ltr">CircuitHandler</code>.
سجّل <code dir="ltr">BlazorCookieCultureStore</code> محدود النطاق أيضاً؛ لأنه يعتمد على
<code dir="ltr">IJSRuntime</code> محدود النطاق، لذا يفشل تسجيل singleton في التحقق من DI (Dependency Injection).
ويقرأ المعالج <code dir="ltr">IRequestCultureFeature</code> من طلب HTTP للاتصال ويبذر خدمة
الدائرة. وقد حلّ <code dir="ltr">RequestLocalizationMiddleware</code> ثقافة الطلب من ملف تعريف
ارتباط الثقافة، وهي الآلية الوحيدة التي تنجو من ترقية WebSocket.

البذر متساهل عمداً. تُتجاهل ثقافة الطلب غير المعروفة أو غير المدعومة؛ ثم تستخدم الدائرة
<code dir="ltr">DefaultCulture</code> أو الثقافة المحيطة. ويجب ألا يفشل ملف ارتباط سيئ اتصالاً.

تكون ثقافة الدائرة ثابتة فعلياً طوال عمر الدائرة. تحفظ
<code dir="ltr">SetCultureAsync</code> الاختيار عبر
<code dir="ltr">IBlazorCultureStore</code>، ويعيد المضيف تحميل الصفحة كي يطبّق خط أنابيب الطلب
الثقافة الجديدة على المحتوى المعروض من الخادم. لذا ينشئ تبديل الثقافة دائرة جديدة.

<div dir="ltr">

```razor
@inject BlazorCultureService CultureService
@inject NavigationManager Navigation

<select @onchange="OnCultureChangedAsync">
    @foreach (var culture in ReferenceCultures.SupportedCultures) {
        <option value="@culture" selected="@(CultureService.Culture == culture)">@culture</option>
    }
</select>

@code {
    private async Task OnCultureChangedAsync(ChangeEventArgs args) {
        var culture = args.Value?.ToString();
        if (!string.IsNullOrEmpty(culture)) {
            await CultureService.SetCultureAsync(culture);
            Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
        }
    }
}
```

</div>

تكون <code dir="ltr">CultureService.Culture</code> مساوية <code dir="ltr">null</code> حتى تُطبَّق
ثقافة. وتحل <code dir="ltr">CultureService.CultureInfo</code> قيمة
<code dir="ltr">DefaultCulture</code> أو ثقافة واجهة المستخدم المحيطة، وهذا يجعلها آمنة
لاستخدامها في خصائص <code dir="ltr">dir</code> و<code dir="ltr">lang</code> قبل البذر.

## مخزن الخادم

يكتب <code dir="ltr">BlazorCookieCultureStore</code> ملف ارتباط RequestLocalization عبر
<code dir="ltr">CookieRequestCultureProvider.MakeCookieValue</code> ويقرأه بـ
<code dir="ltr">ParseCookieValue</code>. لذا يقرأ مزوّد ملف الارتباط القياسي ما يكتبه المخزن
بالضبط. واسم ملف الارتباط افتراضياً
<code dir="ltr">CookieRequestCultureProvider.DefaultCookieName</code>؛ والعمر الافتراضي 365 يوماً.

يجب أن يسجّل المضيف مخزناً. وبدونه يسجّل التركيب مخزناً داخلياً بلا أثر (no-op)، ويبقى الاختيار
في ذاكرة العملية فقط، أي أنه يزول مع الدائرة. والحزمة لا تختار التخزين للمضيف عمداً.

تطبّق <code dir="ltr">BlazorCultureService.SetCultureAsync</code> الثقافة أولاً، ثم تكتب إلى
المخزن. وإذا فشلت الكتابة، تكون الثقافة في الذاكرة قد تغيّرت بالفعل. تعامل مع فشل الحفظ كفشل
طلب ودع مسار إعادة تحميل الصفحة يتعافى؛ لا تبلّغ عن النجاح بناءً على عودة الدالة.

## المضيفات الهجينة والعميل

<div dir="ltr">

```csharp
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
services.AddBlazorLocalization(options => {
    options.Mode = BlazorLocalizationMode.Wasm;
    options.DefaultCulture = "en";
    options.DetectBrowserLanguage = true;
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});

// WebAssembly: the JavaScript runtime is a singleton.
services.AddSingleton<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

</div>

في Blazor Hybrid/WebView2 يأتي <code dir="ltr">IJSRuntime</code> من نطاق WebView. سجّل الخدمة
والمخزن محدودي النطاق كي لا تلتقط الخدمة وقت تشغيل JavaScript الجذري:

<div dir="ltr">

```csharp
services.AddScoped<BlazorCultureService>();
services.AddScoped<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

</div>

يقرأ مخزن التخزين المحلي مفتاحاً واحداً ويكتبه، هو
<code dir="ltr">CultureStorageKey</code> (افتراضياً
<code dir="ltr">jeninnet.localization.culture</code>). نادِ
<code dir="ltr">InitializeAsync</code> مرة واحدة بعد أول عرض؛ فتفاعل JavaScript غير متاح قبل
ذلك في مضيفات العميل.

<div dir="ltr">

```razor
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await CultureService.InitializeAsync();
        }
    }
}
```

</div>

تقرأ <code dir="ltr">InitializeAsync</code> الاختيار المخزّن وتطبّقه عندما يختلف عن الحالي. ومع
تفعيل <code dir="ltr">DetectBrowserLanguage</code>، تتراجع بعد ذلك إلى تفاوض المتصفح:
<code dir="ltr">navigator.languages</code>، ثم <code dir="ltr">navigator.language</code>،
مقابل <code dir="ltr">SupportedCultures</code> بالوسم الدقيق أولاً ثم سلسلة آباء وسم المتصفح
(<code dir="ltr">de-DE</code> إلى <code dir="ltr">de</code>). الكشف للنطاق الساكن فقط،
وللقراءة فقط، وبأفضل جهد؛ وفشل التفاعل أو قائمة فارغة يعني عدم الكشف، لا استثناءً. وتقبل قائمة
الدعم الفارغة أول تفضيل متصفح معروف. ولا يُحفظ اختيار الكشف أبداً، لذا يحتفظ المخزن باختيارات
المستخدم الصريحة فقط.

اشترك في <code dir="ltr">CultureChanged</code> لإعادة العرض؛ ويحمل الحدث الاسم المطبَّق و
<code dir="ltr">CultureInfo</code>.

## المكوّنات الديناميكية وخط الأنابيب

التكامل محوّل (adapter) فوق <code dir="ltr">CultureInfo</code> وDI و
<code dir="ltr">IJSRuntime</code>؛ ولا يلف خط أنابيب المزوّد. في وضع الخادم ينسّق خط الأنابيب
بالثقافة التي يمرّرها المستدعي. ويستخدم التحميل المريح لـ
<code dir="ltr">IMessageLocalizer</code> ثقافة واجهة المستخدم المحيطة، وهي التي لا يغيّرها وضع
الخادم عمداً، لذا يمرّر المكوّن الذي يريد ثقافة الدائرة
<code dir="ltr">CultureService.CultureInfo</code>:

<div dir="ltr">

```csharp
var text = await Localizer.LocalizeAsync("Cart.Items", parameters, CultureService.CultureInfo);
```

</div>

قراءة خدمة الدائرة رخيصة وصريحة؛ أما تعديل الثقافة المحيطة فسيقايض الصحة بالراحة.

<code dir="ltr">IStringLocalizer&lt;T&gt;</code> مختلف: يلتقط جسر المخزن ثقافة واجهة المستخدم
المحيطة عند الإنشاء. وفي وضع الخادم تكون تلك ثقافة الطلب، وهي القيمة الصحيحة للمحتوى المعروض من
الخادم. لا تحتفظ بمثل هذا المُعرِّب في singleton؛ حلّه لكل نطاق مكوّن أو استخدم خط الأنابيب مع
ثقافة الدائرة. راجع [ASP.NET Core وMVC](08-aspnet-core-and-mvc.md).

## حارس مزوّد WASM

لا يستطيع WebAssembly في المتصفح تنفيذ إدخال/إخراج متزامن للملفات أو قواعد البيانات، وكثير من
المزوّدات الموجهة للخادم سيفشل عند أول بحث. تفرض
<code dir="ltr">AddBlazorWasmLocalization</code> مجموعة المزوّدين المسموح بها وقت التسجيل:

- يجب أن ينفّذ كل <code dir="ltr">IInnerMessageProvider</code> مسجّل
  <code dir="ltr">IWasmCompatibleMessageProvider</code>.
- لا يمكن التحقق من المزوّدات المسجّلة عبر مصنع، وتُرفض بحسب الوصف.
- يعلن مزوّدا Memory وPseudo العلامة؛ ولا تعلنها مزوّدات الملفات وقواعد البيانات وResX
  وsatellite والترجمة.
- سجّل المزوّد قبل نداء التركيب؛ فالحارس يفحص مجموعة الخدمات.

**ملاحظة:** تعلن الحزمة حالياً <code dir="ltr">FrameworkReference</code> على
<code dir="ltr">Microsoft.AspNetCore.App</code> لأن
<code dir="ltr">CircuitHandler</code> يعيش في تجميعة خاصة بالإطار. ولا يستطيع مشروع WebAssembly
في المتصفح استهلاك بناء الحزمة هذا مباشرة. وتبقى دلالات النطاق الساكن وحارس المزوّد العقدين
اللذين سيعيد بناء المكوّنات فقط استخدامهما. ويستطيع Blazor Hybrid استخدام الحزمة اليوم.

## تفاعل JavaScript

<code dir="ltr">BlazorCultureJsInterop</code> مكتفٍ بذاته. ينادي سكربتات
<code dir="ltr">eval</code> صغيرة لملفات الارتباط والتخزين المحلي ولغات المتصفح؛ وتُسلسل القيم
الديناميكية عبر سياق JSON مولّد من المصدر كي لا تستطيع قيمة الخروج من السكربت المولّد. ولا يلزم
ملف JavaScript من المضيف.

المقايضة سياسة أمان محتوى صارمة (CSP). يُحظر <code dir="ltr">eval</code> ما لم تسمح به السياسة،
والسماح به يضعف السياسة للصفحة كلها. وعلى المضيفات ذات CSP صارمة إما توفير وحدة JavaScript
مسماة صغيرة ومخزن <code dir="ltr">IBlazorCultureStore</code> مخصص، أو قبول استثناء السياسة.
والحزمة لا تخفي هذا الاختيار.

## مثال عملي: دائرتان متزامنتان

يفتح مستخدمان التطبيق في الوقت نفسه. يصل أحدهما بملف ارتباط عربي والآخر بإنجليزي. يحل الوسيط
<code dir="ltr">ar</code> و<code dir="ltr">en</code>؛ ويفتح كل اتصال دائرته؛ ويبذر كل
<code dir="ltr">BlazorCircuitCultureHandler</code> خدمة
<code dir="ltr">BlazorCultureService</code> المحدودة النطاق. ينادي مكوّن في الدائرة العربية
<code dir="ltr">CultureService.CultureInfo</code> فيحصل على <code dir="ltr">ar</code>؛ ويحصل
مكوّن في الدائرة الإنجليزية على <code dir="ltr">en</code>. ولا يمس أي تعيين
<code dir="ltr">CultureInfo.CurrentUICulture</code>، فلا يوجد تداخل رغم أن الدائرتين تعملان في
العملية نفسها.

يبدّل المستخدم العربي إلى الإنجليزية. تكتب
<code dir="ltr">SetCultureAsync("en")</code> ملف الارتباط، وتُعاد تحميل الصفحة، ويطبّق الوسيط
<code dir="ltr">en</code>، وتُفتح دائرة إنجليزية جديدة. وقد زالت الدائرة القديمة. ويسجّل المخزن
الآن <code dir="ltr">en</code> للزيارة التالية.

## مآزق

**مأزق:** تعديل الثقافة المحيطة في وضع الخادم لجعل عمليات البحث المريحة تعمل. هذا بالضبط تسرب
بين الدوائر الذي وُجد النطاق لمنعه.

**مأزق:** نسيان <code dir="ltr">IBlazorCultureStore</code>. يعمل الاختيار للجلسة الحالية ويزول
في الدائرة التالية أو عند بدء العملية. والمخزن بلا أثر صامت عمداً.

**مأزق:** تفعيل <code dir="ltr">AddBlazorWasmLocalization</code> مع مزوّد ملفات. يفشل التسجيل،
ويسرد الخطأ المزوّد. استخدم النطاق الساكن بلا الحارس لـ Blazor Hybrid.

**مأزق:** التعامل مع <code dir="ltr">SetCultureAsync</code> كعملية معاملاتية عبر التخزين. تُطبَّق
الثقافة في الذاكرة قبل كتابة المخزن.

## إلى أين بعد ذلك

- [ASP.NET Core وMVC](08-aspnet-core-and-mvc.md) لتعريب الطلب وجسر المخزن.
- [الثقافات والارتداد](03-cultures-and-fallback.md) لكيفية التقاء ثقافة الدائرة بالارتداد.
- [RTL (Right-to-Left) والنص ثنائي الاتجاه](16-rtl-and-bidi.md) للاتجاه وعزل الاتجاه في مضيفات
  Blazor.
- [JavaScript وتسليم العميل](10-javascript-and-client.md) لحمولات المتصفح خارج
  Blazor.

</div>
