<div dir="rtl">

# JavaScript وتسليم العميل (JavaScript and client delivery)

تستطيع الصفحات المعروضة من الخادم حل الرسائل أثناء بناء الصفحة. أما كود المتصفح فلا يستطيع ذلك.
يحتاج تطبيق الصفحة الواحدة (SPA) أو هيكل WebAssembly أو WebView على سطح المكتب إلى قوالب الرسائل
كبيانات. وتقدّم تكامل JavaScript ذلك بالضبط: قائمة سماح صريحة من القوالب لثقافة واحدة، ككائن JSON
مسطح.

التكامل صغير عمداً. لا يشحن مكتبة عميل JavaScript ولا محرك تنسيق ولا مبدّل ثقافة. إنه يعرّف عقد
الحمولة (payload) ودلالات HTTP؛ ويملك العميل العرض واختيار الثقافة.

## وضعا تسليم، وحمولة واحدة

ينتج <code dir="ltr">JavaScriptResourcePayloadBuilder</code> كائن
<code dir="ltr">JavaScriptResourcePayload</code> لثقافة واحدة:

- <code dir="ltr">Culture</code> — اسم الثقافة المطبَّع.
- <code dir="ltr">Json</code> — كائن <code dir="ltr">{ "key": "template" }</code> مسطح بترتيب
  قائمة السماح.
- <code dir="ltr">Utf8Json</code> — بايتات UTF-8 (Unicode Transformation Format, 8-bit) الدقيقة لمستند JSON ذلك.
- <code dir="ltr">ETag</code> — وسم كيان قوي بين علامتي اقتباس، محسوب كملخص SHA-256 (Secure Hash Algorithm, 256-bit) السادس عشري
  الصغير لتلك البايتات.

يغذّي المنشئ نفسه وضعي التسليم:

- **نقطة النهاية** — تربط <code dir="ltr">MapJavaScriptLocalization()</code> طلب
  <code dir="ltr">GET</code> على نمط المسار المكوَّن (افتراضياً
  <code dir="ltr">/localization/v1/{culture}</code>) وتخدم الحمولة مع
  <code dir="ltr">ETag</code> و<code dir="ltr">Cache-Control</code>.
- **التصدير الساكن** — تكتب <code dir="ltr">ExportToDirectoryAsync</code> ملفات
  <code dir="ltr">{culture}.json</code> تكون بايتاتها مطابقة لأجسام استجابة نقطة النهاية. وتعيد
  <code dir="ltr">ExportAsync</code> الحمولات في الذاكرة.

تسلسل واحد يغذّي الاثنين. فلا يستطيع تصدير وقت البناء واستجابة وقت التشغيل أن يختلفا حول شكل JSON
أو ترتيبه أو هروبه.

## التسجيل وربط نقطة النهاية

<div dir="ltr">

```csharp
using Jeninnet.Localization.Integrations.JavaScript.Extensions;

services.AddJavaScriptLocalization(options => {
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.AllowedKeys.Add("App.Title");
    options.AllowedKeys.Add("Catalog.Heading");
    options.CacheControlHeader = "public, max-age=0, must-revalidate";
});

var app = builder.Build();
app.MapJavaScriptLocalization();
```

</div>

يتحقق التسجيل من الخيارات فوراً. يجب أن يحتوي نمط المسار على
<code dir="ltr">{culture}</code>؛ ويجب أن تكون قائمة السماح غير فارغة وغير فارغة القيم وخالية من
التكرار؛ ويجب أن يُعرَف كل اسم ثقافة مدعوم. وتسجيل التركيب مرتين يرمي استثناءً.

تُربط نقطة النهاية كـ <code dir="ltr">RequestDelegate</code>، لا كمفوّض حد أدنى لواجهة API مرتبط
بوسائط. هذا يتجنب الربط القائم على الانعكاس ويبقي نقطة النهاية صالحة في مضيفات trimmed وNative
AOT (Ahead-of-Time). ويجب تسجيل خط أنابيب <code dir="ltr">IMessageProvider</code> الأولي (الدالة
<code dir="ltr">AddLocalization()</code> في Core مع مزوّد واحد بالضبط) قبل حل المنشئ.

## الحمولة

تسأل <code dir="ltr">JavaScriptResourcePayloadBuilder.BuildAsync(culture)</code> المزوّد عن كل
مفتاح في قائمة السماح للثقافة **الدقيقة**. وتُحذف المفاتيح التي لا يخدمها المزوّد لتلك الثقافة؛
ولا تُملأ من ثقافة أب. هذه قاعدة المطابقة التامة نفسها في بقية طبقة المزوّد. وارتداد الثقافة يملكه
<code dir="ltr">DefaultMessageLocalizer</code> ولا يُعاد تنفيذه هنا.

<div dir="ltr">

```text
GET /localization/v1/ar
ETag: "3ab6…"
Cache-Control: public, max-age=0, must-revalidate

{ "App.Title": "مكتبة …", "Catalog.Heading": "الفهرس" }
```

