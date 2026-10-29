<div dir="rtl">

# صيغ التبادل (interchange formats)

تنقل حزم التبادل محتوى الكتالوج من أدوات المترجمين وإليها. إنها أدوات وقت بناء ووقت مراجعة. ولا
واحدة منها مزوّد وقت تشغيل، ولا واحدة منها تقرأ كتالوجاً وقت البحث.

تشترك كل الصيغ في مفردات تعارض واحدة وفلسفة تطبيق واحدة:

- يتحقق المستند كله قبل حساب أي شيء.
- يعيد الاستيراد على مستوى القاموس مجموعة تغييرات؛ ويعيد الاستيراد على مستوى الملف كتابة الكتالوج
  الهدف مرة واحدة على الأكثر.
- التشغيل التجريبي لا يغيّر شيئاً.
- رحلة الذهاب والعودة بلا تغيير متطابقة بايتياً لأن مرمّز الملف المشترك يحافظ على ترتيب المفاتيح
  ونمط الأسطر وBOM.
- تظهر ملاحظات المترجم في التقرير ولا تُكتب في كتالوجات JSON أو XML أبداً.

## مفردات الحالة المشتركة

يعيش <code dir="ltr">CatalogKeyStatus</code> في
<code dir="ltr">Jeninnet.Localization.Interchange.Abstractions</code>:

| الحالة | المعنى |
|---|---|
| <code dir="ltr">Unchanged</code> | الهدف مطابق بالفعل؛ لم يُطبَّق شيء. |
| <code dir="ltr">Updated</code> | الهدف مختلف وطُبِّق (أو كان سيُطبَّق في تشغيل تجريبي). |
| <code dir="ltr">Skipped</code> | هدف فارغ أو مفقود، أو إدخال ضبابي، أو <code dir="ltr">translate="no"</code>. ولا تُمحى القوالب الموجودة أبداً. |
| <code dir="ltr">UnknownKey</code> | المفتاح غير موجود في كتالوج المصدر. والتبادل لا يختلق مفاتيح أبداً. |
| <code dir="ltr">StaleSource</code> | قالب مصدر المستند يختلف عن المصدر الحالي. يُبلَّغ عنه ولا يُطبَّق أبداً. |
| <code dir="ltr">UnsupportedKey</code> | لا يمكن تمثيل المفتاح في الصيغة الهدف. يُبلَّغ عنه ولا يُطبَّق أبداً. |

مجموعة الحالات التي تستطيع صيغة إنتاجها أضيق من المفردات. لا تستطيع PO (Portable Object) وResX تمثيل
<code dir="ltr">StaleSource</code> لأن مستنداتهما لا تحمل لقطة قالب مصدر. وتستطيع CSV وXLIFF ذلك.

## السطح العام

كل صيغة صف ساكن بالشكل التخطيطي نفسه (<code dir="ltr">TDocument</code> و
<code dir="ltr">TReport</code> يمثلان الأنواع الخاصة بالصيغة):

<div dir="ltr">

```csharp
public static string ExportCatalog(...);
public static void ExportCatalogFiles(...);
public static TDocument ParseDocument(string text, string filePath);
public static TCatalogImportResult ImportDocument(...);
public static TReport ImportCatalogFiles(...);
```

</div>

خيارات الاستيراد راية <code dir="ltr">DryRun</code> واحدة. وتحمل النتيجة على مستوى القاموس تقرير
كل مفتاح وخرائط الهدف المحدَّثة؛ و<code dir="ltr">null</code> يعني "لا تغييرات في ذلك البعد".
ويحمل تقرير مستوى الملف نتائج كل مفتاح والملاحظات المحللة من المستند.

## CSV

يستخدم <code dir="ltr">CsvCatalogInterchange</code> معيار RFC 4180 بمخطط ثابت:

<div dir="ltr">

```text
key,culture,value,note
App.Title,en,Bookstore,Product name
App.Title,ar,مكتبة,Product name
```

</div>

- **الثقافات:** مصدر واحد وهدف واحد لكل مستند، يُمرَّران كوسائط API لأن CSV لا يملك بيانات وصفية
  للمستند. وصفوف أي ثقافة أخرى تفشل التحقق.
- **اللغة الكتابية (dialect):** UTF-8 (Unicode Transformation Format, 8-bit) مع BOM (Byte Order Mark) عند الكتابة (كشف الجداول UTF-8 بموثوقية)؛ وسجلات
  CRLF (Carriage Return and Line Feed) عند الكتابة؛ ويُتحمَّل BOM وCRLF وLF وCR المنفرد والأسطر الفارغة عند القراءة. اقتباس
  أدنى عند الكتابة؛ وتحليل RFC 4180 صارم عند القراءة.
