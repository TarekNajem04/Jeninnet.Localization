<div dir="rtl">

# واجهة Core API

هذا الفصل مرجع العقد لخط أنابيب الرسائل (message pipeline): واجهة المزوّد (provider)،
والمُعرِّب (localizer) الذي يملك الارتداد (fallback)، والمنسّق (formatter) الذي يعرض الإصابة،
والمحدد (selector) الذي يفرض قاعدة المزوّد الواحد بالضبط، والتسجيل (registration). التوقيعات سطح عام؛
والثوابت المجاورة لها سلوك يجوز للمستهلك الاعتماد عليه.

صفحات الأعضاء المولّدة: <xref:Jeninnet.Localization.Abstractions.IMessageLocalizer>، و
<xref:Jeninnet.Localization.Abstractions.IInnerMessageProvider>، و
<xref:Jeninnet.Localization.Abstractions.IMessageFormatter>.

## أنواع خط الأنابيب

<div dir="ltr">

```csharp
namespace Jeninnet.Localization.Abstractions;

public interface IMessageProvider {
    ValueTask<string?> GetMessageTemplateAsync(
        string key,
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}

public interface IInnerMessageProvider : IMessageProvider;
```

</div>

- <code dir="ltr">key</code> و<code dir="ltr">culture</code> مطلوبان؛ و null يرمي
  <code dir="ltr">ArgumentNullException</code>.
- المزوّد مطابق تماماً فقط. لا يمشي في الآباء أبداً، ولا يطبّق ثقافة افتراضية، ولا يستشير مزوّداً
  آخر.
- الغياب هو <code dir="ltr">null</code>. واستثناءات المزوّد، بما فيها الإلغاء، تنتشر؛ والقراءة
  الفاشلة ليست غياباً أبداً.
- يُفحص الرمز قبل العمل ويُمرَّر إلى الإدخال/الإخراج القابل للإلغاء.

القدرات الاختيارية عقود منفصلة. ويجب أن تتفق
<code dir="ltr">IProviderCapabilities.Supports(feature)</code> مع الواجهات التي تنفّذها النسخة.
والميزات هي <code dir="ltr">None</code> و<code dir="ltr">MultiKeys</code> و
<code dir="ltr">EventsListening</code> و<code dir="ltr">Preload</code> و
<code dir="ltr">Refresh</code> و<code dir="ltr">Caching</code>.

| العقد | الغرض |
|---|---|
| <code dir="ltr">IMultiKeyMessageProvider</code> | تعيد <code dir="ltr">GetMessageTemplatesAsync(keys, culture, token)</code> قاموساً. |
| <code dir="ltr">IPreloadableMessageProvider</code> | تدفّئ <code dir="ltr">PreloadAsync(keys, culture, token)</code> حالة يملكها المزوّد. |
| <code dir="ltr">IRefreshableMessageProvider</code> | تبطل <code dir="ltr">RefreshAsync(token)</code> الحالة المحلية؛ والرمز الملغى يمنع الإبطال. |
| <code dir="ltr">IProviderEventSource</code> | <code dir="ltr">TranslationAcquired</code> / <code dir="ltr">AcquisitionFailed</code> للاكتساب الخارجي. |
| <code dir="ltr">IPluralFormMessageProvider</code> | بحث جمع مراعٍ للعدد، مع سياق اختياري. |
| <code dir="ltr">IWasmCompatibleMessageProvider</code> | علامة: الحل لا ينفّذ إدخال/إخراج ملفات أو قواعد بيانات أو شبكة. |

## المُعرِّب

<div dir="ltr">

```csharp
public interface IMessageLocalizer {
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters,
        CultureInfo culture, CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count,
        IReadOnlyDictionary<string, object?> parameters, CultureInfo culture,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count,
        IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeWithContextAsync(string key, string context,
        IReadOnlyDictionary<string, object?> parameters, CultureInfo culture,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeWithContextAsync(string key, string context,
        IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
}
```

</div>

التحميل بلا ثقافة يستخدم <code dir="ltr">CultureInfo.CurrentUICulture</code>؛ وليس خوارزمية
مختلفة. ويتعامل <code dir="ltr">LocalizePluralAsync</code> مع الإشارة كمقدار.

<div dir="ltr">

```csharp
public DefaultMessageLocalizer(
    IMessageProvider provider,
    IMessageFormatter formatter,
    LocalizationOptions? options);
```

</div>

- تُلتقط الثقافة الافتراضية عند الإنشاء. و<code dir="ltr">null</code>، أو قيمة
  <code dir="ltr">DefaultCulture</code> الفارغة، يعطّل خطوة الثقافة الافتراضية الأخيرة.
- السلسلة هي الثقافة المطلوبة، ثم الآباء، ثم الثقافة الافتراضية مرة واحدة. ولا تُستعلم الثقافة
  الثابتة أبداً.
