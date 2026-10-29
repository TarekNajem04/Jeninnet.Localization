<div dir="rtl">

# الترحيل من Microsoft.Extensions.Localization

التطبيق الذي يستخدم <code dir="ltr">Microsoft.Extensions.Localization</code> بالفعل يملك
<code dir="ltr">IStringLocalizer</code> و<code dir="ltr">IStringLocalizer&lt;T&gt;</code> و
<code dir="ltr">IStringLocalizerFactory</code>، وعادةً ملفات ResX. الواجهات هي نفسها التي يجسر
إليها هذا الإطار، وهذا يجعل الترحيل يبدو أصغر مما هو. فالسلوك خلفها يتغير.

يربط هذا الفصل المفاهيم، ويبدّل التسجيل، ويسرد الفروق التي تكسر الكود بصمت. ويفترض أن التطبيق
يستطيع تحمل ترحيل في إصدار واحد؛ أما الهجين لكل نوع فليس مدمجاً.

## ربط المفاهيم

| Microsoft.Extensions.Localization | Jeninnet.Localization |
|---|---|
| <code dir="ltr">IStringLocalizer</code> / <code dir="ltr">IStringLocalizer&lt;T&gt;</code> | الواجهات نفسها، مسنودة بـ <code dir="ltr">CustomStringLocalizerFactory</code> فوق <code dir="ltr">ILocalizationStore</code>. |
| <code dir="ltr">IStringLocalizerFactory</code> | تُستبدل بـ <code dir="ltr">CustomStringLocalizerFactory</code> عبر <code dir="ltr">AddLocalization()</code>. |
| <code dir="ltr">ResourceManagerStringLocalizerFactory</code> | لا مكافئ في الجسر؛ ويبقى ResX خيار وقت تشغيل فقط عبر خط أنابيب الرسائل. |
| اسم ResX الأساسي / <code dir="ltr">ResourcesPath</code> | اسم مجموعة الموارد. يستخدم <code dir="ltr">Create(Type)</code> الخاصية <code dir="ltr">Type.Name</code>؛ ويستخدم <code dir="ltr">Create(baseName, location)</code> الاسم الأساسي الدقيق. ولا يوجد خيار <code dir="ltr">ResourcesPath</code>. |
| <code dir="ltr">LocalizationOptions.ResourcesPath</code> | لا مكافئ. والمسارات صريحة في تسجيل المزوّد. |
| <code dir="ltr">LocalizationOptions</code> (Microsoft) | <code dir="ltr">Jeninnet.Localization.Abstractions.Options.LocalizationOptions</code> مع <code dir="ltr">DefaultCulture</code> و<code dir="ltr">MissingKeySink</code>. |
| تجميعات satellite / <code dir="ltr">ResourceManager</code> | <code dir="ltr">Providers.ResX</code> أو <code dir="ltr">Providers.Satellite</code> لخط الأنابيب؛ وجسر المخزن منفصل. |
| <code dir="ltr">RequestLocalizationMiddleware</code> | الوسيط نفسه؛ وتستطيع <code dir="ltr">AddLocalizationRequestCultures</code> ملء خياراته. |
| <code dir="ltr">[Required(ErrorMessage = "...")]</code> | <code dir="ltr">[LocalizedRequired("Key")]</code> أو مزوّد بيانات MVC الوصفية لـ <code dir="ltr">RequiredAttribute</code> العادي. |
| القيمة المفقودة: الاسم، و<code dir="ltr">ResourceNotFound = true</code> | <code dir="ltr">[MISSING:key]</code>، و<code dir="ltr">ResourceNotFound = true</code> عبر جسر المخزن. |
| ارتداد ثقافة الأب في ResourceManager | لا ارتداد في جسر المخزن؛ والارتداد يعيش في <code dir="ltr">IMessageLocalizer</code>. |

أكبر فرق سلوكي هو الارتداد. يستطيع مدير موارد Microsoft حل مورد ثقافة أب عندما تغيب الثقافة
الدقيقة. أما جسر المخزن فيحل الثقافة الملتقطة عند إنشاء المُعرِّب بدقة. وإذا كان التطبيق يعتمد على
ارتداد الأب، فإما أن تبذر كل ثقافة مخدومة أو تنقل تلك العمليات إلى خط الأنابيب.

## تبديل التسجيل

تركيب Microsoft النموذجي يبدو هكذا:

