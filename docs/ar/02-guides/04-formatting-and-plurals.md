<div dir="rtl">

# التنسيق وصيغ الجمع (formatting and plurals)

يجري التنسيق (formatting) بعد حل الرسالة. يغطي هذا الفصل المنسّق الافتراضي للعناصر النائبة
(placeholder formatter)، ومحرك ICU (International Components for Unicode) MessageFormat الاختياري، وواجهات الجمع (plural APIs)،
والمنسّقات الآمنة لـ HTML وMarkdown، وخصائص الأداء التي تنتج عن هذه التصاميم.

## مرحلة التنسيق

يحل <code dir="ltr">DefaultMessageLocalizer</code> القالب أولاً، ثم ينادي
<code dir="ltr">IMessageFormatter</code> المسجّل عند الإصابة فقط. ويعيد الغياب
<code dir="ltr">null</code> ولا ينادي المنسّق أبداً. يتلقى المنسّق القالب ووسائط المستدعي، لا
المفتاح ولا المزوّد، وهذا ما يبقيه نقياً وقابلاً للاختبار:

<div dir="ltr">

```csharp
public string Format(string template, IReadOnlyDictionary<string, object?> parameters);
```

</div>

عندما ينفّذ المنسّق <code dir="ltr">ICultureAwareMessageFormatter</code>، ينادي المُعرِّب التحميل
الذي يتلقى الثقافة المطلوبة أيضاً. لا يفعل المنسّق الافتراضي ذلك؛ أما
<code dir="ltr">IcuMessageFormatter</code> فيفعله. وهكذا يتبع عرض الجمع والأرقام والتواريخ
الثقافة المطلوبة حتى عندما يأتي القالب من ثقافة أب أو من الثقافة الافتراضية.

## المنسّق الافتراضي

يفحص <code dir="ltr">DefaultMessageFormatter</code> امتدادات <code dir="ltr">{...}</code>
ويستبدل قيمة القاموس التي يطابق مفتاحها النص بين القوسين.

<div dir="ltr">

```text
Template:    "Hello, {name}! You have {count} items."
Parameters:  name = "Ada", count = 3
Result:      "Hello, Ada! You have 3 items."
```

</div>

تفاصيل السلوك، مؤكدة في المصدر:

- **الوسيط المفقود:** يبقى العنصر النائب كما كُتب تماماً.
- **القيمة الفارغة أو null:** تُستبدل بسلسلة فارغة.
- **التحويل:** <code dir="ltr">value?.ToString()</code>؛ لا يوجد اختيار لسلسلة تنسيق أو
  <code dir="ltr">IFormattable</code>.
- **المطابقة:** المفتاح هو كل شيء حتى القوس <code dir="ltr">}</code> التالي. فـ
  <code dir="ltr">{0}</code> مفتاحه <code dir="ltr">0</code>؛ و<code dir="ltr">{name}</code>
  مفتاحه <code dir="ltr">name</code>.
- **الأقواس:** الأقواس المتداخلة والمهرَّبة ليست جزءاً من العقد.
- **الثقافة:** لا وسيطة ثقافة؛ ويستخدم <code dir="ltr">ToString()</code> الثقافة المحيطة
  <code dir="ltr">CurrentCulture</code>.

النقطة الأخيرة هي المفاجأة. إذا احتوى القالب على <code dir="ltr">{price}</code> وكانت القيمة
<code dir="ltr">decimal</code>، فسيتبع العرض <code dir="ltr">CurrentCulture</code>، لا الثقافة
المررَّرة إلى <code dir="ltr">LocalizeAsync</code>. وللأرقام والتواريخ الصحيحة ثقافياً، استخدم
منسّق ICU أو نسّق الوسيط مسبقاً ومرّر سلسلة.

**مأزق:** لا يدعم المنسّق الافتراضي محددات التنسيق المركّبة (composite format specifiers). في
<code dir="ltr">"Total: {0:N2}"</code> يكون المفتاح <code dir="ltr">0:N2</code>، لا
<code dir="ltr">0</code>، لذا لن يطابق إدخال قاموس باسم <code dir="ltr">0</code>. ويبقى العنصر
النائب حرفياً. استخدم <code dir="ltr">IcuMessageFormatter</code> لتنسيق على نمط
<code dir="ltr">{value, number, ::.00}</code>، أو مرّر سلسلة منسّقة مسبقاً.

## ICU MessageFormat

