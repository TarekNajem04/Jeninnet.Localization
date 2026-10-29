<div dir="rtl">

# مسرد المصطلحات (glossary)

مفردات الكتاب، مرتبة حسب المصطلح الإنجليزي. التعريفات ضيقة عمداً: المصطلح يعني الشيء نفسه في كل
هذا المستودع، حتى حيث يكون الاستخدام العادي أوسع.

## الاختصارات والمصطلحات المختصرة

| الاختصار | الكامل | ملاحظة |
|---|---|---|
| AD | Architecture Decision | |
| API | Application Programming Interface | واجهة برمجة التطبيقات. |
| AOT | Ahead-of-Time compilation | ترجمة مسبقة إلى كود الآلة قبل التشغيل. |
| BCL | Base Class Library | مكتبة الفئات الأساسية في .NET. |
| BOM | Byte Order Mark | علامة اختيارية في بداية تدفق نصي Unicode. |
| CLDR | Common Locale Data Repository | مستودع بيانات اللغات الموحّد التابع لـ Unicode. |
| CLI | Command-Line Interface | واجهة سطر الأوامر. |
| CLR | Common Language Runtime | بيئة تنفيذ .NET. |
| CRLF | Carriage Return + Line Feed | نهاية السطر في Windows: `\r\n`. |
| CRUD | Create, Read, Update, Delete | إنشاء وقراءة وتحديث وحذف. |
| CSV | Comma-Separated Values | قيم مفصولة بفواصل. |
| DI | Dependency Injection | حقن التبعيات. |
| DOC | Documentation | |
| DOM | Document Object Model | نموذج كائن المستند. |
| DTO | Data Transfer Object | كائن نقل البيانات. |
| EF Core | Entity Framework Core | أداة ORM من Microsoft لـ .NET. |
| EOT | End of Transmission | المحرف `\u0004` الذي يفصل السياق عن المفتاح في gettext. |
| GUID | Globally Unique Identifier | معرّف فريد عالمياً. |
| HTML | HyperText Markup Language | لغة توصيف النص التشعبي. |
| i18n | internationalization | اختصار رقمي: `i` + 18 حرفاً + `n`. |
| ICU | International Components for Unicode | مكوّنات Unicode الدولية لتنسيق الرسائل. |
| IDE | Integrated Development Environment | بيئة التطوير المتكاملة. |
| IL | Intermediate Language | لغة .NET الوسيطة (CIL/MSIL). |
| ISO | International Organization for Standardization | المنظمة الدولية للمعايير. |
| JIT | Just-in-Time compilation | ترجمة عند التشغيل. |
| JLN | Jeninnet Localization | بادئة معرّفات التشخيص (`JLNLOC`, `JLNKEY`). |
| JSON | JavaScript Object Notation | صيغة تبادل بيانات. |
| l10n | localization | اختصار رقمي: `l` + 10 أحرف + `n`. |
| LTR | Left-to-Right | من اليسار إلى اليمين. |
| MO | Machine Object | صيغة GNU gettext الثنائية المترجمة (`.mo`). |
| MSTest | Microsoft Test Framework | إطار الاختبار المستخدم في المستودع. |
| MVC | Model-View-Controller | نمط الفصل بين النموذج والعرض والتحكم. |
| ORM | Object-Relational Mapping | ربط الكائنات بقواعد البيانات العلائقية. |
| OTel | OpenTelemetry | إطار الرصد والقياس. |
| OTLP | OpenTelemetry Protocol | بروتوكول OpenTelemetry. |
| PO | Portable Object | صيغة GNU gettext النصية (`.po`). |
| R8 | Requirement 8 | متطلب الإلغاء والعقود غير المتزامنة. |
| RFC | Request for Comments | وثيقة معايير تقنية. |
| ResX | `.resx` resource format | ليس اختصاراً: اسم صيغة ملفات الموارد XML. |
| RTL | Right-to-Left | من اليمين إلى اليسار. |
| SHA-256 | Secure Hash Algorithm, 256-bit | خوارزمية تجزئة آمنة. |
| SQL | Structured Query Language | لغة الاستعلامات البنيوية. |
| TFM | Target Framework Moniker | مثل `net10.0` أو `netstandard2.0`. |
| TLS | Transport Layer Security | أمان طبقة النقل. |
| TM | Translation Memory | ذاكرة الترجمة. |
| TTL | Time To Live | مدة الصلاحية. |
| URI | Uniform Resource Identifier | معرّف مورد موحّد. |
| URL | Uniform Resource Locator | محدّد موقع مورد. |
| UTC | Coordinated Universal Time | التوقيت العالمي المنسق. |
| UTF-8 | Unicode Transformation Format, 8-bit | ترميز Unicode ثماني البتات. |
| WASM | WebAssembly | صيغة تنفيذ في المتصفح. |
| XAML | Extensible Application Markup Language | لغة توصيف تطبيقات. |
| XLIFF | XML Localization Interchange File Format | صيغة تبادل ملفات الترجمة. |
| XML | Extensible Markup Language | لغة التوصيف القابلة للتوسعة. |

