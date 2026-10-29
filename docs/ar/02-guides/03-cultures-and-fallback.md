<div dir="rtl">

# الثقافات والارتداد (cultures and fallback)

التعامل مع الثقافة (culture) هو الموضع الذي تنحرف فيه نماذج التعريب عن توقعات المطوّرين أكثر من
أي موضع آخر. يغطي هذا الفصل تسمية الثقافات، وسلاسل الآباء (parent chains)، والثقافة الافتراضية،
واختيار ثقافة الطلب (request culture)، والاختبارات التي تحافظ على صدق سلوك الارتداد.

## أسماء الثقافات والآباء

اسم ثقافة .NET معرّف على نمط BCP-47: <code dir="ltr">en</code> و
<code dir="ltr">en-US</code> و<code dir="ltr">de-CH</code> و<code dir="ltr">sr-Latn-RS</code>
و<code dir="ltr">zh-Hans-CN</code>. ويكشف <code dir="ltr">CultureInfo</code> الاسم والأب
(<code dir="ltr">Parent</code>):

- <code dir="ltr">new CultureInfo("de-CH").Parent.Name</code> هو <code dir="ltr">"de"</code>.
- <code dir="ltr">new CultureInfo("fr-CA").Parent.Name</code> هو <code dir="ltr">"fr"</code>.
- <code dir="ltr">new CultureInfo("sr-Latn-RS").Parent.Name</code> هو
  <code dir="ltr">"sr-Latn"</code>، وأبوه <code dir="ltr">"sr"</code>.
- <code dir="ltr">new CultureInfo("ar").Parent</code> هو الثقافة الثابتة (invariant culture).
- <code dir="ltr">CultureInfo.InvariantCulture.Name</code> هو السلسلة الفارغة.

سلسلة الآباء هي أساس ارتداد المُعرِّب (localizer fallback). وهي ليست بحثاً عن "أقرب كتالوج"؛ بل
مسير حتمي صاعد في هرم الثقافات.

### الثقافات المحايدة والمحددة (neutral and specific cultures)

الثقافة المحايدة (neutral culture) مثل <code dir="ltr">de</code> تمثل لغة بلا منطقة. والثقافة
المحددة (specific culture) مثل <code dir="ltr">de-CH</code> تمثل لغة داخل منطقة. كثيراً ما تخزّن التطبيقات الترجمات عند المستوى
المحايد وتترك الطلبات المحددة ترتد إليه. يعمل ذلك لأن أب <code dir="ltr">de-CH</code> هو
<code dir="ltr">de</code>.

وهذا يعني أيضاً أن الكتالوج قد يبدو مكتملاً وهو ليس كذلك. إذا طلب مستخدم
<code dir="ltr">de-CH</code> ولم يوجد إلا <code dir="ltr">de</code>، فسيُحلّ كل مفتاح. وإذا كان
المنتج يحتاج صياغة سويسرية خاصة، فإن غياب <code dir="ltr">de-CH</code> يصبح غير مرئي وقت
التشغيل. تعامل مع الكتالوج المحايد كارتداد صالح، لا كدليل على تغطية الثقافة المحددة.

في مزوّدات الملفات، اسم الملف هو مفتاح الثقافة. يخدم <code dir="ltr">de.json</code> الثقافة
المحايدة؛ ويخدم <code dir="ltr">de-CH.json</code> الثقافة المحددة. تستخدم اصطلاحات تعدد الثقافات
جذر الملف كرمز ثقافة، لذا ليس <code dir="ltr">de-CH.json</code> نسخة من
<code dir="ltr">de.json</code> من وجهة نظر المزوّد. أما المُعرِّب فيربطهما عبر سلسلة الآباء.

### الثقافة الثابتة (invariant culture)

الثقافة الثابتة (invariant culture) ليست لغة، ويجب ألا تكون كتالوجاً أبداً. يستبعدها المُعرِّب من
مسير الآباء. وإذا كانت الثقافة المطلوبة محايدة، تُتخطى حلقة الآباء بالكامل، وتكون الثقافة
الافتراضية، إن كانت مكوَّنة، المرشح الوحيد.

توجد القاعدة لسبب: كثير من الثقافات يكون أبوها الثقافة الثابتة، ولذلك كان بحث الثقافة الثابتة سيطابق
بصمت دلو ثقافة محايداً لم يؤلّفه أحد عن قصد. واستبعادها يجعل سلسلة الارتداد تنتهي عند ثقافة
افتراضية حقيقية، لا عند ثقافة عرَضية.

## خوارزمية الارتداد الواحدة

ينفّذ <code dir="ltr">DefaultMessageLocalizer</code> خوارزمية واحدة بالضبط:

<div dir="ltr">