<div dir="ltr">

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews();
```

</div>

ولتركيب Jeninnet شكل مختلف لأن المخزن تبعية منفصلة:

<div dir="ltr">

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;
using JeninnetLocalizationOptions = Jeninnet.Localization.Abstractions.Options.LocalizationOptions;

builder.Services.AddControllersWithViews();

builder.Services.AddLocalization(options => options.DefaultCulture = "en");

builder.Services.AddEntityFrameworkLocalizationStore(
    builder.Configuration,
    connectionStringName: "LocalizationDb");

builder.Services.AddMvcLocalizedValidation();
builder.Services.AddLocalizationRequestCultures();
```

</div>

تسجيل المخزن إلزامي لـ <code dir="ltr">IStringLocalizer&lt;T&gt;</code>. وبدونه يُسجَّل المصنع
لكنه لا يستطيع حل مجموعة موارد. وخط أنابيب الرسائل وحده لا يحتاج إلى مخزن.

**مأزق:** توجد <code dir="ltr">AddLocalization</code> في كل من
<code dir="ltr">Microsoft.Extensions.DependencyInjection</code> و
<code dir="ltr">Jeninnet.Localization.Core.Extensions</code>. وإذا استُوردت مساحتا الأسماء
معاً، يكون النداء ملتبساً. استخدم اسماً مستعاراً لنوع الخيارات وأهّل نداء الامتداد، أو أبقِ
مساحات الأسماء منفصلة.

## ترحيل البيانات

محتوى ResX مفتاحه <code dir="ltr">data name</code> ويحمل قيمة وتعليقاً اختيارياً. ومجموعة الموارد
مفتاحها <code dir="ltr">(resourceSet, culture, name)</code>. والربط آلي لكنه يحتاج قرارات:

1. **اختر اسم مجموعة الموارد.** يستخدم <code dir="ltr">Create(Type)</code> الخاصية
   <code dir="ltr">Type.Name</code>، فالربط الطبيعي هو اسم النوع بلا مساحة أسماء. وإذا تشارك
   نوعان الاسم نفسه في مساحتي أسماء مختلفتين، تصطدمان. اختر أسماء أساسية صريحة أو أعد تسمية
   مجموعات الموارد.
2. **اختر المفتاح.** يصبح اسم إدخال ResX مفتاح الرسالة دون تغيير. وهنا تصبح الكتالوجات التي
   تستخدم النص كمفتاح محرجة؛ فكّر في إدخال مفاتيح منقطة الآن لا لاحقاً.
3. **اختر الثقافة.** ملف ResX واحد لكل ثقافة يُرتبط بزوج <code dir="ltr">(set, culture)</code>
   واحد. وملف ResX المحايد يُرتبط عادةً بالثقافة الافتراضية.
4. **اختر المخزن أو كتالوجاً.** المخزن القابل للتعديل وقت التشغيل يأخذ الإدخالات عبر واجهة الإدارة
   المحكومة؛ ومزوّد الملفات يأخذ كتالوج JSON أو XML مسطحاً. وكلاهما صالح، وهما مساران مختلفان.

تستطيع أداة بذر صغيرة تحليل ملف ResX وإنشاء الإدخالات:

<div dir="ltr">

```csharp
var document = ResXCatalogInterchange.ParseDocument(
    await File.ReadAllTextAsync("Resources/HomeController.ar.resx"),
    "HomeController.ar.resx");

var admin = store.RequireAdmin();
foreach (var entry in document.Entries.Where(e => e.IsStringResource && e.Value is not null)) {
    await admin.CreateEntryAsync(
        "HomeController", "ar", entry.Name, entry.Value!,
        new ResourceAdminOptions { Actor = "resx-migration" });
}
```

</div>

يبلّغ <code dir="ltr">CreateEntryAsync</code> عن <code dir="ltr">AlreadyExists</code> بدلاً من
الرمي، لذا يمكن اعتبار التشغيل الثاني متطابق الأثر. ويجب أن يعاين ترحيل الإنتاج بـ
<code dir="ltr">DryRun = true</code> أولاً ويحتفظ بالفاعل.

وإذا كان الهدف كتالوج JSON بدلاً من ذلك، فاكتب الإدخالات نفسها في كائن مسطح. ويستطيع مولّد
المفاتيح استهلاك ذلك الملف كـ <code dir="ltr">*.loc.json</code> إذا صُرّح به كملف إضافي.

## استراتيجية المفاتيح

تستخدم مسارات ResX في Microsoft غالباً نصاً إنجليزياً أو أسماء خصائص كمفاتيح. يعمل ذلك في ResX لأن
المفتاح نادراً ما يقرأه إنسان خارج المحرر. وفي خط أنابيب مدفوع بالمفاتيح يصبح عبئاً:

