<div dir="rtl">

# RTL والنص ثنائي الاتجاه (RTL and bidirectional text)

دعم الاتجاه من اليمين إلى اليسار (RTL (Right-to-Left)) مسؤولية المضيف، لا ميزة في خط أنابيب الرسائل. تعيد
المكتبة سلاسل؛ ويقرر التطبيق اتجاه المستند والتخطيط والخطوط وعزل الاتجاه (bidi isolation). يوضح
هذا الفصل كيفية اتخاذ ذلك القرار باتساق عبر المضيفات المدعومة، وكيفية تجنب العيوب التي تظهر عندما
يُعامل RTL كـ"ترجمة النص وعكس التخطيط".

العربية والعبرية والفارسية والأردية لغات RTL شائعة، لكن لا تستنتج الاتجاه من رمز بلد أو اسم لغة.
استخدم معلومات النص للثقافة:

<div dir="ltr">

```csharp
var culture = CultureInfo.CurrentUICulture;
var isRtl = culture.TextInfo.IsRightToLeft;
var direction = isRtl ? "rtl" : "ltr";
```

</div>

استخدم <code dir="ltr">CurrentUICulture</code> للنص والاتجاه، و
<code dir="ltr">CurrentCulture</code> للأرقام والتواريخ والعملة. ويعيّن وسيط تعريب الطلب عادةً
الاثنين، لكنهما قد يختلفان عندما يفصل التطبيق عمداً التنسيق عن لغة واجهة المستخدم.

## ما تفعله المكتبة وما لا تفعله

- تحل قالباً لثقافة وتنسّق العناصر النائبة.
- تحافظ على المحارف في القالب؛ ولا تعكسها ولا تعيد تشكيلها ولا تعيد ترتيبها.
- لا تعيّن <code dir="ltr">dir</code> أو <code dir="ltr">lang</code> أو
  <code dir="ltr">FlowDirection</code> أو <code dir="ltr">RightToLeft</code>.
- لا تعقّم قوالب HTML أو Markdown. والاتجاه لا يغيّر ذلك الحد.

بقية هذا الفصل كود تطبيق.

## HTML: الخصائص lang وdir وCSS المنطقي

عيّن الخاصيتين على المستند، لا على حاوية فقط:

<div dir="ltr">

```cshtml
@{
    var culture = CultureInfo.CurrentUICulture;
    var direction = culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";
}
<!DOCTYPE html>
<html lang="@culture.Name" dir="@direction">
<head>
    <meta charset="utf-8" />
    <title>@Localizer[ReferenceKeys.APPLICATION_TITLE]</title>
</head>
<body>
    @RenderBody()
</body>
</html>
```

</div>

تصف <code dir="ltr">lang</code> اللغة؛ ويصف <code dir="ltr">dir</code> الاتجاه الأساسي. وتعيين
<code dir="ltr">lang="ar"</code> وحده لا يغيّر التخطيط، وتعيين
<code dir="ltr">dir="rtl"</code> وحده لا يمنح التقنيات المساعدة اللغة.

ولكتلة ينتجها المستخدم واتجاهها مجهول، يتيح <code dir="ltr">dir="auto"</code> لأول محرف قوي
تحديد الاتجاه. أبقِ <code dir="ltr">rtl</code>/<code dir="ltr">ltr</code> صريحين لإطار
التطبيق؛ والتخمين مناسب لمحتوى المستخدم، لا لإطار الواجهة.

فضّل خصائص CSS المنطقية على أزواج اليسار/اليمين:

<div dir="ltr">

```css
.nav-link {
    margin-inline-start: 1rem;
}

.price {
    text-align: end;
}

.card {
    padding-inline: 1rem;
    border-inline-start: 2px solid #ccc;
}
```

</div>

استبدل <code dir="ltr">margin-left</code> و<code dir="ltr">padding-right</code> و
<code dir="ltr">left</code> و<code dir="ltr">right</code> و
<code dir="ltr">text-align: left/right</code> بمكافئاتها المنطقية حيث يجب أن ينعكس التخطيط.
وأبقِ الخصائص الفيزيائية حيث يجب ألا ينعكس العنصر، مثل محور رسم بياني أو كتلة كود.

## عزل الاتجاه للمحتوى مختلط الاتجاه