**الفاعل (Actor).** التسمية التي يوفّرها المستدعي وتُسجَّل على تعديل محكوم وعلى إدخال تدقيقه،
وعادةً تكون الأصل المصادَق. وهو دليل فقط إذا وفّره المضيف من هوية موثوقة، لا من جسم طلب. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**مخزن الإدارة (Admin store).** <code dir="ltr">IResourceAdminStore</code>، سطح الكتابة المحكوم
فوق مخزن مجموعات الموارد. تنفيذ الواجهة هو القدرة على مستوى النوع؛ و
<code dir="ltr">SupportsAdministration</code> هو الجواب على مستوى النسخة. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**حدث التدقيق (Audit event).** الحدث الحي <code dir="ltr">Audited</code> الذي يُرفع مرة واحدة لكل
صف معدَّل بعد تثبيت تعديل محكوم. إنه داخل العملية وغير دائم. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**سجل التدقيق (Audit record).** سجل محفوظ بسلسلة تجزئة لصف معدَّل واحد، يحمل بيانات وصفية وتجزئتي
SHA-256 قبل وبعد، لا نص القالب. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**الكتالوج (Catalog).** مجموعة مفاتيح رسائل وقوالب لثقافة واحدة، مخزّنة في ملف أو جدول قاعدة بيانات
أو مورد مترجم أو خدمة ترجمة. والكتالوج ليس مجموعة موارد: فخط أنابيب الرسائل يقرأ الكتالوجات، بينما
يدير المخزن مجموعات الموارد. راجع
[اختيار مزوّد](../02-guides/01-choosing-a-provider.md).