- الغياب يعيد <code dir="ltr">null</code> ولا ينادي المنسّق.
- استثناءات المنسّق والمزوّد تنتشر. ولا يُحوَّل الإلغاء إلى <code dir="ltr">null</code> أبداً.
- يسأل <code dir="ltr">LocalizePluralAsync</code> أولاً
  <code dir="ltr">IPluralFormMessageProvider</code>. وإذا لم يعط شيئاً، تختار فئة CLDR (Common Locale Data Repository) المفتاح:
  <code dir="ltr">One</code> يختار المفرد، وكل فئة أخرى تختار الجمع. وإذا غاب المفتاح المفضل،
  يُجرَّب المفتاح الآخر.
- يبني <code dir="ltr">LocalizeWithContextAsync</code> القيمة
  <code dir="ltr">context + '\u0004' + key</code>؛ والسياق الفارغ يتصرف كالمفتاح العادي.

المُعرِّب singleton. يحمل المزوّد والمنسّق ولقطة الخيارات والمصب، لذا تعامل مع تكوين الثقافة
الملتقط كتكوين بدء تشغيل.

## المنسّقات

<div dir="ltr">

```csharp
public interface IMessageFormatter {
    string Format(string template, IReadOnlyDictionary<string, object?> parameters);
}
public interface ICultureAwareMessageFormatter : IMessageFormatter {
    string Format(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture);
}
```

</div>

يستبدل <code dir="ltr">DefaultMessageFormatter</code> العناصر النائبة المسمّاة
<code dir="ltr">{...}</code>:

- الوسيط المفقود يبقى حرفياً؛ والقيمة null تصبح فارغة.
- التحويل هو <code dir="ltr">value?.ToString()</code>؛ ولا توجد معالجة لسلسلة تنسيق.
- <code dir="ltr">{0}</code> مفتاحه <code dir="ltr">0</code>؛ و
  <code dir="ltr">{name:N2}</code> مفتاحه <code dir="ltr">name:N2</code>، لذا لن يطابق
  <code dir="ltr">name</code>.
- إنه غير مراعٍ للثقافة؛ وتستخدم التحويلات الثقافة المحيطة
  <code dir="ltr">CurrentCulture</code>.

<code dir="ltr">IcuMessageFormatter</code> هو
<code dir="ltr">ICultureAwareMessageFormatter</code> اختياري: والتسجيل هو التفعيل. وتمر القوالب
الخالية من تركيب ICU (International Components for Unicode) عبر المنسّق الداخلي. وترمي الأنماط المشوّهة وعدم تطابق الأنواع
<code dir="ltr">FormatException</code>؛ ويبقي الوسيط البسيط المفقود العنصر النائب حرفياً.

يرمّز <code dir="ltr">HtmlSafeMessageFormatter</code> قيم الوسائط لكنه يثق بترميز القالب؛ ودالته
الساكنة <code dir="ltr">EncodeArgument(object?)</code> هي العنصر الأولي المشترك في جسر MVC.
ويعرض <code dir="ltr">MarkdownMessageFormatter</code> مجموعة فرعية من CommonMark قبل منسّقه
الداخلي. وكلاهما ينتج HTML مرمّزاً ويجب إخراجه خاماً.

يوجد منسّق واحد بالضبط لكل تطبيق. سجّله قبل <code dir="ltr">AddLocalization()</code> أو استبدل
الواصف بعده.

## اختيار المزوّد

<div dir="ltr">

```csharp
public sealed class LocalizationProviderSelector : IMessageProvider {
    public LocalizationProviderSelector(IEnumerable<IInnerMessageProvider> providers);
    public ValueTask<string?> GetMessageTemplateAsync(...);
}
```

</div>

- صفر مزوّدين: كل بحث يعيد <code dir="ltr">null</code>.
- مزوّد واحد: يفوّض المحدد.
- أكثر من واحد: يرمي الإنشاء <code dir="ltr">InvalidOperationException</code> مع تسمية الأنواع
  المحلولة.

لا يوجد تسلسل ولا ترتيب ولا ارتداد على مستوى المزوّد.

يفوّض <code dir="ltr">CultureAwareMessageProvider(IReadOnlyDictionary&lt;string, IMessageProvider&gt; providers)</code>
إلى اسم الثقافة الكامل، ثم إلى اسم اللغة ISO المكوّن من حرفين، ويمرّر
<code dir="ltr">CultureInfo</code> الأصلية، ويعيد <code dir="ltr">null</code> في غير ذلك.
والثقافة الثابتة تعيد <code dir="ltr">null</code>؛ وهو نفسه واعٍ بالجمع.

## الخيارات والتسجيل

<div dir="ltr">

```csharp
namespace Jeninnet.Localization.Abstractions.Options;

public class LocalizationOptions {
    public string DefaultCulture { get; set; } = "en";
    public IMissingKeySink? MissingKeySink { get; set; }
}
```

</div>

<div dir="ltr">