- يرى المترجمون المفتاح في PO (Portable Object)/XLIFF (XML Localization Interchange File Format) وقد يترجمونه بالخطأ.
- إعادة تسمية المفتاح ترحيل كتالوج؛ والمفاتيح النصية تجعل ذلك الترحيل مستمراً.
- تنطوي المعاني المكررة. فشاشتان بالنص نفسه تحتاجان غالباً ترجمتين مختلفتين.
- تظهر المفاتيح في التتبعات ومصبات المفاتيح المفقودة وتقارير التكافؤ. والمعرّفات المنقطة أسهل
  قراءة وأكثر أماناً من الجمل.

اعتمد المفاتيح المنقطة (<code dir="ltr">Checkout.Name.Required</code> و
<code dir="ltr">Cart.Items.Empty</code>) واستخدم السياقات حيث يكون للمفتاح نفسه معانٍ مختلفة. هذه
النصيحة نفسها في [المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md)، لكنها تهم أكثر
أثناء الترحيل لأن تغيير المفاتيح لاحقاً يمس كل كتالوج.

## التعايش

المصنع خدمة واحدة. فبمجرد أن يستبدل <code dir="ltr">CustomStringLocalizerFactory</code> النوع
<code dir="ltr">ResourceManagerStringLocalizerFactory</code>، يمر كل حل لـ
<code dir="ltr">IStringLocalizer&lt;T&gt;</code> عبر المخزن. ولا يوجد هجين لكل نوع في الصندوق.

خيارات التعايش العملية:

- **انقسام على مستوى العملية.** مضيف يستخدم جسر Jeninnet؛ وآخر ما زال يستخدم تعريب Microsoft. هذا
  نظيف لكنه لا يساعد تطبيقاً واحداً.
- **رحّل كل مستهلكي <code dir="ltr">IStringLocalizer</code> دفعة واحدة.** أبقِ خط أنابيب الرسائل
  منفصلاً لمستهلكي <code dir="ltr">IMessageLocalizer</code>. ويستطيع الاثنان العمل في عملية
  واحدة؛ فهما يقرآن مصادر مختلفة.
- **أبقِ ResX على خط الأنابيب.** يخدم <code dir="ltr">Providers.ResX</code> الواجهة
  <code dir="ltr">IMessageLocalizer</code> من موارد مترجمة. ولا يغذّي
  <code dir="ltr">IStringLocalizer</code>. وهذا يحفظ تأليف ResX لمستهلكي خط الأنابيب بينما
  يستخدم الجسر المخزن، بكلفة كتالوجين.
- **غلّف مصنعاً قديماً.** يستطيع <code dir="ltr">IStringLocalizerFactory</code> مخصص تفويض بعض
  الأنواع إلى مصنع Microsoft وغيرها إلى مصنع Jeninnet. هذا كود مخصص وخارج التركيب المدعوم؛
  اختبره بعناية إن احتجته.

**مأزق:** نداء <code dir="ltr">AddLocalization()</code> من Microsoft بعد نداء Jeninnet، أو العكس،
وافتراض أن المصنع يُدمج. كلاهما يسجّل مصنعاً؛ وآخر استبدال يفوز. قرّر أي سطح يملك
<code dir="ltr">IStringLocalizer</code> وأبقِ مالكاً واحداً.

## قائمة تحقق الترحيل

راجع هذه القائمة قبل حذف التسجيل القديم:

- [ ] كل حل لـ <code dir="ltr">IStringLocalizer&lt;T&gt;</code> لديه
  <code dir="ltr">ILocalizationStore</code>.
- [ ] أسماء مجموعات الموارد تطابق <code dir="ltr">Type.Name</code> أو الاسم الأساسي الصريح
  المستخدم في موضع النداء.
- [ ] كل ثقافة مخدومة لديها إدخالات للمفاتيح التي يحتاجها مستخدموها؛ ولا يُفترض ارتداد الأب.
- [ ] الإدخالات المفقودة تعرض <code dir="ltr">[MISSING:key]</code>؛ واختبارات الواجهة واللقطات
  محدَّثة.
- [ ] الوسائط تستخدم تنسيق <code dir="ltr">{0}</code> المركب، لا العناصر النائبة المسمّاة، على
  مسار <code dir="ltr">IStringLocalizer</code>.
- [ ] سمات التحقق تستخدم العائلة المعرَّبة أو مزوّد بيانات MVC الوصفية، ومفاتيحها موجودة في
  المخزن.
- [ ] أسماء <code dir="ltr">DisplayAttribute</code> وأوصافها وتلميحاتها إما مفاتيح في المخزن أو
  تستخدم <code dir="ltr">ResourceType</code> عمداً.
