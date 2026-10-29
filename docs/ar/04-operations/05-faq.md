<div dir="rtl">

# الأسئلة الشائعة (frequently asked questions)

هذه أسئلة تتكرر في مراجعات التصميم والتأهيل وحوادث الإنتاج. كل إجابة قصيرة، لكن الروابط تشير إلى
الفصل الذي يطوّر الموضوع كما ينبغي.

## لماذا يسمح الإطار بمزوّد واحد فقط؟

لأن تسلسل المزوّدين محرك ارتداد ثانٍ، وهذه المكتبة تملك محركاً واحداً عمداً. يحل
<code dir="ltr">LocalizationProviderSelector</code> نسخ
<code dir="ltr">IInnerMessageProvider</code> المسجّلة ويرمي
<code dir="ltr">InvalidOperationException</code> إذا وُجد أكثر من واحد. المُعرِّب يملك ارتداد
الثقافة؛ والمزوّدون مصادر مطابقة تماماً. ومزوّدان سيكرران تلك الملكية ويجعلان ترتيب البحث ملتبساً.
وإذا كان على مصدرين الإسهام في المحتوى، فادمجهما قبل وقت التشغيل أو اجعل أحدهما مرجعياً. راجع
[النموذج الذهني](../01-getting-started/04-mental-model.md).

## هل أستطيع دمج ملف JSON مع قاعدة بيانات؟

ليس بتسجيل الاثنين. صدّر أحد المصدرين إلى الآخر، أو ولّد كتالوجاً مدموجاً وقت البناء، أو اجعل
أحدهما نظام السجل. ويجوز لمضيفات مختلفة استخدام مزوّدين مختلفين؛ فقاعدة المزوّد الواحد بالضبط
تنطبق لكل تركيب، لا لكل مستودع. وإذا احتجت محتوى قابلاً للتعديل وقت التشغيل ولقطة ملف، فاختر
واحداً لخط أنابيب الرسائل واستخدم المخزن لمجموعات الموارد المُدارة منفصلة. راجع
[اختيار مزوّد](../02-guides/01-choosing-a-provider.md).

## ما الحد الأدنى من الحزم؟

لـ <code dir="ltr">IMessageLocalizer</code>: حزمة
<code dir="ltr">Jeninnet.Localization.Core</code> (التي تجلب
<code dir="ltr">Jeninnet.Localization.Abstractions</code> تبعياً) مع حزمة مزوّد واحدة بالضبط.
ولجسر <code dir="ltr">IStringLocalizer</code>، أضف <code dir="ltr">ILocalizationStore</code>؛
وحزمة مخزن EF Core أحد الخيارات. ولتحقق ASP.NET Core والعروض، أضف
<code dir="ltr">Jeninnet.Localization.Integrations.Mvc.Validation</code>. أما "الثلاثي الأساسي"
الاسمي فهو <code dir="ltr">Abstractions</code> و<code dir="ltr">Core</code> و
<code dir="ltr">Storage</code>؛ وكود التطبيق يحصل عادةً على <code dir="ltr">Storage</code>
تبعياً ويشير إلى <code dir="ltr">Core</code> مع مزوّد فقط. راجع
[خريطة الحزم](../../en/appendix/02-package-map.md).

## هل أحتاج مخزن مجموعات الموارد لاستخدام IMessageLocalizer؟

لا. خط أنابيب الرسائل والمخزن نطاقان منفصلان. يقرأ
<code dir="ltr">IMessageLocalizer</code> مزوّد رسائل ولا يقرأ
<code dir="ltr">ILocalizationStore</code> أبداً. والمخزن لمجموعات موارد مسمّاة وقابلة للتغيير،
ولـ <code dir="ltr">IStringLocalizer</code>، وللإدارة والتدقيق والتاريخ. وتسجيله لا يمنح خط
أنابيب الرسائل مصدراً، وتسجيل مزوّد لا يعبّئ المخزن. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