<code dir="ltr">IcuMessageFormatter</code> منسّق اختياري ينفّذ
<code dir="ltr">ICultureAwareMessageFormatter</code>. التسجيل هو المفتاح: سجّله بدلاً من المنسّق
الافتراضي. ويستخدم إعداد DI (Dependency Injection) الدالة <code dir="ltr">TryAddSingleton</code>، لذا يفوز المنسّق
المسجّل مسبقاً:

<div dir="ltr">

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization(options => options.DefaultCulture = "en");
```

</div>

القالب الخالي من تركيب ICU يأخذ مسار المنسّق الداخلي الافتراضي نفسه، فيبقى المحتوى القائم دون
تغيير. والقالب الذي يحتوي <code dir="ltr">{arg, type, ...}</code> يشغّل محرك ICU. كما تشغّله
أنواع الوسائط المجهولة وترمي <code dir="ltr">FormatException</code> وصفية بدلاً من المرور بصمت.

التراكيب المدعومة:

- <code dir="ltr">plural</code> مع فروع <code dir="ltr">=number</code> و
  <code dir="ltr">offset:N</code> وفئات CLDR (Common Locale Data Repository) الأساسية؛ و<code dir="ltr">selectordinal</code>؛
  و<code dir="ltr">select</code>.
- <code dir="ltr">number</code> (الافتراضي، <code dir="ltr">integer</code>،
  <code dir="ltr">currency</code>، <code dir="ltr">percent</code>) و
  <code dir="ltr">date</code>/<code dir="ltr">time</code> (من الافتراضي إلى
  <code dir="ltr">full</code>).
- الرسائل المتداخلة، و<code dir="ltr">#</code> داخل فروع الجمع، واقتباس ICU بالفاصلة العليا
  (<code dir="ltr">''</code>، <code dir="ltr">'{...}'</code>).
- مجموعة فرعية من هياكل <code dir="ltr">::</code>: <code dir="ltr">percent</code>،
  <code dir="ltr">currency/XXX</code>، ودقة ثابتة، وتاريخ/وقت رقمي.

القوالب المشوّهة، وأنواع الوسائط المجهولة، ووسائط المحدد المفقودة، وعدم تطابق الأنواع، وغياب فرع
<code dir="ltr">other</code>، والمحددات المكررة، كلها ترمي
<code dir="ltr">FormatException</code> مع موضع النمط. أما الوسائط البسيطة المفقودة فتحافظ على عقد
خط الأنابيب المتساهل: يبقى العنصر النائب كما كُتب.

### مثال عملي: جمع ICU

<div dir="ltr">

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["Cart.Items"] = "{count, plural, =0 {No items} one {# item} other {# items}}"
});

var localizer = provider.GetRequiredService<IMessageLocalizer>();
var parameters = new Dictionary<string, object?> { ["count"] = 3 };
var text = await localizer.LocalizeAsync("Cart.Items", parameters, new CultureInfo("en"));
// "3 items"
```

</div>

مع <code dir="ltr">count = 0</code> يفوز الفرع الصريح <code dir="ltr">=0</code> وتكون النتيجة
<code dir="ltr">No items</code>. ومع <code dir="ltr">count = 1</code> يفوز الفرع
<code dir="ltr">one</code> ويعرض <code dir="ltr">#</code> القيمة <code dir="ltr">1</code>. ومع
<code dir="ltr">count = 1000</code> يعرض الفرع <code dir="ltr">other</code> القيمة
<code dir="ltr">1,000</code> بالإنجليزية، لأن <code dir="ltr">#</code> يستخدم تنسيق الأرقام
للثقافة المطلوبة.

القالب نفسه بالعربية مع <code dir="ltr">count = 3</code> يختار فئة
<code dir="ltr">few</code>، لذا يجب أن يقدّم القالب العربي فرع <code dir="ltr">few</code>
(وعادةً <code dir="ltr">zero</code> و<code dir="ltr">one</code> و<code dir="ltr">two</code>
و<code dir="ltr">many</code> و<code dir="ltr">other</code>). المحرك يختار الفئة؛ والمترجم يقدّم
الفروع.

### تنسيق الأرقام والتواريخ

داخل ICU، يجب أن تكون القيم الرقمية والتاريخية بالنوع الصحيح وقت التشغيل.
<code dir="ltr">{amount, number, currency}</code> يتطلب قيمة رقمية؛ و
<code dir="ltr">{when, date, long}</code> يتطلب <code dir="ltr">DateTime</code> أو
<code dir="ltr">DateTimeOffset</code> أو <code dir="ltr">DateOnly</code>. وتمرير سلسلة يرمي
<code dir="ltr">FormatException</code>. هذا الصرامة مقصودة: كان دمج السلاسل الصامت سيخفي خطأ
النوع حتى يكشفه مترجم أو ثقافة.