```text
requested culture
  -> parent
  -> parent of parent
  -> ... until the parent chain reaches invariant (exclusive)
  -> configured default culture, once
  -> null
```

</div>

المزوّدات مطابقة تماماً (exact-match). لا تمشي في الآباء أبداً، ولا تطبّق ثقافة افتراضية، ولا
تنادي مزوّداً آخر. هذا الفصل هو ما يجعل الارتداد قابلاً للاختبار: يمكنك استبدال مزوّد يسجّل كل
ثقافة يُسأل عنها وتؤكد التسلسل الدقيق.

الثقافة الافتراضية اختيارية. في DI (Dependency Injection)، تسجّل <code dir="ltr">AddLocalization()</code> كائن
<code dir="ltr">LocalizationOptions</code> بقيمة
<code dir="ltr">DefaultCulture = "en"</code>. ويستطيع التحميل المُكوِّن تعيين قيمة أخرى. وتعيين
سلسلة فارغة يعطّل خطوة الثقافة الافتراضية الأخيرة، فيُحلّ البحث فقط عبر الثقافة المطلوبة
وآبائها.

تُلتقط الثقافة الافتراضية عند إنشاء المُعرِّب. ولا تُقرأ من الخيارات في كل بحث. وفي تركيب DI
عادي يكون المُعرِّب singleton، لذا تعامل مع الثقافة الافتراضية كتكوين بدء تشغيل.

## توزيع الثقافة على مستوى المزوّد

تلفّ بعض عمليات التسجيل مزوّداً واحداً لكل ثقافة داخل
<code dir="ltr">CultureAwareMessageProvider</code>. هذا ما تفعله تحميلات التسجيل متعددة الثقافات
لـ JSON وXML وPO. سلوك الموزّع (dispatcher) أضيق من ارتداد المُعرِّب:

1. إذا كان الاسم الكامل للثقافة المطلوبة مسجّلاً، فاستخدم ذلك المزوّد.
2. وإلا، إذا كان اسم اللغة ISO المكوّن من حرفين مسجّلاً، فاستخدم ذلك المزوّد.
3. وإلا، فأعد <code dir="ltr">null</code>.

لا يمشي في آباء إضافية، ويمرّر الثقافة المطلوبة الأصلية إلى المزوّد المختار. ويظل المُعرِّب مالك
مسير الآباء الكامل. الموزّع تسهيل للتخطيط الشائع <code dir="ltr">de</code>/<code dir="ltr">de-CH</code>،
وليس محرك ارتداد ثانياً.

هذا يخلق سلوكاً دقيقاً لكنه مفيد: لطلب في <code dir="ltr">de-CH</code> مع مزوّدين مسجّلين لـ
<code dir="ltr">de</code> و<code dir="ltr">en</code>، يستطيع الموزّع حل مزوّد
<code dir="ltr">de</code> في خطوة المُعرِّب الأولى فقط إذا سأل المُعرِّب عن
<code dir="ltr">de</code>. عملياً يسأل المُعرِّب عن <code dir="ltr">de-CH</code> أولاً، فيخطئ
الموزّع، ثم يسأل المُعرِّب عن <code dir="ltr">de</code> فيصيب الموزّع. ويظل عدد استعلامات المزوّد
الظاهر تابعاً لسلسلة المُعرِّب.

لا تستخدم مزوّدات ResX وsatellite الموزّع. ينادي
<code dir="ltr">ResXMessageProvider</code> الدالة
<code dir="ltr">ResourceManager.GetResourceSet</code> مع تعطيل استكشاف الآباء، ويستخدم
<code dir="ltr">SatelliteMessageProvider</code> القيمة
<code dir="ltr">tryParents: false</code>. وكلاهما يحل الثقافة المطلوبة بدقة ويترك الارتداد
للمُعرِّب.

## اختيار ثقافة الطلب

في ASP.NET Core، تختار <code dir="ltr">RequestLocalizationMiddleware</code> الخاصة بالإطار ثقافة
الطلب من مزوّدات مثل سلسلة الاستعلام (query string) وملف تعريف الارتباط (cookie) و
<code dir="ltr">Accept-Language</code>. وتكوّن حزمة
<code dir="ltr">Jeninnet.Localization.Integrations.RequestLocalization</code> ذلك الوسيط من
مخزن التعريب، وتضيّق النتائج إلى الثقافات التي يستطيع التطبيق خدمتها فعلاً.

تسجّل <code dir="ltr">AddLocalizationRequestCultures</code> تكويناً يتضمن:

- **الثقافات المخدومة** — <code dir="ltr">LocalizationRequestCultureOptions.SupportedCultures</code>
  الصريحة عند تعيينها؛ وإلا <code dir="ltr">ILocalizationStore.GetSupportedCulturesAsync()</code>.
  وتكون الثقافة الافتراضية المحلولة جزءاً من المجموعة المخدومة دائماً.