- [ ] تعريب الطلب يملك مخزناً أو ثقافات مدعومة صريحة؛ و<code dir="ltr">Accept-Language</code> ما
  زال يختار الثقافة المتوقعة.
- [ ] المستهلكون محدودو النطاق والعابرون لا يلتقطون مُعرِّباً في singleton.
- [ ] سلوك <code dir="ltr">GetAllStrings</code> مقبول: الجسر يعدّد الثقافة الدقيقة فقط.
- [ ] أداة التكافؤ لا تبلّغ عن مفاتيح مفقودة أو انحرافات عناصر نائبة في الكتالوج المرحَّل.
- [ ] توجد خطة تراجع: أبقِ التسجيل والكتالوجات القديمة حتى يشحن إصدار واحد.

## مثال عملي: متحكم واحد

قبل:

<div dir="ltr">

```csharp
public class HomeController : Controller {
    private readonly IStringLocalizer<HomeController> _localizer;
    public HomeController(IStringLocalizer<HomeController> localizer) => _localizer = localizer;

    public IActionResult Index() => View(_localizer["Welcome", User.Identity?.Name ?? "guest"]);
}
```

</div>

ملف ResX هو <code dir="ltr">Resources/HomeController.resx</code> مع إدخال باسم
<code dir="ltr">Welcome</code> وقيمة <code dir="ltr">Welcome, {0}!</code>.

بعد:

1. أنشئ المخزن (أو كتالوج JSON مستورداً إليه) بمجموعة الموارد
   <code dir="ltr">HomeController</code>، والثقافة <code dir="ltr">en</code>، والمفتاح
   <code dir="ltr">Welcome</code>، والقالب <code dir="ltr">Welcome, {0}!</code>.
2. سجّل المخزن و<code dir="ltr">AddLocalization</code> والمزوّد.
3. أبقِ المتحكم دون تغيير. فالنوع العابر
   <code dir="ltr">IStringLocalizer&lt;HomeController&gt;</code> يحل الآن عبر المخزن.
4. أضف إدخالات <code dir="ltr">ar</code> لكل مفتاح مخدوم. لا تعتمد على ارتداد
   <code dir="ltr">en</code> ما لم يخدم التطبيق الإنجليزية للمستخدمين العرب عمداً.
5. استبدل رسائل التحقق المضمّنة بـ <code dir="ltr">LocalizedRequired</code> وأخواتها، أو سجّل
   <code dir="ltr">AddMvcLocalizedValidation</code> لبيانات <code dir="ltr">RequiredAttribute</code>
   الوصفية العادية.
6. شغّل أداة التكافؤ واختبارات الواجهة القائمة. وعلامة الغياب وفروق الارتداد هما أكثر إخفاقين
   محتملين في الاختبارات.

لم يتغير كود المتحكم. بل تغيّر التسجيل والبيانات وتوقعات الارتداد.

## مآزق

**مأزق:** افتراض أن <code dir="ltr">Create(Type)</code> يستخدم اسم النوع الكامل. إنه يستخدم
<code dir="ltr">Type.Name</code>؛ ومساحات الأسماء ليست جزءاً من اسم مجموعة الموارد.

**مأزق:** افتراض أن <code dir="ltr">Create(baseName, location)</code> يحدّ النطاق بالموقع. يُتحقق
من الموقع ويُتجاهل؛ والاسم الأساسي هو الهوية كلها.

**مأزق:** توقع دلالات <code dir="ltr">ResourcesPath</code> أو ارتداد ثقافة الأب من المخزن. لا يوجد
خيار <code dir="ltr">ResourcesPath</code>، ويحل الجسر الثقافة الملتقطة بدقة. ابذر الثقافات
المخدومة أو استخدم خط الأنابيب.

**مأزق:** مقارنة القيم المفقودة بالسلوك القديم. كان المُعرِّب القديم يعيد الاسم؛ والجسر يعيد
<code dir="ltr">[MISSING:key]</code> ويرفع <code dir="ltr">ResourceNotFound</code>.

**مأزق:** نسيان أن سمات DataAnnotations المعرَّبة تحتاج أيضاً المصنع المسنود بالمخزن. تركيب خط
الأنابيب فقط يُبنى ثم يفشل التحقق وقت التشغيل.

## إلى أين بعد ذلك

- [ASP.NET Core وMVC](../02-guides/08-aspnet-core-and-mvc.md) للتركيب الشبكي الكامل.
- [مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md) للجسر.
- [مرجع المزوّدين](03-provider-reference.md) لخيارات ResX وsatellite وقت التشغيل.

</div>