## واجهات الجمع

يوجد شكلان للجمع.

### واجهة زوج المفاتيح

تأخذ <code dir="ltr">LocalizePluralAsync</code> مفتاحاً مفرداً ومفتاحاً جمعاً:

<div dir="ltr">

```csharp
var text = await localizer.LocalizePluralAsync(
    "Cart.One",
    "Cart.Other",
    count,
    new Dictionary<string, object?> { ["count"] = count },
    CultureInfo.CurrentUICulture);
```

</div>

يُختار الشكل بواسطة
<code dir="ltr">PluralRules.GetCardinalCategory(culture, count)</code>:

- <code dir="ltr">PluralCategory.One</code> يختار المفتاح المفرد.
- كل فئة أخرى تختار مفتاح الجمع.

يُحل كل مفتاح عبر سلسلة ارتداد الثقافة نفسها كبحث عادي. وتُحسب الفئة في الثقافة المطلوبة حتى
عندما يأتي القالب الفائز من أب أو من الثقافة الافتراضية. وإذا غاب المفتاح المفضل، يجرّب المُعرِّب
المفتاح الآخر؛ وإذا غاب الاثنان فالنتيجة <code dir="ltr">null</code>. ولا يُحقن العدد في قاموس
الوسائط، لذا تحتاج القوالب التي تعرض <code dir="ltr">{count}</code> إلى تزويد المستدعي به.

بالنسبة للغات التي تملك أكثر من شكلين معروضين، تمثل واجهة الزوج هذه تدهوراً موثّقاً: إنها تفرّق
بين <code dir="ltr">one</code> و"ليس واحداً"، ولا تختار قوالب <code dir="ltr">few</code> أو
<code dir="ltr">many</code> أو <code dir="ltr">two</code> منفردة. استخدم فروع جمع ICU، أو مزوّداً
يخدم جداول جمع لكل صيغة.

### جداول المزوّد لكل صيغة

إذا نفّذ المزوّد المحلول <code dir="ltr">IPluralFormMessageProvider</code>، سأله المُعرِّب أولاً
عن شكل مراعٍ للعدد. ينفّذ مزوّد PO (Portable Object) ذلك عندما يحمل الكتالوج ترويسة
<code dir="ltr">Plural-Forms</code>. ويختار المزوّد فهرس <code dir="ltr">msgstr</code> الدقيق
للعدد والثقافة. وإذا كان الشكل المختار فارغاً أو الإدخال مفقوداً، ينتقل المُعرِّب إلى مسار زوج
المفاتيح، فيحتفظ المزوّدون بلا دعم لكل صيغة بسلوكهم الموثّق.

يبقى المزوّد مطابقاً تماماً لكل ثقافة. وينادي المُعرِّب المزوّد مرة واحدة لكل خطوة ارتداد، تماماً
كما يفعل في عمليات البحث العادية.

## تغطية فئات الجمع

<code dir="ltr">PluralRules.GetCardinalCategory</code> مجموعة فرعية من CLDR منقولة يدوياً، وتغطي
اللغات الجرمانية والرومانسية والعربية والسلافية والبلطية والقلطية، والعبرية، والمالطية، وعدة لغات
أحادية الشكل. تستخدم القيم السالبة المقدار؛ وتُحل اللغات المجهولة إلى
<code dir="ltr">Other</code>. وتغطي الأعداد الترتيبية الإنجليزية فقط.

القيود الموثّقة: المطابقة حسب اللغة المكوّنة من حرفين، لذا تتبع <code dir="ltr">pt-PT</code>
قواعد <code dir="ltr">pt</code> لا القواعد الكتالونية الخاصة بالمنطقة؛ ومعاملات الترميز المضغوط
(<code dir="ltr">c</code>/<code dir="ltr">e</code>) خارج النطاق؛ واللغات خارج الجدول تُحل إلى
<code dir="ltr">Other</code>.

**مأزق:** <code dir="ltr">PluralCategory.Other</code> لا يعني "الجمع". إنه يعني "لا شيء من
الفئات الأخرى". بالنسبة للإنجليزية هو صيغة الجمع؛ وبالنسبة للعربية فئة متمايزة. ألّف كل فئة
يختارها القالب الهدف.