## كيف أُرحّل من RESX؟

غالباً يُخلط بين ترحيلين مختلفين. إذا كانت ملفات <code dir="ltr">.resx</code> مضمّنة في تجميعة
وتريد أن يحلّها <code dir="ltr">ResourceManager</code>، فاستخدم
<code dir="ltr">Jeninnet.Localization.Providers.ResX</code> مع اسم مورد البيان الأساسي
والتجميعة المالكة. وإذا كانت ملفات <code dir="ltr">.resx</code> تبادلاً للمترجمين، فاستخدم حزمة
تبادل ResX لاستيرادها إلى كتالوجات JSON/XML. فمزوّد ResX وقت التشغيل لا يقرأ ملفات
<code dir="ltr">.resx</code> السائبة، وحزمة التبادل لا تصبح مزوّد وقت تشغيل. راجع
[README مزوّد ResX](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) و
[README التبادل](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md).

## كيف أتعامل مع صيغ الجمع؟

ثلاثة خيارات، بترتيب متزايد من الدقة:

1. **زوج المفاتيح** — تختار
   <code dir="ltr">LocalizePluralAsync(singularKey, pluralKey, count, ...)</code> المفرد لـ
   <code dir="ltr">PluralCategory.One</code> ومفتاح الجمع لكل فئة أخرى. إنه بسيط ويعمل للغات ذات
   صيغتين.
2. **جداول المزوّد لكل صيغة** — ينفّذ مزوّد PO (Portable Object) الواجهة
   <code dir="ltr">IPluralFormMessageProvider</code> عندما يحمل الكتالوج ترويسة
   <code dir="ltr">Plural-Forms</code>، فيستطيع اختيار صيغة <code dir="ltr">msgstr[n]</code>
   الدقيقة.
3. **جمع ICU (International Components for Unicode)** — يختار <code dir="ltr">IcuMessageFormatter</code> بفروع
   <code dir="ltr">{count, plural, ...}</code> فئات CLDR (Common Locale Data Repository) داخل القالب، ويدعم
   <code dir="ltr">=0</code> و<code dir="ltr">offset</code> و<code dir="ltr">#</code>.

استخدم ICU أو جداول كل صيغة للعربية والسلافية وغيرهما من اللغات التي تملك أكثر من صيغتين
معروضتين. وواجهة زوج المفاتيح تدهور موثّق هناك. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

## متى أستخدم ICU MessageFormat؟

استخدمه عندما يحتاج قالب إلى فئات جمع، أو <code dir="ltr">select</code>، أو
<code dir="ltr">selectordinal</code>، أو عرض أرقام وتواريخ مراعٍ للثقافة. إنه اختياري: سجّل
<code dir="ltr">IcuMessageFormatter</code> كـ <code dir="ltr">IMessageFormatter</code> قبل
<code dir="ltr">AddLocalization</code>، أو استبدل الواصف. والقوالب الخالية من تركيب ICU تأخذ مسار
المنسّق الافتراضي، لذا فتفعيله لا يغيّر المحتوى القائم. وهو أصرم من المنسّق الافتراضي ويرمي
<code dir="ltr">FormatException</code> للتراكيب المشوّهة. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

## كيف أفرّق بين "Open" كزر و"Open" كحالة طلب؟

استخدم <code dir="ltr">LocalizeWithContextAsync(key, context, parameters, culture)</code>.
يبني المُعرِّب مفتاح المزوّد كـ <code dir="ltr">context + '\u0004' + key</code>، وهو اصطلاح EOT (End of Transmission)
في gettext، فتتعامل إدخالات <code dir="ltr">msgctxt</code> في PO دون مسار بحث ثانٍ. والسياق جزء
من الهوية: تغييره لاحقاً ترحيل كتالوج. والسياق الفارغ يتصرف كمفتاح عادي. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