- **ثقافة الطلب الافتراضية** — <code dir="ltr">options.DefaultCulture</code> الصريحة، وإلا
  <code dir="ltr">LocalizationOptions.DefaultCulture</code> المسجّلة، وإلا
  <code dir="ltr">"en"</code>.
- **ترتيب المزوّدات** — قيمة المسار (route value)، ثم سلسلة الاستعلام، ثم ملف تعريف الارتباط، ثم
  <code dir="ltr">Accept-Language</code>. مزوّد المسار هو
  <code dir="ltr">RouteValueRequestCultureProvider</code> مع مفتاح مسار قابل للتكوين
  <code dir="ltr">RouteCultureKey</code> (افتراضياً <code dir="ltr">"culture"</code>).

تضيّق <code dir="ltr">ServedCultureRequestCultureProvider</code> أي نتيجة مزوّد إلى المجموعة
المخدومة: اسم الثقافة الدقيق أولاً، ثم ارتداد الآباء المحدود المكوَّن في الإطار، وإلا ثقافة الطلب
الافتراضية. لا يغيّر الجسر الثقافة المحيطة (ambient culture) أبداً؛ ويطبّق وسيط الإطار الثقافة
المحلولة لكل طلب كالمعتاد.

سلوك الفشل مقصود:

| الحالة | السلوك |
|---|---|
| المخزن متاح ويبلّغ عن صفر ثقافات | فشل بدء التشغيل باستثناء قابل للتنفيذ |
| المخزن غير متاح، أو لا يوجد مخزن مسجّل | تصبح المجموعة المخدومة الثقافة الافتراضية فقط، مع تحذير عند توفر التسجيل |
| اسم ثقافة مكوَّن غير معروف | فشل بدء التشغيل مع تسمية القيمة |
| الثقافة الافتراضية ليست في المجموعة المخدومة | تُضاف الافتراضية إلى المجموعة |

الصف الثاني مفاجأة إنتاجية شائعة: إذا لم يُسجَّل مخزن ولم تُكوَّن ثقافات صريحة، فلن يستطيع
التطبيق خدمة الثقافة الافتراضية فقط. وطلب <code dir="ltr">ar</code> لن يختار
<code dir="ltr">ar</code> لمجرد أن مزوّداً يحتوي ملفات عربية. جسر ثقافة الطلب يقرأ المخزن، لا
مزوّد الرسائل.

في Blazor، لإدارة الثقافة نطاقان. تسجّل Blazor Server خدمة
<code dir="ltr">BlazorCultureService</code> محدودة النطاق لكل دائرة (circuit)، مبذورة من ثقافة
الطلب ولا تغيّر الثقافة المحيطة أبداً. أما مضيفات النطاق الساكن (WebAssembly وHybrid) فتسجّل خدمة
singleton تطبّق ثقافة العملية المحيطة وتُطلق
<code dir="ltr">CultureChanged</code>. راجع [Blazor](09-blazor.md).

## مثال عملي: تتبع السلسلة

يستخدم المثال التالي على نمط الاختبار مزوّد تسجيل (recording provider). المزوّد نفسه بسيط عمداً:
يخزّن إدخالات مفتاحية بالشكل <code dir="ltr">culture:key</code> ويسجّل كل ثقافة يُسأل عنها.

<div dir="ltr">

```csharp
sealed class RecordingProvider(IReadOnlyDictionary<string, string> messages) : IMessageProvider {
    public List<string> QueriedCultures { get; } = [];

    public ValueTask<string?> GetMessageTemplateAsync(
        string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        QueriedCultures.Add(culture.Name);
        return ValueTask.FromResult(
            messages.TryGetValue($"{culture.Name}:{key}", out var value) ? value : null);
    }
}

var provider = new RecordingProvider(new Dictionary<string, string> { ["de:Greeting"] = "Hallo" });
var localizer = new DefaultMessageLocalizer(
    provider,
    new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en" });

var result = await localizer.LocalizeAsync(
    "Greeting", new Dictionary<string, object?>(), new CultureInfo("de-CH"));
// result is "Hallo"; QueriedCultures is ["de-CH", "de"].
```

</div>