لا يعزل تخطيط RTL تلقائياً قيمة مُقحمة. فقد يجرّ معرّف طلب لاتيني أو URL أو اسم ملف أو اسم مستخدم
علامات الترقيم المحيطة إلى الجهة الخاطئة. اعزل القيمة المُقحمة:

<div dir="ltr">

```cshtml
<p>
    @Localizer[ReferenceKeys.ORDER_ID_FIELD]:
    <bdi>@order.Id</bdi>
</p>
```

</div>

<code dir="ltr">&lt;bdi&gt;</code> هو عنصر HTML لهذه المهمة. وفي سياقات النص العادي (السجلات
ومتون البريد وتسميات سطح المكتب)، استخدم محارف العزل في Unicode:

<div dir="ltr">

```csharp
private static string Isolate(string value) => $"\u2068{value}\u2069";
```

</div>

يبدأ U+2068 (First Strong Isolate) العزل وينهيه U+2069 (Pop Directional Isolate). استخدمه حول
القيم المُقحمة، لا حول الجملة كلها. اختبر النتيجة في العارض الهدف؛ فبعض الطرفيات وعملاء البريد
القدامى لا يتعاملون جيداً مع محارف العزل.

تجنّب <code dir="ltr">unicode-bidi: bidi-override</code> إلا إذا كنت تعرض عمداً قيمة محكومة باتجاه
ثابت. إنه يتجاوز خوارزمية الاتجاه وينتج عادةً نصاً معكوساً.

## ASP.NET Core وMVC

مثال التخطيط أعلاه هو مسار العرض من الخادم. وثمة قاعدتان إضافيتان:

- حل ثقافة الطلب قبل تشغيل التخطيط. يفعل ذلك وسيط
  <code dir="ltr">UseRequestLocalization</code> في خط الأنابيب الموثّق؛ ويقرأ التخطيط
  <code dir="ltr">CurrentUICulture</code> الناتجة.
- أبقِ القيم المقحمة معزولة. فالجملة المعرَّبة التي تحوي معرّفاً لاتينياً في وسطها ما زالت تحتاج
  <code dir="ltr">&lt;bdi&gt;</code> حول المعرّف، حتى عندما تكون الجملة نفسها عربية.

مضيف MVC المرجعي يعيّن <code dir="ltr">lang</code> من الثقافة الحالية بالفعل؛ أضف
<code dir="ltr">dir</code> في المكان نفسه عندما تتبنى هذا الإرشاد. راجع
[ASP.NET Core وMVC](08-aspnet-core-and-mvc.md) لإعداد تعريب الطلب ومُعرِّب العرض.

## Blazor Server

يستطيع تخطيط Blazor تعيين <code dir="ltr">dir</code> على غلاف، لكن عنصر المستند يحتاج
<code dir="ltr">lang</code> و<code dir="ltr">dir</code> أيضاً لسلوك المتصفح الصحيح والتقنيات
المساعدة. ومن الأساليب مساعد JavaScript صغير:

<div dir="ltr">

```javascript
window.jeninnetSetDocumentCulture = (culture, direction) => {
    document.documentElement.lang = culture;
    document.documentElement.dir = direction;
};
```

</div>

حمّل المساعد من صفحة المضيف (أو وحدة JS تُحمَّل عند البدء). وناده بعد معرفة الثقافة وبعد تبديلها:

<div dir="ltr">

```razor
@inject BlazorCultureService CultureService
@inject IJSRuntime JS

<div class="page" dir="@Direction" lang="@CultureName">
    @Body
</div>

@code {
    private CultureInfo Culture => CultureService.CultureInfo;

    private string CultureName => Culture.Name;

    private string Direction =>
        Culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await JS.InvokeVoidAsync(
                "jeninnetSetDocumentCulture", CultureName, Direction);
        }
    }
}
```

</div>

<code dir="ltr">BlazorCultureService.CultureInfo</code> هي ثقافة الدائرة المختارة؛ ويُبقي
الارتداد العرض الأول آمناً قبل التهيئة. أعد تشغيل التفاعل بعد تبديل الثقافة إذا كان تخطيطك لا
يعيد تحميل المستند.

راجع [Blazor](09-blazor.md) لخدمة الثقافة محدودة الدائرة، ومخزن ملف الارتباط، وحارس مزوّد WASM (WebAssembly).