## كيف أبوّب التعريب في CI؟

استخدم طبقات عدة، لا طبقة واحدة:

- يحوّل مولّد المفاتيح الكتالوجات المشوّهة وتصادمات المعرّفات إلى أخطاء بناء؛
- يقترح محلل قابلية التعريب نقل السلاسل المضمّنة الموجهة للمستخدم؛
- تبلّغ <code dir="ltr">jeninnet-localization-keys</code> عن المفاتيح المفقودة وغير المستخدمة
  وانحراف القوالب وفجوات الصيغ وانحراف العناصر النائبة، إضافة إلى مصفوفة
  مخدوم/ارتداد/مفقود، برمز خروج غير صفري؛
- تؤكد اختبارات الوحدة تسلسلات الارتداد وسلوك المنسّق؛
- يتحقق مستهلك اختبار الدخان AOT (Ahead-of-Time) من مسار النشر عندما يكون التقليص في النطاق.

رمز الخروج <code dir="ltr">1</code> من أداة التكافؤ نتيجة؛ و<code dir="ltr">2</code> خطأ استخدام
أو بيئة. راجع [المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md) و
[README الأداة](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

## كيف أترجم على نطاق واسع؟

صدّر كتالوج ثقافة واحدة إلى صيغة تبادل (CSV للجداول، وXLIFF للمورّدين، وPO لأدوات gettext،
وResX لمحرري ResX)، وأرسله، ثم استورده مع التحقق وتشغيل تجريبي. أبقِ ثقافة المصدر مرجع المفاتيح:
فالتبادل لا يختلق مفاتيح. وللترجمة الآلية، فضّل دفعة دون اتصال إلى كتالوج وقت التشغيل؛ واستخدم
مزوّد ترجمة Azure وقت الطلب فقط عندما تكون الترجمة الديناميكية قرار منتج صريحاً. وتوجد ميزات
التجميع والذاكرة والمسرد وذاكرة الترجمة لديه لتلك الحالة. راجع
[README مزوّد Azure](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md)
للسطح المدعوم.

## كيف أختبر التعريب؟

استخدم مزوّد تسجيل يسجّل كل استعلام <code dir="ltr">(key, culture)</code> ويعيد قوالب محكومة.
أكّد تسلسل الثقافات الدقيق، لا السلسلة المعادة وحدها. أضف اختبارات لاستبعاد الثقافة الثابتة،
ووصول الثقافة الافتراضية، والغياب الكامل الذي يعيد <code dir="ltr">null</code>، والإلغاء، وسلوك
المنسّق. ولجسر المخزن، استخدم مخزناً اختبارياً وأكّد
<code dir="ltr">[MISSING:key]</code> و<code dir="ltr">ResourceNotFound</code>. والتعريب الزائف
لضمان الجودة البصري، لا بديلاً عن فحص التكافؤ. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

## كيف أجد الترجمات المفقودة في الإنتاج؟

فعّل مصب المفاتيح المفقودة. يكتب <code dir="ltr">LoggerMissingKeySink</code> تحذيراً منظماً واحداً
لكل غياب بعد فشل سلسلة الارتداد كاملة، مع المفتاح والثقافة وموضع النداء. راقب
<code dir="ltr">localization.lookup.misses</code> و
<code dir="ltr">localization.fallback.steps</code>؛ فخطوة الارتداد ليست فشلاً بالضرورة، لكن معدل
الغياب الصاعد فشل. لا تستنتج التغطية من نجاح عرض الصفحات: فالارتداد قد يخفي فجوة. استخدم أداة
التكافؤ والتعريب الزائف للتغطية. راجع [استكشاف الأخطاء](04-troubleshooting.md).

## كيف يعمل ارتداد الثقافة فعلاً؟

الثقافة المطلوبة، ثم كل أب، مع التوقف قبل الثقافة الثابتة، ثم الثقافة الافتراضية المكوَّنة مرة
واحدة. والمزوّد مطابق تماماً في كل خطوة. وإذا لم يُحل شيء، فالنتيجة
<code dir="ltr">null</code>. وتُلتقط الثقافة الافتراضية عند إنشاء المُعرِّب. والثقافة الافتراضية
الفارغة تعطّل الخطوة الأخيرة. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

## لماذا تعرض صفحتي العربية الإنجليزية؟

إما أن الكتالوج العربي لا يحتوي المفتاح فوصل الارتداد إلى الثقافة الافتراضية، أو أن ثقافة الطلب
لم تصبح العربية قط. افحص تكوين ثقافة الطلب أولاً: فبلا جسر تعريب الطلب، لا تجعل ملفات مزوّد
عربية اللغة <code dir="ltr">ar</code> ثقافة مخدومة. وإذا كانت الثقافة صحيحة، فافحص الكتالوج عن
المفتاح الدقيق وعما إذا كانت قيمة فارغة تكبت الارتداد. والارتداد الناجح سلوك متوقع، لا خطأ. راجع
[استكشاف الأخطاء](04-troubleshooting.md).

## كيف أتيح للمسؤولين تحرير السلاسل وقت التشغيل؟

استخدم مخزن مجموعات موارد EF Core ومسار <code dir="ltr">IResourceAdminStore</code> المحكوم. لا
تستخدم CRUD (Create, Read, Update, Delete) الخام لـ <code dir="ltr">ILocalizationResourceSet</code> في التعديلات الإدارية: إنه
يتجاوز التحقق والإبلاغ عن التعارضات والتدقيق والتأصيل. وتذكّر أن المخزن لا يغذّي
<code dir="ltr">IMessageLocalizer</code>؛ وإذا كان على خط أنابيب وقت التشغيل رؤية التعديل، فأنت
تحتاج استراتيجية مزامنة أو معمارية مختلفة. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

## كيف أدقّق من غيّر قالباً؟

استخدم مسار الإدارة المحكوم و<code dir="ltr">IResourceHistoryStore</code>. كل تعديل مطبَّق ينتج
سجل تدقيق بالفاعل والوقت والعملية ومجموعة المفاتيح وتجزئتي SHA-256 (Secure Hash Algorithm, 256-bit) قبل وبعد. والإصدارات رتيبة لكل
دفعة تعديل، وتعيد <code dir="ltr">GetStringsAsOfVersionAsync</code> تشغيل الكتالوج عند إصدار.
وسلسلة التجزئة دليل عبث، لكنها غير مرساة؛ فالمهاجم القادر على إعادة الكتابة بالكامل يستطيع إعادة
حسابها. راجع [التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

## هل يعمل مع التقليص وNative AOT؟

نعم لخط أنابيب وقت التشغيل ولكل مزوّد رسائل. يبني المستودع مشاريع وقت التشغيل
<code dir="ltr">net8.0</code> و<code dir="ltr">net9.0</code> و<code dir="ltr">net10.0</code> بـ <code dir="ltr">IsAotCompatible=true</code>، وينشر مستهلك
اختبار الدخان Native AOT ويشغّله. ومخزن EF Core حزمة وقت التشغيل الوحيدة المستبعدة لأن EF Core
نفسه ليس متوافقاً تماماً مع AOT. وبعض أسطح الراحة القائمة على الانعكاس موسومة
<code dir="ltr">RequiresUnreferencedCode</code>. راجع
[AOT والتقليص](../aot-and-trimming.md).

## ما حالة الإصدار وموقف التأصيل؟

المستودع غير منشور وتحت تطوير نشط؛ ولم تُنشر أي حزم NuGet. ويعلن البناء الإصدار
<code dir="ltr">1.0.0.0</code>، وينتج <code dir="ltr">dotnet pack</code> محلي حزماً بإصدار
<code dir="ltr">1.0.0</code> في <code dir="ltr">artifacts/</code>. وهوية الحزمة هي اسم المشروع
يساوي اسم التجميعة يساوي معرّف الحزمة. لا تخلط حزمة محزومة محلياً مع إصدار موجز مستقبلي مفترض في
حل واحد. راجع [README المستودع](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md).

## أي ترخيص ينطبق؟

MIT. يشحن المستودع ملف الترخيص وتعلن الحزم
<code dir="ltr">PackageLicenseExpression</code> كـ MIT. راجع
[LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

## هل أستطيع استخدام هذا في Blazor WebAssembly وتطبيقات سطح المكتب؟

تستطيع تطبيقات سطح المكتب استخدام حزم الخادم العادية، بما فيها مزوّدا الملفات وSQLite. أما
WebAssembly في المتصفح فمقيّد: تتطلب
<code dir="ltr">AddBlazorWasmLocalization</code> مزوّدين ينفّذون
<code dir="ltr">IWasmCompatibleMessageProvider</code>؛ ومزوّد الذاكرة هو التنفيذ المدمج، ويُرفض
مزوّدا الملفات وقواعد البيانات. وBlazor Hybrid عملية سطح مكتب وقد يستخدم إدخال/إخراج ملفات أو
قاعدة بيانات. راجع
[README تكامل Blazor](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md).

## كيف أعرض رسائل HTML أو Markdown؟

يرمّز <code dir="ltr">HtmlSafeMessageFormatter</code> قيم الوسائط مع إبقاء القوالب موثوقة؛
ويعرض <code dir="ltr">MarkdownMessageFormatter</code> مجموعة فرعية من CommonMark ثم ينسّق
النتيجة. وكلاهما ينتج HTML يجب إخراجه خاماً، لا ترميزه كـ HTML مرة أخرى. ولا يعقّم أي منهما
قالباً غير موثوق: فالقوالب محتوى، ومراجعة المحتوى جزء من سير عمل الترجمة. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

## كيف أتعامل مع لغات اليمين إلى اليسار؟

تعيد المكتبة سلاسل؛ ولا تعيّن اتجاه النص أو التخطيط. ويعيّن المضيف
<code dir="ltr">dir</code> وCSS المناسب للثقافة. ويمكن خدمة الكتالوجات العربية كأي ثقافة أخرى،
وقواعد الارتداد محايدة للغة. ترجم جملاً كاملة بدلاً من دمج الشظايا، لأن ترتيب الكلمات والقواعد
تختلف. راجع [RTL (Right-to-Left) والنص ثنائي الاتجاه](../02-guides/16-rtl-and-bidi.md) لأمثلة المضيف وCSS
المنطقي وعزل الاتجاه وقائمة فحص.

## كيف أنشر تغييرات الكتالوج؟

لمزوّدات الملفات، انشر ملفات الكتالوج مع التطبيق وأبقِ مسار المحتوى نسبياً إلى
<code dir="ltr">AppContext.BaseDirectory</code>. ولمزوّدات قواعد البيانات، طبّق المخطط وابذر أو
حدّث الصفوف قبل إصدار التطبيق أو معه. وللمخزن، استخدم
<code dir="ltr">ResourceSynchronizer</code> بتشغيل تجريبي واحتفظ بالتقرير كدليل إصدار. راجع
[النشر](01-deployment.md).

## من أين أبدأ إذا تعطّل شيء؟

صنّف العرض أولاً: غياب، أو ثقافة خاطئة، أو استثناء بدء تشغيل، أو استثناء مزوّد. ثم استخدم جدول
الأعراض في [استكشاف الأخطاء](04-troubleshooting.md). وأكثر الأسباب الجذرية شيوعاً مزوّد ثانٍ، أو
ثقافة لم تُخدم قط، أو كتالوج لم يُنشر، أو بحث مخزن خُلق للمسار خط أنابيب الرسائل.

</div>