- **صيغ الجمع:** خارج المجموعة المسطحة. وتسافر المفاتيح السياقية كنص خلية عادي، لكن المفتاح
  السياقي مقابل هدف XML يكون <code dir="ltr">UnsupportedKey</code> لأن أسماء XML لا تستطيع حمل
  فاصل EOT (End of Transmission).
- **الملاحظات:** تُكتب في كل صف للمفتاح، ويُتحقق من اتفاقها عندما تكون غير فارغة، وتظهر لكل مفتاح.
- **المصدر القديم:** قابل للتمثيل. ويُقارن صف المصدر بكتالوج المصدر الحالي.
- **حدود الفقد:** لا جداول جمع ولا بيانات وصفية للتنسيق، والسياق غير قابل للتمييز عن نص المفتاح.

<div dir="ltr">

```csharp
CsvCatalogInterchange.ExportCatalogFiles("en.json", "ar.json", "ar.csv", "en", "ar");
var report = CsvCatalogInterchange.ImportCatalogFiles(
    "ar.csv", "en.json", "ar.json", "en", "ar",
    new CsvImportOptions { DryRun = true });
```

</div>

## PO

يقرأ <code dir="ltr">PoCatalogInterchange</code> نص gettext PO القياسي ويكتبه لثقافة واحدة.

<div dir="ltr">

```po
msgctxt "Views.Catalog.Index"
msgid "Heading"
msgstr "الفهرس"
```

</div>

- **الإدخالات:** <code dir="ltr">msgid</code> هو المفتاح؛ و<code dir="ltr">msgstr</code> قالب
  الثقافة؛ ويحمل إدخال الترويسة (<code dir="ltr">msgid ""</code>) القيمة
  <code dir="ltr">Content-Type: text/plain; charset=UTF-8</code> و
  <code dir="ltr">Language:</code>.
- **السياقات:** تنضم إدخالات <code dir="ltr">msgctxt</code> بـ EOT
  (<code dir="ltr">'\u0004'</code>) عند الاستيراد، مطابقةً لمفتاح
  <code dir="ltr">LocalizeWithContextAsync</code> وقت التشغيل.
- **الجمع:** <code dir="ltr">msgid_plural</code> مع <code dir="ltr">msgstr[n]</code> المتتالية.
  وتُبلَّغ مجموعة الجمع مرة واحدة؛ وتُعاد الجداول المحدَّثة عندما تُقدَّم الجداول الحالية.
- **الملاحظات والرايات:** تتحول تعليقات <code dir="ltr">#.</code> المستخرجة إلى
  <code dir="ltr">TranslatorNote</code>؛ وتُحفظ <code dir="ltr">#, fuzzy</code>، وتكون الإدخالات
  الضبابية <code dir="ltr">Skipped</code>.
- **الترتيب والترميز:** ترتيب ترتيبي حتمي؛ سلاسل أحادية السطر بهروب بنمط C؛ UTF-8 بلا BOM عند
  الكتابة؛ ويُتحمَّل BOM عند القراءة.
- **سياسة Plural-Forms:** يقبل التصدير ترويسة <code dir="ltr">Plural-Forms</code> اختيارية
  يوفّرها المستدعي وتُكتب حرفياً. ولا يملك الإطار جدول تعبيرات gettext. وبدون ترويسة، ينطبق ارتداد
  الزوج في المزوّد (<code dir="ltr">one</code> إلى الصيغة 0، وإلا الصيغة 1 عند وجودها).
- **المصدر القديم:** غير قابل للتمثيل. فملف PO يحمل مفاتيح، لا قوالب مصدر.
- **حدود الفقد:** لا لقطة مصدر، ولا كشف موثوق لمصدر نُقل؛ ودلالات الجمع تعتمد على الترويسة
  المقدَّمة.

## ResX

يقرأ <code dir="ltr">ResXCatalogInterchange</code> سلاسل وتعليقات ResX 2.0 المرجعية ويكتبها.

- **الإدخالات:** عناصر <code dir="ltr">data</code> بـ <code dir="ltr">name</code> و
  <code dir="ltr">xml:space="preserve"</code>، وبقيمة <code dir="ltr">value</code> واحدة على
  الأكثر، وتعليق <code dir="ltr">comment</code> واحد على الأكثر. واسم الإدخال هو المفتاح.
- **الثقافة:** ثقافة واحدة لكل مستند. ولا يملك ResX بيانات وصفية للثقافة؛ ويوفّر اسم الملف أو
  المستدعي الثقافة. وتكوين الملفات المحايدة وسير عمل satellite مسؤولية المستدعي.