## تنسيق آمن لـ HTML

يمرّر <code dir="ltr">HtmlSafeMessageFormatter</code> ترميز القالب الموثوق، ويرمّز قيم الوسائط
بـ HTML باستخدام <code dir="ltr">HtmlEncoder.Default</code>؛ وتمر القيم الرقمية والمنطقية
والتاريخية بالهوية كي يظل ICU يعمل. ولا يعقّم القوالب. الناتج HTML مرمّز بالفعل ويجب إخراجه
خاماً؛ ومصب HTML عادي سيعيد ترميزه مرتين.

## تنسيق Markdown

يعرض <code dir="ltr">MarkdownMessageFormatter</code> مجموعة فرعية موثّقة من CommonMark أولاً، ثم
يسلّم HTML إلى منسّق داخلي للاستبدال. العرض أولاً يعني أن قيم الوسائط لا تُحلّل أبداً كـ Markdown
أو HTML. المنسّق الداخلي الافتراضي هو <code dir="ltr">HtmlSafeMessageFormatter</code> (وهو نفسه
يفترض <code dir="ltr">DefaultMessageFormatter</code>)، لذا يحمل
<code dir="ltr">new MarkdownMessageFormatter()</code> ضمان الترميز المسبق. ركّب صراحةً من أجل ICU
أو الوسائط المرمّزة مسبقاً:

<div dir="ltr">

```csharp
new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter()));
new MarkdownMessageFormatter(new DefaultMessageFormatter());
```

</div>

الكتل: فقرات وقوائم بسيطة مرقمة وغير مرقمة. والسطور: التوكيد، والتوكيد القوي، وامتدادات الكود
بعلامة خلفية واحدة، والروابط، وهروب الشرطة المائلة العكسية. التراكيب خارج المجموعة الفرعية تُعرض
حرفياً. HTML الخام خامل ما لم يُفعَّل <code dir="ltr">allowRawHtml</code>. الناتج HTML ويجب
إخراجه خاماً؛ وتُرمَّز وجهات الروابط كسمات، لكن فحص المخطط (scheme) يبقى مسؤولية المؤلف.

## قواعد الهروب في لمحة

| السياق | القاعدة |
|---|---|
| العنصر النائب الافتراضي | <code dir="ltr">{key}</code>؛ المفاتيح المجهولة تبقى حرفية؛ لا محددات تنسيق |
| ICU | <code dir="ltr">''</code> لفاصلة عليا حرفية؛ <code dir="ltr">'{...}'</code> لنص حرفي؛ <code dir="ltr">#</code> داخل الجمع |
| قالب Markdown | هروب بالشرطة المائلة العكسية للمحارف الخاصة؛ HTML الخام خامل افتراضياً |
| وسيط HTML | يرمّزه <code dir="ltr">HtmlSafeMessageFormatter</code>؛ القوالب موثوقة |
| ناتج Markdown | HTML بالفعل؛ أخرجه خاماً ولا ترمّزه كـ HTML مرة أخرى |

## ملاحظات الأداء

- يستخدم المنسّق الافتراضي تعبيراً نمطياً مجمّعاً (compiled regex) ويخصص سلسلة نتيجة واحدة لكل
  نداء.
- يحلّل ICU فقط عند وجود تركيب؛ والقوالب المعقدة تُحلَّل في كل نداء.
- يخصص Markdown منشئاً (builder) ومصفوفة أسطر في كل نداء، ولا يخزّن القوالب المعروضة مؤقتاً.
- كلفة الارتداد يهيمن عليها إدخال/إخراج المزوّد؛ والقواميس داخل العملية رخيصة.
- خطافات التشخيص محمية بمستمع، لذا لا يدفع المضيفون غير المراقبين إلا فحوص الحماية.

لعائلات القوالب الساخنة، خزّن السلسلة المنسّقة مؤقتاً في موضع النداء بمفتاح ثقافة. لا تخزّنها
مؤقتاً داخل منسّق مشترك؛ فليس لديه إشارة إبطال، والوسائط تتغير في كل نداء.

## إلى أين بعد ذلك

- [الثقافات والارتداد](03-cultures-and-fallback.md) لكيفية وصول الثقافة المطلوبة إلى المنسّق.
- [الترجمة الآلية](12-machine-translation.md) لتفاعل القوالب المترجمة مع التنسيق
  والتخزين المؤقت.
- [الأداء](../04-operations/03-performance.md) للقياس وتخطيط السعة.

</div>