| السيناريو | الاستعلامات المتوقعة | النتيجة |
|---|---|---|
| <code dir="ltr">de-CH</code>، والمفتاح موجود في <code dir="ltr">de</code> | <code dir="ltr">de-CH</code>، <code dir="ltr">de</code> | قالب ألماني |
| <code dir="ltr">de</code>، والمفتاح موجود فقط في الافتراضية <code dir="ltr">en</code> | <code dir="ltr">de</code>، <code dir="ltr">en</code> | قالب إنجليزي |
| <code dir="ltr">fr-CA</code>، والمفتاح موجود في <code dir="ltr">fr</code> | <code dir="ltr">fr-CA</code>، <code dir="ltr">fr</code> | قالب فرنسي؛ لا يُستعلم عن الافتراضية |
| <code dir="ltr">fr-CA</code>، والمفتاح غير موجود في أي مكان | <code dir="ltr">fr-CA</code>، <code dir="ltr">fr</code>، <code dir="ltr">en</code> | <code dir="ltr">null</code> |
| طلب الثقافة الثابتة، والمفتاح في <code dir="ltr">en</code> | <code dir="ltr">en</code> | قالب إنجليزي |
| <code dir="ltr">en</code> مفقودة، والافتراضية <code dir="ltr">en</code> | <code dir="ltr">en</code>، <code dir="ltr">en</code> | <code dir="ltr">null</code> |

يوثّق الصف الأخير استعلام الثقافة الافتراضية المكرر عندما تتساوى الافتراضية مع الثقافة المطلوبة.
هذا متوقع وظاهر في مقاييس خطوات الارتداد.

## اختبارات تكشف انحدارات الثقافة

اختبر أربعة أشياء على الأقل:

1. **السلسلة الدقيقة.** استخدم مزوّد تسجيل وأكّد أسماء الثقافات المرتبة. لا تؤكد السلسلة المعادة
   وحدها؛ فالسلسلة الخاطئة قد تعيد السلسلة الصحيحة لسبب خاطئ.
2. **استبعاد الثقافة الثابتة.** أكّد أن أي استعلام لا يحمل اسم الثقافة الفارغ.
3. **وصول الثقافة الافتراضية.** غطّ ثقافة مطلوبة بلا إدخالات وثقافة افتراضية بإدخالات.
4. **اختيار الطلب.** في ASP.NET Core، اختبر جسر الطلب بثقافات مدعومة صريحة، وثقافة غير معروفة،
   ومخزن مفقود.

إذا استخدمت <code dir="ltr">CultureInfo.CurrentUICulture</code> في الاختبارات، فأعدها في كتلة
<code dir="ltr">finally</code>. تسرّب الثقافة المحيطة بين الاختبارات مصدر شائع لإخفاقات تعتمد على
الترتيب.

## مآزق

**مأزق:** استخدام <code dir="ltr">CurrentCulture</code> حيث يُقصد
<code dir="ltr">CurrentUICulture</code>. الأولى تتحكم في التنسيق؛ والثانية تختار موارد واجهة
المستخدم. يستخدم التحميل المريح <code dir="ltr">CurrentUICulture</code>؛ وفي كود الخلفية قد
تختلفان.

**مأزق:** افتراض أن <code dir="ltr">pt-BR</code> و<code dir="ltr">pt-PT</code> لهما قواعد جمع
متمايزة في محرك الجمع. تطابق مجموعة CLDR (Common Locale Data Repository) الفرعية حسب اللغة المكوّنة من حرفين، لذا تستخدم
<code dir="ltr">pt-PT</code> قواعد <code dir="ltr">pt</code> رغم أن CLDR تفرّق بين المنطقة.
القيد موثّق؛ وإذا كان سلوك الجمع الخاص بالمنطقة متطلباً صارماً، فاختر المفاتيح أو القوالب في
مستوى أعلى.

**مأزق:** إنشاء ملف باسم <code dir="ltr">de-CH.json</code> وتوقع أن يخدم كل الطلبات الألمانية.
إنه يخدم <code dir="ltr">de-CH</code> فقط. مسير الآباء في المُعرِّب يصعد من طلب محدد إلى المحايد،
لا ينزل من المحايد إلى المحدد. وطلب <code dir="ltr">de</code> لا يقرأ
<code dir="ltr">de-CH.json</code>.

**مأزق:** الاعتماد على <code dir="ltr">Accept-Language</code> وحده دون جسر ثقافة الطلب. يحتاج
الوسيط إلى ثقافات مدعومة مكوَّنة؛ ويقرأها الجسر من المخزن. راجع
[ASP.NET Core وMVC](08-aspnet-core-and-mvc.md).

## إلى أين بعد ذلك

- [التنسيق وصيغ الجمع](04-formatting-and-plurals.md) لمرحلة التنسيق المراعية
  للثقافة.
- [النموذج الذهني](../01-getting-started/04-mental-model.md) لقواعد اختيار المزوّد والارتداد في
  سياقها.
- [اختبار التعريب](../../en/02-guides/15-testing-your-localization.md) لاستراتيجية اختبار أوسع.

</div>