- **اللغة الكتابية:** ترويسة VS المرجعية (<code dir="ltr">resmimetype</code>
  <code dir="ltr">text/microsoft-resx</code>، والإصدار <code dir="ltr">2.0</code>)، ومسافة
  بادئة بمقدار مسافتين، وأسطر LF، وUTF-8 مع BOM عند الكتابة. وتتحمل القراءة BOM وتتجاهل عنصري
  الجذر <code dir="ltr">assembly</code> و<code dir="ltr">metadata</code>.
- **صيغ الجمع:** خارج المجموعة.
- **الملاحظات:** تعليقات الإدخالات. تُحلَّل وتظهر؛ ولا تُطبَّق على ملف كتالوج أبداً.
- **الإدخالات غير المدعومة:** الإدخالات ذات النوع، أو <code dir="ltr">mimetype</code>، أو بلا
  قيمة تكون <code dir="ltr">UnsupportedKey</code>.
- **المصدر القديم:** غير قابل للتمثيل.
- **حدود الفقد:** قابلية التمثيل في XML 1.0. وفاصل سياق EOT غير مسموح ويُرفض عند التصدير.

الكاتب والقارئ مكتوبان يدوياً لأن <code dir="ltr">ResXResourceWriter</code> و
<code dir="ltr">ResXResourceReader</code> الكلاسيكيين في BCL (Base Class Library) خاصان بسطح مكتب Windows فقط على
إصدارات .NET المدعومة. والشكل المصدَّر مثبَّت بالاختبارات وبمكدس XML في BCL.

## XLIFF 2.0

يستهدف <code dir="ltr">XliffCatalogInterchange</code> معيار XLIFF (XML Localization Interchange File Format) 2.0 بملف واحد، ولغة مصدر واحدة،
ولغة هدف واحدة، و<code dir="ltr">segment</code> واحد لكل وحدة.

- **الوحدات:** عناصر <code dir="ltr">&lt;unit id&gt;</code> فريدة، و<code dir="ltr">&lt;source&gt;</code>
  نصي عادي مطلوب، و<code dir="ltr">&lt;target&gt;</code> نصي عادي اختياري. وحالات المقطع
  <code dir="ltr">initial</code> و<code dir="ltr">translated</code> و
  <code dir="ltr">reviewed</code> و<code dir="ltr">final</code>.
- **السياق:** <code dir="ltr">&lt;note category="context"&gt;</code>. وفي معرّفات الوحدات يُرمَّز
  فاصل EOT كـ U+2404 (␄)، وهو مسموح في XML 1.0.
- **الجمع:** تسافر كل صيغة كوحدة خاصة بها (<code dir="ltr">{key}[{index}]</code>) مع ملاحظات
  <code dir="ltr">plural-singular</code> و<code dir="ltr">plural-other</code> و
  <code dir="ltr">plural-index</code> و<code dir="ltr">plural-count</code>.
- **ملاحظات المترجم:** <code dir="ltr">&lt;note category="translator"&gt;</code> على الوحدات
  المسطحة فقط؛ ولا تحمل وحدات الجمع ملاحظات في هذه المجموعة الفرعية. والملاحظات المتعارضة على
  وحدة واحدة تفشل التحقق.
- **راية الترجمة:** <code dir="ltr">translate="no"</code> تكون
  <code dir="ltr">Skipped</code> عند الاستيراد.
- **اللغة الكتابية:** تُتجاهل العناصر ذات مساحات أسماء المورّد مع أشجارها الفرعية. وأي شيء آخر
  خارج المجموعة الفرعية يُرفض. ولا يوجد فحص XSD كامل؛ والتحقق القائم على الكود هو فحص المطابقة.
- **المصدر القديم:** قابل للتمثيل. ويُقارن مصدر الوحدة بكتالوج المصدر الحالي.
- **حدود الفقد:** المفاتيح السياقية أو صيغ الجمع التي تتجاوز خانات الزوج مقابل هدف XML مسطح تكون
  <code dir="ltr">UnsupportedKey</code>. وملاحظات المترجم على وحدات الجمع غير ممثَّلة.

<div dir="ltr">

```csharp
XliffCatalogInterchange.ExportCatalogFiles(
    "en.json", "ar.json", "ar.xliff", "en", "ar", fileId: "bookstore");

var report = XliffCatalogInterchange.ImportCatalogFiles(
    "ar.xliff", "en.json", "ar.json",
    new XliffImportOptions { DryRun = true });
```

</div>

## التراكيب في لمحة