**إصدار الكتالوج (Catalog version).** الإصدار الرتيب المعيَّن لدفعة تعديل محكوم واحدة. ويتشاركه كل
سجل تدقيق في الدفعة، وتستخدمه القراءات عند إصدار كحد. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**تجزئة السلسلة (Chain hash).** تجزئة سجل تدقيق مدمجة مع تجزئة سلسلة السجل السابق. وأي تغيير في
أي سجل يكسر السلسلة ما لم تُعَد كتابة كل تجزئة لاحقة. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**فئة جمع CLDR (Common Locale Data Repository plural category).** إحدى الفئات الأساسية الخاصة باللغة التي يستخدمها محرك
الجمع: <code dir="ltr">Zero</code> أو <code dir="ltr">One</code> أو
<code dir="ltr">Two</code> أو <code dir="ltr">Few</code> أو <code dir="ltr">Many</code> أو
<code dir="ltr">Other</code>. و<code dir="ltr">Other</code> يعني "لا شيء من الفئات المدرجة"، لا
"الجمع" فقط. راجع [التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

**السياق (Context).** نطاق تسمية يفرّق بين المفاتيح، يرمّزه
<code dir="ltr">LocalizeWithContextAsync</code> كـ
<code dir="ltr">context + '\u0004' + key</code> (اصطلاح EOT في gettext). راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

**ارتداد الثقافة (Culture fallback).** الخوارزمية الوحيدة التي يملكها
<code dir="ltr">DefaultMessageLocalizer</code>: الثقافة المطلوبة، ثم ثقافات الآباء، مع التوقف قبل
الثقافة الثابتة، ثم الثقافة الافتراضية الاختيارية مرة واحدة. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**مزوّد واعٍ بالثقافة (Culture-aware provider).**
<code dir="ltr">CultureAwareMessageProvider</code>، موزّع يحمل مزوّداً واحداً لكل ثقافة ويختار
بالاسم الدقيق للثقافة ثم باسم اللغة المكوّن من حرفين. وهو ليس محرك ارتداد؛ فالمُعرِّب ما زال ينفّذ
مشي الآباء. راجع [الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**الثقافة الافتراضية (Default culture).** ثقافة الارتداد الأخيرة المكوَّنة عبر
<code dir="ltr">LocalizationOptions</code>. وتُلتقط عند إنشاء المُعرِّب؛ وnull أو الفراغ يعطّل
الخطوة. راجع [الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**الانحراف (Drift).** أي عدم تطابق غير مقصود بين ما يشير إليه الكود وما يخدمه الكتالوج، أو بين
تمثيلين للكتالوج نفسه. وتبلّغ أداة التكافؤ عن انحراف القالب وفجوات الصيغ وانحراف العناصر النائبة.
راجع [المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md) و
[README الأداة](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**التشغيل التجريبي (Dry run).** معاينة محكومة تتحقق من تعديل وتبلّغ عما سيفعله دون تغيير البيانات
أو رفع سجلات تدقيق. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**مخزن EF Core (Entity Framework Core store).** مخزن مجموعات الموارد المنفَّذ بـ
<code dir="ltr">SqlDbLocalizationStore</code> فوق
<code dir="ltr">LocalizationDataContext</code>. إنه
<code dir="ltr">ILocalizationStore</code> و<code dir="ltr">IResourceHistoryStore</code>، لا
<code dir="ltr">IMessageProvider</code>. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**مزوّد المطابقة التامة (Exact-match provider).** مزوّد يجيب عن مفتاح واحد وثقافة واحدة ولا يمشي
في الآباء ولا يستشير مصدراً آخر. وكل مزوّد رسائل مدمج يتبع هذه القاعدة. راجع
[النموذج الذهني](../01-getting-started/04-mental-model.md).

**سلسلة الارتداد (Fallback chain).** التسلسل المرتب للثقافات التي يستعلم عنها المُعرِّب لمفتاح
واحد. ويرى المزوّد كل خطوة كطلب مستقل مطابق تماماً. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**انحراف الصيغة (Format drift).** مفتاح مخدوم لثقافة في صيغة كتالوج وغائب من كتالوج صيغة أخرى، أو
مخدوم بقوالب مختلفة عبر الصيغ. راجع
[README الأداة](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**gettext.** مجموعة أدوات الترجمة GNU وعائلة صيغها: كتالوجات PO المصدرية وكتالوجات MO المترجمة
وسياقات <code dir="ltr">msgctxt</code> وترويسات <code dir="ltr">Plural-Forms</code>.

**سلسلة التجزئة (Hash chain).** بناء أثر التدقيق الذي تعتمد فيه تجزئة سلسلة كل سجل على السجل
السابق. إنه دليل عبث، لا أصالة ضد مهاجم قادر على إعادة الكتابة بالكامل. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**التبادل (Interchange).** صيغة ملف تُستخدم لتصدير محتوى الكتالوج واستيراده للمترجمين: CSV أو PO
أو ResX أو XLIFF. وحزم التبادل أدوات، لا مزوّدات وقت تشغيل. راجع
[نظرة عامة على التبادل](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md).

**الثقافة الثابتة (Invariant culture).** الثقافة التي اسمها فارغ والتي لا يستعلم عنها مشي
الارتداد أبداً. وهي ليست لغة ولا ينبغي أن تكون كتالوجاً. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**المفتاح (Key).** السلسلة المعتمة التي يمرّرها المستدعي إلى المُعرِّب. ولا تُطبَّع ولا تُقتطع
ولا تُفسَّر كهرمية. راجع [النموذج الذهني](../01-getting-started/04-mental-model.md).

**زوج المفاتيح (Key pair).** شكل المفتاح المفرد/الجمع الذي تقبله
<code dir="ltr">LocalizePluralAsync</code>. ويختار <code dir="ltr">One</code> المفتاح المفرد؛
وكل فئة أخرى تختار مفتاح الجمع. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

**خط أنابيب الرسائل (Message pipeline).** مسار <code dir="ltr">IMessageLocalizer</code>: تحقق،
واختيار مزوّد واحد، ومشي الارتداد، وتنسيق عند الإصابة، وإعادة <code dir="ltr">null</code> عند
الغياب. راجع [النموذج الذهني](../01-getting-started/04-mental-model.md).

**مزوّد الرسائل (Message provider).** تنفيذ <code dir="ltr">IInnerMessageProvider</code>/
<code dir="ltr">IMessageProvider</code> يعيد قوالب لمفتاح وثقافة دقيقين. ويُحلّ مزوّد واحد بالضبط
لكل تركيب. راجع [اختيار مزوّد](../02-guides/01-choosing-a-provider.md).

**مصب المفاتيح المفقودة (Missing key sink).** <code dir="ltr">IMissingKeySink</code>، الخطاف
الاختياري الذي يتلقى تقريراً واحداً بعد أن تغيب سلسلة الارتداد كاملة. والافتراضي null ولا يضيف
عملاً لكل بحث. راجع [استكشاف الأخطاء](../04-operations/04-troubleshooting.md).

**MO (Machine Object).** صيغة GNU gettext الثنائية المترجمة. ويقرأها مزوّد PO عندما يختار
<code dir="ltr">Format</code> القارئ الثنائي أو يكون امتداد الملف
<code dir="ltr">.mo</code>. راجع
[README مزوّد PO](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md).

**الثقافة المحايدة (Neutral culture).** لغة بلا منطقة، مثل <code dir="ltr">de</code>. ويرتد الطلب
المحدد مثل <code dir="ltr">de-CH</code> إلى أبيه المحايد عبر المُعرِّب. راجع
[الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md).

**التكافؤ (Parity).** خاصية اتفاق مجموعة المفاتيح التي يشير إليها المصدر مع الكتالوجات والثقافات
المكوَّنة. وأداة التكافؤ تقيسها؛ ولا تكتب تغييرات. راجع
[README الأداة](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**العنصر النائب (Placeholder).** امتداد <code dir="ltr">{name}</code> (أو
<code dir="ltr">{name, type, ...}</code> في ICU) داخل قالب يستبدله المنسّق. ويستخدم المنسّق
الافتراضي النص حتى <code dir="ltr">}</code> التالي كمفتاح قاموس. راجع
[التنسيق وصيغ الجمع](../02-guides/04-formatting-and-plurals.md).

**انحراف العناصر النائبة (Placeholder drift).** مفتاح/ثقافة تختلف مجموعة معرّفات عناصره النائبة
عن مجموعة الثقافة المرجعية. وإعادة ترتيب الوسائط المسمّاة ليست انحرافاً؛ وإضافة المعرّفات أو
إسقاطها انحراف. راجع
[README الأداة](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**PO (Portable Object).** صيغة كتالوج gettext النصية: <code dir="ltr">msgid</code> و
<code dir="ltr">msgstr</code> و<code dir="ltr">msgctxt</code> الاختياري وصيغ الجمع وترويسة
<code dir="ltr">Plural-Forms</code>. راجع
[README مزوّد PO](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md).

**المزوّد (Provider).** اختصار مزوّد الرسائل. وفي هذا الكتاب، لا يعني "المزوّد" أبداً نظام إدارة
ترجمة أو مخزناً. راجع [اختيار مزوّد](../02-guides/01-choosing-a-provider.md).

**التعريب الزائف (Pseudo-localization).** ثقافة اصطناعية تنتج بتحويل قوالب المصدر لكشف فائض
التخطيط والسلاسل غير المترجمة. وهو مزوّد، لا ترجمة. راجع
[README مزوّد Pseudo](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md).

**التنقيح (Redaction).** قاعدة التدقيق التي تستبدل نص القالب بتجزئة SHA-256 له. وتنطبق على سجلات
التدقيق، لا على يوميات إعادة التشغيل. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**يوميات إعادة التشغيل (Replay journal).** جدول <code dir="ltr">StringVersions</code> الملحق فقط
الذي يحفظ القيم الفعلية وشواهد القبور كي تعيد القراءات عند إصدار إنتاج الكتالوج. إنها بيانات
كتالوج، لا بيانات تدقيق منقّحة. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**مجموعة الموارد (Resource set).** مجموعة سلاسل مسمّاة وقابلة للتغيير لثقافة دقيقة واحدة، مفاتيح
إدخالاتها هي الأسماء. وتُدار عبر <code dir="ltr">ILocalizationStore</code>، لا عبر خط أنابيب
الرسائل. راجع [مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**مخزن مجموعات الموارد (Resource-set store).**
<code dir="ltr">ILocalizationStore</code> وعقود مجموعات الموارد التابعة له. ولا يشارك في عمليات
بحث <code dir="ltr">IMessageLocalizer</code>. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**ResX (XML-based .resx resource format).** صيغة موارد <code dir="ltr">.resx</code>. ويقرأ مزوّد وقت التشغيل موارد مترجمة عبر
<code dir="ltr">ResourceManager</code>؛ وتقرأ حزمة التبادل نص ResX وتكتبه للمترجمين. راجع
[README مزوّد ResX](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md).

**تجميعة satellite (Satellite assembly).** حزمة مترجمة
<code dir="ltr">{culture}/{AssemblyName}.resources.dll</code> يستكشفها
<code dir="ltr">ResourceManager</code>. ويخدمها مزوّد satellite مع تعطيل استكشاف الآباء. راجع
[README مزوّد satellite](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md).

**التسلسل (Sequence).** ترتيب الإلحاق الذي يعيّنه المخزن لسجل تدقيق أو يوميات. إنه يتزايد بصرامة
ومستقل عن إصدار الكتالوج؛ وقد تنتج دفعة واحدة سجلات كثيرة بإصدار واحد. راجع
[التدقيق والتاريخ](../02-guides/07-audit-and-history.md).

**مولّد المصدر (Source generator).** مولّد Roslyn التزايدي الذي يصدر مفاتيح
<code dir="ltr">const string</code> من كتالوجات <code dir="ltr">*.loc.json</code>. يعمل وقت
البناء وليس تبعية وقت تشغيل. راجع
[المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md).

**مزخرف المخزن (Store decorator).** غلاف فوق <code dir="ltr">ILocalizationStore</code> يضيف
سلوكاً، مثل <code dir="ltr">CachedLocalizationStoreDecorator</code> (الذاكرة) أو
<code dir="ltr">DistributedLocalizationStoreDecorator</code> (الذاكرة الموزعة). وتُربط المزخرفات
صراحةً، لا بامتداد <code dir="ltr">Add...</code>. راجع
[مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

**المفتاح المكتوب بقوة (Strongly typed key).** ثابت وقت ترجمة مولَّد من مفتاح كتالوج، اصطلاحاً
<code dir="ltr">LocalizationKeys.CART_ITEMS</code>. وهو سلسلة عادية وقت التشغيل. راجع
[المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md).

**ذاكرة الترجمة (Translation memory).** مخزن مطابقة تماماً للترجمات المعتمدة يستشيره مزوّد Azure
قبل الترجمة الآلية. وليست ذاكرة TTL. راجع
[README مزوّد Azure](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md).

**مزوّد الترجمة (Translation provider).** مزوّد رسائل قالب مصدره هو مفتاح البحث وقيمته تُجلب من
خدمة ترجمة بعيدة. وهو المزوّد الوحيد، لا طبقة ارتداد. راجع
[اختيار مزوّد](../02-guides/01-choosing-a-provider.md).

**حارس WASM (WebAssembly guard).** الفحص في
<code dir="ltr">AddBlazorWasmLocalization</code> الذي يرفض المزوّدات التي لا تنفّذ
<code dir="ltr">IWasmCompatibleMessageProvider</code>، ويفرض أن المزوّد المسجّل لا يعلن إدخال/إخراج
ملفات أو قواعد بيانات أو شبكة في عملية متصفح. راجع
[README تكامل Blazor](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md).

</div>