## Blazor Hybrid

يعرض Blazor Hybrid HTML داخل WebView، لذا تنطبق قواعد <code dir="ltr">dir</code> وCSS نفسها.
ويعيّن المضيف المرجعي الاتجاه على حاويته الجذرية:

<div dir="ltr">

```razor
@{
    var culture = CultureService.CultureInfo;
    var direction = culture.TextInfo.IsRightToLeft ? "rtl" : "ltr";
}

<div class="page" dir="@direction" lang="@culture.Name">
    ...
</div>
```

</div>

ويجب أن تحمل صفحة المضيف المضمّنة (<code dir="ltr">wwwroot/index.html</code>) أيضاً قيمة
<code dir="ltr">lang</code> و<code dir="ltr">dir</code> افتراضيتين معقولتين؛ ويستطيع المكوّن
الجذري تحديثهما عندما يبدّل المستخدم الثقافة. ويعرض
[مضيف Blazor Hybrid](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Reference.BlazorHybrid/README.md)
البنية العاملة.

## WinForms

عيّن الاتجاه على النموذج وأعد تطبيقه بعد تبديل الثقافة:

<div dir="ltr">

```csharp
private void ApplyDirection() {
    var isRtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
    RightToLeft = isRtl ? RightToLeft.Yes : RightToLeft.No;
    RightToLeftLayout = isRtl;
}
```

</div>

يغيّر <code dir="ltr">RightToLeft</code> محاذاة النص وسلوك عناصر التحكم. ويعكس
<code dir="ltr">RightToLeftLayout</code> تخطيط العناصر المدعومة؛ واختبر عناصر التحكم المرسومة
يدوياً والصور والأيقونات منفصلة لأنها لا تنعكس كلها تلقائياً. ويطبّق مضيف WinForms المرجعي
<code dir="ltr">RightToLeft</code> في <code dir="ltr">ApplyCulture</code> وهو مثال عامل.

## WPF وXAML

تورث <code dir="ltr">FlowDirection</code> نزولاً في الشجرة المرئية:

<div dir="ltr">

```csharp
var isRtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
```

</div>

عيّنها على <code dir="ltr">Window</code> أو العنصر الجذري. ينعكس التخطيط، لكن الصور والأيقونات
والرسم المخصص لا تنعكس. اعكس سهماً فقط عندما يكون معناه اتجاهياً؛ لا تعكس الأرقام أو الرسوم
البيانية أو الشعارات. ولمحاذاة النص، فضّل <code dir="ltr">TextAlignment</code> واللوحات المراعية
للتدفق على الهوامش المضمّنة.

يستخدم مضيف WPF المرجعي ارتباطات عادية ولا يبتكر امتداد ترميز XAML للتعريب؛ والاتجاه كود تطبيق
بالطريقة نفسها.

## التنسيق والعناصر النائبة والترجمة

- **استخدم جملاً كاملة.** ترجمة الشظايا ودمجها يكسر ترتيب الكلمات في كل لغة ويجعل عيوب الاتجاه
  أصعب رؤية.
- **حافظ على العناصر النائبة.** قد يعيد المترجم ترتيب <code dir="ltr">{0}</code>/
  <code dir="ltr">{1}</code> أو العناصر النائبة المسمّاة؛ ويستبدلها وقت التشغيل بعد الترجمة.
  وإسقاط عنصر نائب عيب، لا اختيار تخطيط.
- **اعزل القيمة المُقحمة فقط.** طبّق <code dir="ltr">&lt;bdi&gt;</code> أو محارف عزل Unicode حول
  المعرّفات وعناوين URL والأسماء والقيم مختلطة الاتجاه الأخرى، لا حول الجملة المترجمة كلها.
- **نسّق القيم بـ <code dir="ltr">CurrentCulture</code>.** يجب أن يستخدم سعر أو تاريخ داخل جملة
  RTL تنسيق الأرقام والتواريخ للثقافة، لا سلسلة أعيد ترتيبها يدوياً.
- **لا تعكس السلاسل.** عكس المحارف يكسر تشكيل العربية والأرقام ومقاطع الاتجاه. دع خوارزمية الاتجاه
  في المنصة وتخطيط المضيف يقومان بالعمل.