| التركيب | CSV | PO | ResX | XLIFF |
|---|---|---|---|---|
| مفتاح/قالب مسطح | نعم | نعم | نعم | نعم |
| السياق | كنص مفتاح | <code dir="ltr">msgctxt</code> | لا | ملاحظة سياق + معرّف مرمّز |
| صيغ الجمع | لا | نعم | لا | نعم، وحدة لكل صيغة |
| ملاحظات المترجم | عمود <code dir="ltr">note</code> | تعليقات <code dir="ltr">#.</code> | <code dir="ltr">comment</code> | ملاحظة مترجم |
| لقطة المصدر | نعم | لا | لا | نعم |
| حالة المقطع | لا | راية ضبابية | لا | نعم |
| لغتا المصدر والهدف في المستند | وسائط API | ترويسة <code dir="ltr">Language:</code> | لا | <code dir="ltr">srcLang</code> / <code dir="ltr">trgLang</code> |
| الترميز عند الكتابة | UTF-8 + BOM، CRLF | UTF-8، بلا BOM | UTF-8 + BOM، LF | UTF-8، بلا BOM |

## ضمانات الذهاب والعودة

الضمانات دقيقة، والحدود مهمة:

- **إعادة كتابة الكتالوج:** يُحافظ على ترتيب المفاتيح ونمط الأسطر وBOM. والاستيراد بلا تغيير لا
  يعيد كتابة الهدف. وعند وجود تغييرات، يُكتب الهدف مرة واحدة.
- **نص التبادل:** التصدير حتمي لمدخل ثابت. ترتب PO الإدخالات ترتيبياً، وCSV المفاتيح ترتيبياً،
  وResX الأسماء ترتيبياً، ويحافظ XLIFF على ترتيب المصدر.
- **الملاحظات:** لا تُطبَّق على ملفات الكتالوج أبداً. طبّقها عبر
  <code dir="ltr">IResourceAdminStore.SetNoteAsync</code>.
- **التشغيل التجريبي:** يحسب التقرير نفسه كالتشغيل المطبَّق لكنه لا يغيّر شيئاً.
- **تغييرات المصدر:** تكتشفها CSV وXLIFF كـ <code dir="ltr">StaleSource</code>. ولا تستطيع PO
  وResX ذلك.

## إعدادات اللغة الكتابية

لا يوجد فاصل أو محرف اقتباس أو سطر جديد أو ترميز قابل للتكوين في CSV؛ فاللغة الكتابية ثابتة.
وتقبل PO فقط ترويسة <code dir="ltr">Plural-Forms</code> التي يوفّرها المستدعي. ويقبل XLIFF قيمة
<code dir="ltr">fileId</code> عند التصدير ويفرض المجموعة الفرعية عند القراءة. ولا يقبل ResX أي
إعدادات لغة كتابية إطلاقاً.

هذه مقايضة مقصودة. اللغة الكتابية الثابتة أسهل اختباراً وأصعب إفساداً؛ وقد تحتاج أداة المترجم خطوة
تحويل، لكن الكتالوج الناتج متوقع.

## مثال عملي: رحلة ذهاب وعودة واحدة

صدّر <code dir="ltr">en.json</code> و<code dir="ltr">ar.json</code> إلى
<code dir="ltr">ar.po</code> بترويسة <code dir="ltr">Plural-Forms</code> مقدَّمة وملاحظات مترجم.
وبعد أن يحرّر المترجم الملف، استورد بـ <code dir="ltr">DryRun = true</code> وراجع إدخالات
<code dir="ltr">Updated</code> و<code dir="ltr">Skipped</code> والتعارض. ثم طبّق الأهداف المتغيرة،
وطبّق الملاحظات المحللة عبر <code dir="ltr">SetNoteAsync</code>، ثم شغّل فحص التكافؤ. ويظل مزوّد
وقت التشغيل يقرأ كتالوجات JSON.

## مآزق

**مأزق:** توقع كتابة ملاحظات المترجم في JSON أو XML. إنها بيانات تقرير.

**مأزق:** استخدام CSV للغة تحتاج صيغ جمع. فالمخطط المسطح لا يستطيع حملها.

**مأزق:** توقع <code dir="ltr">StaleSource</code> من PO أو ResX. فتلك الصيغ لا تحمل لقطة مصدر.

## إلى أين بعد ذلك

- [سير عمل المترجمين](../02-guides/11-translator-workflow.md) لعملية الذهاب والعودة.
- [مرجع الأدوات](05-tooling-reference.md) لفحص التكافؤ الذي يكمل الاستيراد.
- [مرجع المزوّدين](03-provider-reference.md) لسلوك PO وResX وsatellite وقت التشغيل.
- [واجهة Core API](01-core-api.md) لترميز مفتاح السياق المستخدم في PO وXLIFF.

</div>