</div>

قائمة السماح هي حد الأمان. لا يُطلب من المزوّد إلا المفاتيح المدرجة، لذا يستحيل تفريغ المخزن كله.
والقائمة مرتبة: يحافظ كائن JSON على ترتيب التصريح، ما يجعل الحمولات حتمية وسهلة المقارنة.

يستخدم المسلسل مشفّر System.Text.Json لـ JavaScript. تُهرَّب علامات الاقتباس والشرطات المائلة
العكسية ومحارف التحكم والمحارف الحساسة لـ HTML
(<code dir="ltr">&lt;</code> و<code dir="ltr">&gt;</code> و<code dir="ltr">&amp;</code> و
<code dir="ltr">'</code> و<code dir="ltr">+</code>) وU+2028/U+2029 والمحارف غير ASCII. لذا تكون
البايتات نفسها آمنة كجسم JSON أو كائن <code dir="ltr">&lt;script&gt;</code> مضمّن أو سياق سلسلة
JavaScript. ويعيد <code dir="ltr">JSON.parse</code> النص الأصلي للقالب.

هذه سلامة سياق JavaScript، لا تعقيم HTML. إذا كتب العميل قالباً في DOM عبر
<code dir="ltr">innerHTML</code>، فسيظل الترميز في قالب موثوق ترميزاً. قيم المخزن بيانات؛ وقرر
عند حدود العميل بين استخدام <code dir="ltr">textContent</code> أو مصب HTML مُراجَع. راجع
[الأمان](../04-operations/02-security.md).

## تبديل الثقافة والتخزين المؤقت

تبديل الثقافة قرار العميل. التدفق النموذجي يخزّن الثقافة المختارة، ويطلب الحمولة المطابقة، ويعيد
العرض:

<div dir="ltr">

```js
const culture = localStorage.getItem("culture") ?? "en";
const response = await fetch(`/localization/v1/${encodeURIComponent(culture)}`, {
  headers: { "Accept": "application/json" }
});

if (!response.ok) {
  throw new Error(`Localization payload unavailable for ${culture}`);
}

const messages = await response.json();
const t = (key) => Object.hasOwn(messages, key) ? messages[key] : key;
```

</div>

دلالات HTTP مصممة لإعادة التحقق لا للتخزين المؤقت الثابت طويل العمر:

- <code dir="ltr">ETag</code> تجزئة محتوى قوية بين علامتي اقتباس. البايتات المتساوية تنتج وسوماً
  متساوية؛ والبايتات المتغيرة تنتج وسماً مختلفاً.
- <code dir="ltr">If-None-Match</code> المطابق يعيد
  <code dir="ltr">304 Not Modified</code> بلا جسم. والمقارنة هي المقارنة الضعيفة في RFC 9110:
  تُقبل صيغ <code dir="ltr">W/"…"</code>، ويطابق <code dir="ltr">*</code>.
- <code dir="ltr">Cache-Control</code> افتراضياً
  <code dir="ltr">public, max-age=0, must-revalidate</code>. وتعيين
  <code dir="ltr">CacheControlHeader</code> إلى null أو سلسلة فارغة يحذف الترويسة. ولأن حمولات
  الثقافات ليست لكل مستخدم، يمكن لذاكرة مشتركة أو CDN الاحتفاظ بها، لكن الافتراضي يجعل العملاء
  يعيدون التحقق.
- الثقافات غير المعروفة والثقافات خارج <code dir="ltr">SupportedCultures</code> تعيد
  <code dir="ltr">404</code>.

تبني نقطة النهاية الحمولة في كل طلب. وليس لها ذاكرة مؤقتة على مستوى المزوّد. ومع مزوّد ملفات أو
ذاكرة يكون ذلك رخيصاً؛ ومع مزوّد قاعدة بيانات، ينفّذ كل طلب بحثاً واحداً في المزوّد لكل مفتاح في
قائمة السماح. وإذا كانت هذه الكلفة مهمة، فضع ذاكرة أمامية أو CDN، أو استخدم التصدير الساكن.
إعادة التحقق بـ ETag تتجنب جسم الاستجابة، لا استعلامات المزوّد التي تحسب الوسم.

**ملاحظة:** <code dir="ltr">SupportedCultures</code> الفارغة تخدم أي ثقافة معروفة. هذا مناسب
لدليل كتالوج يُكتشف عند بدء التشغيل، لكنه يعني أيضاً أن نقطة النهاية ستجيب لكل ثقافة يتعرف عليها
وقت التشغيل. صرّح بالمجموعة المخدومة عندما يكون للمنتج قائمة لغات ثابتة.

## التصدير وقت البناء

كثيراً ما لا تستطيع الاستضافة الساكنة أو الهياكل دون اتصال أو حزم WebAssembly نداء نقطة نهاية وقت
التشغيل، أو لا ينبغي لها ذلك. صدّر البايتات نفسها وقت البناء:

<div dir="ltr">

```csharp
var builder = new JavaScriptResourcePayloadBuilder(
    provider.GetRequiredService<IMessageProvider>(),
    options);

await builder.ExportToDirectoryAsync("wwwroot/localization", ["en", "ar", "ar-SY"]);
```

</div>

تُتخطى الثقافات غير المعروفة أو غير الموجودة في
<code dir="ltr">SupportedCultures</code>. ويُنشأ الدليل عند غيابه. ويُسمّى كل ملف
<code dir="ltr">{culture}.json</code> ويكون مطابقاً بايتياً لجسم نقطة النهاية، فيستطيع العميل
التبديل بين التسليم الساكن والديناميكي دون تغيير محلله.

يستخدم التصدير مخطط المزوّد نفسه، أي أنه يلاحظ حالته الحالية وقت التصدير. وإذا كان المزوّد قاعدة
بيانات، فصدّر أثناء النشر بعد أن يصبح الكتالوج نهائياً، لا أثناء معالجة الطلبات.

## مثال عملي: تطبيق صفحة واحدة بقائمة سماح مُراجَعة

يملك فريق منتج كتالوج JSON بأربعمئة مفتاح، لكن واجهة المتجر العامة تحتاج فقط سلاسل التنقل
والسلة والدفع. ينشئون قائمة سماح صريحة بستين مفتاحاً ويسجّلون نقطة النهاية مع
<code dir="ltr">SupportedCultures</code> بقيم <code dir="ltr">en</code> و
<code dir="ltr">ar</code> و<code dir="ltr">de</code>.

يحمل التطبيق <code dir="ltr">/localization/v1/en</code> عند البدء، ويخزّن الحمولة في الذاكرة،
ويعيد الجلب عند تبديل اللغة. ويخزّن عامل الخدمة (service worker) الاستجابات الناجحة حسب URL
ويرسل <code dir="ltr">If-None-Match</code> عند إعادة التحقق؛ فتعيد الحمولات غير المتغيرة
<code dir="ltr">304</code>. أما صفحة الدفع، التي تعمل في WebView منفصل بلا وصول شبكي، فتشحن
ملفات <code dir="ltr">en.json</code> و<code dir="ltr">de.json</code> المولّدة بـ
<code dir="ltr">ExportToDirectoryAsync</code>.

لا تعبر المفاتيح الثلاثمئة والأربعون المتبقية الشبكة أبداً. وعند إضافة مفتاح جديد في الواجهة العامة، تصبح قائمة
السماح تغيير كود قابلاً للمراجعة، وتبلّغ أداة التكافؤ عما إذا كان المفتاح الجديد موجوداً في
الثقافات الثلاث. راجع
[التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md).

## مآزق

**مأزق:** توقع ارتداد ثقافة الأب في الحمولة. المفتاح الموجود في
<code dir="ltr">ar</code> فقط غائب من <code dir="ltr">ar-SY</code>؛ ويحصل العميل على
<code dir="ltr">{}</code> لذلك المفتاح ما لم يخدم المضيف ثقافة الأب أو يدرج ثقافة الارتداد
صراحةً.

**مأزق:** تفريغ مخزن بإضافة كل مفتاح معروف إلى قائمة السماح. القائمة قرار نشر، لا قائمة راحة.
تعامل معها كجزء من سطح API العام.

**مأزق:** افتراض أن الحمولة آمنة لـ HTML. إنها آمنة لـ JavaScript. قد تحتوي القوالب ترميزاً
موثوقاً، والعميل يقرر كيفية عرضه.

**مأزق:** وضع محتوى لكل مستخدم خلف ذاكرة مشتركة. دقة الحمولة على مستوى الثقافة؛ ولا توجد حمولات
لكل مستخدم. وإذا كانت رسالة تحتوي بيانات شخصية، فهي لا تنتمي إلى نقطة النهاية هذه.

**مأزق:** حذف <code dir="ltr">SupportedCultures</code> وافتراض أن نقطة النهاية مغلقة. مع قائمة
فارغة، تُخدم كل ثقافة معروفة.

**مأزق:** التعامل مع ETag كذاكرة مؤقتة مجانية لعمل المزوّد. إنه يوفر النطاق الترددي، لا عمليات بحث
المزوّد التي تبني الحمولة.

**مأزق:** ربط نقطة النهاية قبل تسجيل
<code dir="ltr">AddJavaScriptLocalization</code>. ترمي
<code dir="ltr">MapJavaScriptLocalization</code> برسالة قابلة للتنفيذ عندما تكون الخيارات غائبة.

## إلى أين بعد ذلك

- [ASP.NET Core وMVC](08-aspnet-core-and-mvc.md) لاختيار ثقافة الطلب على الخادم.
- [Blazor](09-blazor.md) لإدارة الثقافة داخل مضيفات Blazor.
- [الأمان](../04-operations/02-security.md) لثقة المحتوى وحدود التخزين المؤقت.

</div>