- **أبقِ الاتجاه في المضيف.** لا تحمل بيانات وصفية للاتجاه في قالب الرسالة. فقد تظهر سلسلة عربية
  واحدة في صفحة RTL وجدول إدارة LTR (Left-to-Right) وبريد نص عادي؛ والمضيف يقرر.

## قائمة فحص الاختبار

| الفحص | ما يجب التحقق منه |
|---|---|
| خصائص المستند | <code dir="ltr">lang</code> يطابق الثقافة و<code dir="ltr">dir</code> يساوي <code dir="ltr">rtl</code> لثقافات RTL |
| التخطيط | التنقل والهوامش والحدود والمحاذاة تنعكس مع CSS المنطقي |
| المحتوى المختلط | المعرّفات وعناوين URL والأسماء والأرقام تبقى مقروءة داخل جمل RTL |
| الأرقام والتواريخ | القيم تستخدم تنسيق الثقافة المقصودة، لا نصاً معكوساً |
| الأيقونات والصور | الأيقونات الاتجاهية تنعكس؛ الشعارات والرسوم والأرقام لا تنعكس |
| النماذج | التسميات ورسائل التحقق وملخصات الأخطاء تحاذي وتُقرأ صحيحاً |
| تبديل الثقافة | يتحدث الاتجاه فوراً بعد التبديل، دون غلاف قديم |
| مضيفات سطح المكتب | <code dir="ltr">RightToLeft</code>/<code dir="ltr">RightToLeftLayout</code> أو <code dir="ltr">FlowDirection</code> يعاد تطبيقها عند تبديل الثقافة |
| إمكانية الوصول | قارئات الشاشة تتلقى <code dir="ltr">lang</code> الصحيحة واتجاه المستند |
| الانحدار البصري | صور العربية/العبرية تُراجَع، لا تأكيدات السلاسل فقط |

استخدم محتوى عربياً وعبرياً حقيقياً في الاختبارات، لا التعريب الزائف وحده. فالتعريب الزائف يساعد
في اكتشاف فائض التخطيط؛ ولا يختبر سلوك الاتجاه.

## مآزق

**مأزق:** تعيين <code dir="ltr">lang="ar"</code> وتوقع أن يبدّل المتصفح الاتجاه.
<code dir="ltr">lang</code> بيانات وصفية؛ و<code dir="ltr">dir</code> يتحكم في الاتجاه الأساسي.

**مأزق:** تطبيق <code dir="ltr">text-align: right</code> في كل مكان. إنه يكسر المحتوى اللاتيني
المضمّن في صفحة RTL ويتجاهل دلالات البداية/النهاية المنطقية. استخدم
<code dir="ltr">start</code>/<code dir="ltr">end</code> والخصائص المنطقية.

**مأزق:** لف التخطيط كله في <code dir="ltr">&lt;bdi&gt;</code>. اعزل القيم المُقحمة، لا الصفحات
كاملة.

**مأزق:** عكس سلسلة "لإصلاح" العربية. ذلك يدمّر التشكيل وينتج أرقاماً خاطئة.

**مأزق:** عكس كل أيقونة. لا تنعكس إلا الأيقونات الاتجاهية؛ والشعار المعكوس عيب علامة تجارية.

**مأزق:** نسيان تبديل الثقافة. الاتجاه المعيَّن عند البدء فقط يترك الصفحة في الاتجاه القديم بعد أن
يغيّر المستخدم اللغة.

## إلى أين بعد ذلك

- [الثقافات والارتداد](03-cultures-and-fallback.md) لكيفية اختيار ثقافة الطلب أو المضيف.
- [التنسيق وصيغ الجمع](04-formatting-and-plurals.md) للعناصر النائبة وICU وتنسيق الأرقام
  والتواريخ.
- [ASP.NET Core وMVC](08-aspnet-core-and-mvc.md) لتعريب الطلب والعروض.
- [Blazor](09-blazor.md) لإدارة الثقافة محدودة الدائرة والساكنة.
- [الأمان](../04-operations/02-security.md) للقوالب الموثوقة وترميز المخرجات.
- [القيود المعروفة](../03-reference/10-known-limitations.md) للحد القائل إن الاتجاه مملوك
  للمضيف.

</div>