```csharp
namespace Jeninnet.Localization.Core.Extensions;

public static IServiceCollection AddLocalization(this IServiceCollection services);
public static IServiceCollection AddLocalization(this IServiceCollection services,
    Action<LocalizationOptions> configure);
```

</div>

يسجّل التحميل بلا وسائط الخيارات الافتراضية بـ
<code dir="ltr">TryAddSingleton</code>. ويرمي التحميل المُكوِّن إذا كانت الخيارات مسجّلة بالفعل،
ويتحقق من الثقافة، ويسجّل النسخة المكوَّنة. ويسجّل كلاهما:

- <code dir="ltr">IMessageFormatter</code> / <code dir="ltr">IMessageLocalizer</code> /
  <code dir="ltr">IMessageProvider</code>، كلٌّ بـ <code dir="ltr">TryAddSingleton</code>.
- <code dir="ltr">IStringLocalizerFactory</code> → <code dir="ltr">CustomStringLocalizerFactory</code>
  (<code dir="ltr">Replace</code>).
- <code dir="ltr">IStringLocalizer&lt;&gt;</code> → <code dir="ltr">StringLocalizer&lt;&gt;</code>
  الإطاري (<code dir="ltr">TryAddTransient</code>).

ولا يسجّلان <code dir="ltr">ILocalizationStore</code> ولا
<code dir="ltr">IStringLocalizer</code> غير عام. وتتعارض
<code dir="ltr">AddLocalization</code> بالاسم مع امتداد Microsoft.Extensions.Localization.

## المفاتيح المفقودة

<div dir="ltr">

```csharp
public interface IMissingKeySink {
    void ReportMiss(string key, string culture, string? callSite);
}
```

</div>

يشحن Core الأنواع <code dir="ltr">MissingKeyCollector(maxEntries = 1000)</code> (يجمع حسب
المفتاح/الثقافة بسقف)، و<code dir="ltr">LoggerMissingKeySink(ILogger)</code> (تحذير منظم، معرّف
حدث <code dir="ltr">1001</code>)، و
<code dir="ltr">CompositeMissingKeySink(params IMissingKeySink[])</code> (يوزّع بالترتيب). ويحتوي
المُعرِّب استثناءات المصبات؛ فالمصب التشخيصي الفاشل لا يكسر البحث أبداً.

## الجمع والثقافات

<div dir="ltr">

```csharp
public enum PluralCategory { Zero, One, Two, Few, Many, Other }

public static class PluralRules {
    public static PluralCategory GetCardinalCategory(CultureInfo culture, decimal value);
    public static PluralCategory GetOrdinalCategory(CultureInfo culture, decimal value);
}
```

</div>

القواعد مجموعة فرعية من CLDR منقولة يدوياً. وتستخدم المطابقة
<code dir="ltr">TwoLetterISOLanguageName</code>؛ وتُحل اللغات المجهولة إلى
<code dir="ltr">Other</code>؛ وتستخدم القيم السالبة المقدار. وتغطي الأعداد الترتيبية الإنجليزية
فقط. و<code dir="ltr">Other</code> يعني "لا شيء من الفئات الأخرى"، لا "الجمع".

## مثال عملي: بحث واحد

يخدم مزوّد إدخالات <code dir="ltr">de</code> فقط، والثقافة الافتراضية
<code dir="ltr">en</code>. طلب <code dir="ltr">de-CH</code> يستعلم عن
<code dir="ltr">de-CH</code>، فيغيب، ثم يستعلم عن <code dir="ltr">de</code> فيحل. وطلب
<code dir="ltr">fr</code> يستعلم عن <code dir="ltr">fr</code>، ثم عن
<code dir="ltr">en</code>؛ وإذا غاب الاثنان، فالنتيجة <code dir="ltr">null</code> ولا يُنادى
المنسّق. ولا ينتقل أي مكوّن آخر بين الثقافات.

## مآزق

**مأزق:** تسجيل مزوّدين. يرمي المحدد عند الإنشاء؛ ولا توجد سلسلة.

**مأزق:** توقع أن يعمل المنسّق عند الغياب. الغياب <code dir="ltr">null</code> ويُتخطى التنسيق.

**مأزق:** توقع أن يمشي جسر <code dir="ltr">IStringLocalizer</code> في الآباء أو يستخدم العناصر
النائبة المسمّاة. إنه لا يفعل أياً منهما؛ راجع [واجهة التخزين](02-storage-api.md).

## الثوابت في لمحة

- مزوّد واحد بالضبط، أو لا شيء؛ والمزوّدات مطابقة تماماً.
- المُعرِّب هو مالك الارتداد الوحيد.
- الغياب <code dir="ltr">null</code>؛ والخطأ استثناء؛ والإلغاء ليس غياباً أبداً.
- يعمل التنسيق بعد الإصابة فقط؛ ولسطح جسر المخزن دلالات مختلفة.

## إلى أين بعد ذلك

- [واجهة التخزين](02-storage-api.md) لعقود المخزن والجسر.

</div>
