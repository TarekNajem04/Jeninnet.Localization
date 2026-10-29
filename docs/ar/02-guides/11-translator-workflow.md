<div dir="rtl">

# سير عمل المترجمين (translator workflow)

الكتالوج بنية بيانات. وسير عمل الترجمة سلسلة تسليمات بين أشخاص وأدوات. توجد حزم التبادل
(interchange packages) لجعل تلك التسليمات بلا فقد بما يكفي لتكون آمنة، وصريحة بما يكفي لتكون قابلة
للمراجعة.

لسير العمل خمس مراحل: استخراج مجموعة المفاتيح والتحقق منها، وتصدير صيغة ملائمة للمترجم، والترجمة
خارج المستودع، والاستيراد مع التحقق، ثم تشغيل فحص التكافؤ. ولا يقرأ مزوّد وقت التشغيل ملف التبادل
أبداً؛ ويبقى الكتالوج المرجعي JSON أو XML.

## المرحلة 1: الاستخراج والتحقق

قبل تصدير أي شيء، حدّد ما يشير إليه المصدر فعلاً وما تخدمه كل ثقافة. تفعل أداة
<code dir="ltr">jeninnet-localization-keys</code> الأمرين. تحلل صيغة C# وعروض Razor، وتحل ثوابت
<code dir="ltr">*Keys</code>، وتقارن النتيجة بكل ملف كتالوج تستطيع قراءته.

<div dir="ltr">

```bash
jeninnet-localization-keys \
  --catalog ./Localization \
  --source ./src \
  --default-culture en \
  --report parity.json
```

</div>

يحتوي التقرير مصفوفة اكتمال بثلاث حالات لكل ثقافة ومفتاح: <code dir="ltr">served</code> (موجود
محلياً)، و<code dir="ltr">fallback</code> (موجود في أب أو في الثقافة الافتراضية)، و
<code dir="ltr">missing</code> (غائب من السلسلة كلها).

<code dir="ltr">fallback</code> ليس فشلاً في مهمة ترجمة، لكنه تحذير. والثقافة التي تُحل دائماً عبر
الارتداد فقط ليست مترجمة، حتى لو بدا التطبيق مكتملاً. استخدم المصفوفة لاختيار المفاتيح والثقافات
التي تستحق الإرسال إلى مترجم.

الأداة للتقارير فقط. لا تنشئ إدخالات كتالوج، ولا تكتب مفاتيح في ملفات، ولا تطبّق ترجمات. هذا الحد
مقصود؛ فكتابة الكتالوج تنتمي إلى المخزن المحكوم أو إلى تغيير ملف صريح مُراجَع.

## المرحلة 2: التصدير

اختر الصيغة التي تلائم أدوات المترجم، لا صيغة وقت التشغيل. تقرأ حزم التبادل كتالوجات JSON وXML
وتكتب PO (Portable Object) أو CSV أو ResX أو XLIFF (XML Localization Interchange File Format). وكلها تتحقق من المستند كله قبل تطبيق أي شيء.

| الصيغة | الأنسب إلى | صيغ الجمع | السياق | ملاحظات المترجم |
|---|---|---|---|---|
| PO | Poedit وWeblate وCrowdin وخطوط gettext | نعم، <code dir="ltr">msgstr[n]</code> | <code dir="ltr">msgctxt</code> | تعليقات <code dir="ltr">#.</code> |
| XLIFF 2.0 | الموردين المحترفين وأدوات CAT | وحدة واحدة لكل صيغة | <code dir="ltr">&lt;note category="context"&gt;</code> | <code dir="ltr">&lt;note category="translator"&gt;</code> |
| CSV | مراجعة الجداول والتسليمات السريعة | لا، مسطحة فقط | نص المفتاح فقط | عمود <code dir="ltr">note</code> |
| ResX | Visual Studio وResX Manager | لا، مسطحة فقط | نص المفتاح فقط | عنصر <code dir="ltr">comment</code> |

تصدير PO لثقافة واحدة:

<div dir="ltr">

```csharp
PoCatalogInterchange.ExportCatalogFiles(
    catalogPath: "Localization/ar.json",
    poPath: "handoff/ar.po",
    culture: "ar",
    translatorNotes: notes,
    pluralForms: "nplurals=6; plural=n==0 ? 0 : n==1 ? 1 : n==2 ? 2 : n%100>=3 && n%100<=10 ? 3 : n%100>=11 ? 4 : 5;");
```

</div>

ترويسة <code dir="ltr">pluralForms</code> اختيارية وتُكتب حرفياً. ولا يملك الإطار جدول تعبيرات
gettext؛ فـ <code dir="ltr">PluralRules</code> تكشف تصنيف CLDR (Common Locale Data Repository) فقط. وإذا احتاجت أداة المترجم
ترويسة <code dir="ltr">Plural-Forms</code>، زوّدها المستدعي. وإذا حُذفت، ينطبق ارتداد الزوج
الموثّق في مزوّد PO: يختار <code dir="ltr">one</code> الصيغة 0، وكل فئة أخرى تختار الصيغة 1 عند
وجودها.

تصدير XLIFF لمورّد:

<div dir="ltr">

```csharp
XliffCatalogInterchange.ExportCatalogFiles(
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    xliffPath: "handoff/ar.xliff",
    srcLang: "en",
    trgLang: "ar",
    translatorNotes: notes);
```

</div>

CSV وResX صيغتان مسطحتان. وهما جيدتان للمراجعة وعمل المصطلحات، لكن اللغة التي تملك أكثر من صيغتي
جمع تحتاج PO أو XLIFF إذا كان على المترجم تحرير كل صيغة. وإلا تتدهور واجهة الزوج إلى المفرد مقابل
كل شيء آخر.

## المرحلة 3: الترجمة خارج المستودع

يعمل المترجم على الملف المصدَّر. وما يسافر مع النص لا يقل أهمية عن النص:

- **الملاحظات** إرشادات على مستوى المفتاح ولا تخص ثقافة. تكتب PO تعليقات
  <code dir="ltr">#.</code>، وXLIFF عنصر
  <code dir="ltr">&lt;note category="translator"&gt;</code>، وCSV عمود
  <code dir="ltr">note</code>، وResX عنصر <code dir="ltr">comment</code>.
- **السياقات** تفرّق بين المفاتيح المتطابقة. مفتاح وقت التشغيل هو
  <code dir="ltr">context + '\u0004' + key</code>. وتستخدم PO الخيار
  <code dir="ltr">msgctxt</code>؛ وتضع XLIFF السياق في ملاحظة وترمّز الفاصل كـ U+2404 في معرّف
  الوحدة؛ وتحمل CSV المفتاح المركب كنص خلية؛ ولا يستطيع ResX تمثيله لأن الفاصل غير مسموح في
  XML 1.0.
- **مجموعات الجمع** إدخال منطقي واحد بعدة صيغ. تكتب PO الخيار
  <code dir="ltr">msgid_plural</code> و<code dir="ltr">msgstr[n]</code> المتتالية؛ وتكتب XLIFF
  وحدة لكل صيغة مع ملاحظات <code dir="ltr">plural-singular</code> و
  <code dir="ltr">plural-other</code> و<code dir="ltr">plural-index</code> و
  <code dir="ltr">plural-count</code>.

يجب أن يحافظ المترجمون على كل عنصر نائب وكل تركيب ICU (International Components for Unicode) بدقة. فإسقاط
<code dir="ltr">{0}</code> أو تغيير اسم <code dir="ltr">{count}</code> عيب وقت تشغيل، وماسح
العناصر النائبة في أداة التكافؤ مصمم لاكتشافه. راجع
[التنسيق وصيغ الجمع](04-formatting-and-plurals.md).

**مأزق:** تحرير كتالوج المصدر أثناء ترجمة قيد التنفيذ. تقارن عملية الاستيراد بالمصدر الحالي حيث
تحمل الصيغة لقطة مصدر (CSV وXLIFF)، وتبلّغ عن
<code dir="ltr">StaleSource</code>. ولا تحمل PO وResX قالب مصدر، لذا لا يمكن اكتشاف تسليم قديم.
جمّد المصدر طوال رحلة الذهاب والعودة، أو اقبل أن التغييرات المتأخرة تحتاج تصديراً جديداً.

## المرحلة 4: الاستيراد مع التحقق

كل استيراد تبادل هو تحقق ثم تطبيق. يفشل المستند المشوّه قبل حساب أي شيء؛ ويُعاد كتابة الملف الهدف
مرة واحدة على الأكثر، ولا يُعاد أبداً في تشغيل تجريبي، ولا عندما لا يتغير شيء.

<div dir="ltr">

```csharp
var preview = PoCatalogInterchange.ImportCatalogFiles(
    poPath: "handoff/ar.po",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    options: new PoImportOptions { DryRun = true });

if (preview.ConflictCount > 0) {
    throw new InvalidOperationException($"Translation conflicts: {preview.ConflictCount}");
}

var applied = PoCatalogInterchange.ImportCatalogFiles(
    poPath: "handoff/ar.po",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json");
```

</div>

مفردات حالة كل مفتاح مشتركة بين الصيغ: <code dir="ltr">Unchanged</code> (الهدف مطابق)،
و<code dir="ltr">Updated</code> (طُبّق هدف متغير)، و<code dir="ltr">Skipped</code> (هدف فارغ أو
إدخال ضبابي أو <code dir="ltr">translate="no"</code>)، و<code dir="ltr">UnknownKey</code>
(المفتاح غائب من كتالوج المصدر)، و<code dir="ltr">StaleSource</code> (قالب المصدر مختلف؛ يُبلَّغ
عنه ولا يُطبَّق أبداً)، و<code dir="ltr">UnsupportedKey</code> (غير قابل للتمثيل في الصيغة الهدف).

يعدّ <code dir="ltr">ConflictCount</code> حالات التعارض، لا كل إدخال متخطى. اقرأ تقرير كل مفتاح
بدلاً من اعتبار الصفر دليلاً على تطبيق كل سطر.

تظهر ملاحظات المترجم في التقرير، ولا تُطبَّق على ملف الكتالوج أبداً. لا يملك كتالوجا JSON وXML
تخزيناً للملاحظات. طبّق الملاحظات عبر المخزن المحكوم:

<div dir="ltr">

```csharp
var admin = store.RequireAdmin();

foreach (var key in applied.Keys.Where(k => k.TranslatorNote is not null)) {
    await admin.SetNoteAsync("Bookstore", key.Key, key.TranslatorNote!, new ResourceAdminOptions {
        Actor = "translation-import"
    });
}
```

</div>

تلك الخطوة تعديل محكوم منفصل بتحققها وإبلاغها عن التعارضات وسجل تدقيقها. وليست جزءاً من استيراد
الملف، ولا ينبغي تخطيها بصمت لأن الملف بدا سليماً.

## المرحلة 5: التكافؤ قبل الدمج

شغّل أداة التكافؤ مرة أخرى بعد الاستيراد. الفحوص المهمة هي:

- كل مفتاح مشار إليه أصبح الآن <code dir="ltr">served</code> أو
  <code dir="ltr">fallback</code> عن قصد في كل ثقافة.
- لا يوجد مفتاح كتالوج غير مستخدم دون سبب موثّق.
- قوالب ثقافة ومفتاح واحد تتفق عبر الصيغ.
- مجموعات العناصر النائبة تتفق مع الثقافة المرجعية.
- لم يصبح أي ملف كتالوج غير قابل للقراءة.

تخرج الأداة بالقيمة <code dir="ltr">0</code> عند النظافة، و<code dir="ltr">1</code> عند وجود
مشكلات تكافؤ، و<code dir="ltr">2</code> عند خطأ استخدام أو بيئة. اربط الخروج
<code dir="ltr">1</code> في CI كفشل. راجع
[التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md).

## رحلة واقعية متعددة الثقافات

يشحن متجر كتب الإنجليزية كمصدر والعربية والألمانية والبولندية كأهداف. يستخدم الفريق PO للعربية
والبولندية لأن كلتيهما تحتاج أكثر من صيغتي جمع، وXLIFF لمورّد الألمانية لأن أداة CAT لديه تتوقعه،
وCSV لمراجعة داخلية.

1. تُجمَّد مجموعة مفاتيح المصدر عند الإصدار <code dir="ltr">2026.10</code>. وتبلّغ أداة التكافؤ
   عن 1,240 مفتاحاً مشاراً إليه ولا إدخالات إنجليزية مفقودة.
2. تُصدَّر ملفات PO العربية والبولندية بترويسات <code dir="ltr">Plural-Forms</code> صريحة
   يوفّرها مورّد الترجمة. ويُصدَّر XLIFF الألماني بمعرّف ملف المورّد. ويُصدَّر CSV لمراجعة
   المصطلحات لدى مالك المنتج.
3. يعيد المورّد الملفات. يحتوي الملف العربي ثلاثة إدخالات
   <code dir="ltr">#, fuzzy</code> من تغيير مصطلحات؛ ويعلّمها الاستيراد
   <code dir="ltr">Skipped</code>، ويشير إليها التقرير.
4. صُدِّر XLIFF الألماني قبل تعديل المصدر. ويبلّغ الاستيراد عن
   <code dir="ltr">StaleSource</code> لذلك المفتاح ويترك الهدف القديم مكانه. يعيد الفريق التصدير
   ويكرر الاستيراد.
5. يتضمن XLIFF الألماني مفتاحين سياقيين لا يستطيع كتالوج XML الهدف تمثيلهما. يُبلَّغ عنهما
   <code dir="ltr">UnsupportedKey</code>؛ ويخدم وقت التشغيل الألمانية من PO، فيغيّر الفريق صيغة
   التسليم لتلك المفاتيح بدلاً من إسقاطها.
6. تُطبَّق الملاحظات العائدة في الصيغ الثلاث عبر <code dir="ltr">SetNoteAsync</code> بالفاعل
   <code dir="ltr">release-2026.10</code>. ولم تمس استيرادات الملفات الملاحظات أبداً.
7. تبلّغ أداة التكافؤ عن صفر مفاتيح مفقودة، وصفر انحراف عناصر نائبة، وثلاثة مفاتيح غير مستخدمة
   موثّقة كمحجوزة للإصدار التالي. وينجح البناء.

لا يتغير تسجيل مزوّد وقت التشغيل خلال أي من ذلك. ويستمر التطبيق في خدمة الكتالوجات السابقة حتى
تُدمج الملفات المُراجَعة وتُنشر.

## مآزق

**مأزق:** التعامل مع التصدير كنسخة احتياطية. إنه أثر تسليم. والكتالوج المرجعي هو ملف JSON أو XML
(أو المخزن)؛ أعد توليد التصدير منه بعد أي تغيير في المصدر.

**مأزق:** استيراد ملف ترجمة فوق كتالوج تغيّر. افحص
<code dir="ltr">StaleSource</code> ومجموعة مفاتيح المصدر قبل التطبيق. والصيغ التي لا تستطيع تمثيل
لقطة مصدر لن تحذّرك.

**مأزق:** توقع كتابة الملاحظات في JSON أو XML. إنها بيانات تقرير. طبّقها عبر واجهة الملاحظات
المحكومة.

**مأزق:** استخدام CSV للغات كثيفة الجمع. لا يملك المخطط المسطح جدول جمع؛ وينطبق تعيين الزوج فقط.
استخدم PO أو XLIFF.

**مأزق:** تطبيق ترجمة بهدف فارغ "لمسح" قيمة. الأهداف الفارغة تكون
<code dir="ltr">Skipped</code>؛ ولا تُمحى القوالب الموجودة أبداً. احذف الإدخال أو حدّثه عبر المخزن
إذا كان ذلك هو المقصود.

**مأزق:** افتراض أن استيراداً بلا تغيير يعيد كتابة الملف. إنه لا يفعل ذلك، وهذا ما يجعل رحلات
الذهاب والعودة متطابقة بايتياً ممكنة.

## إلى أين بعد ذلك

- [صيغ التبادل](../03-reference/04-interchange-formats.md) للمجموعات الفرعية الكاملة للصيغ
  وحدود الفقد.
- [التدقيق والتاريخ](07-audit-and-history.md) لمسار الملاحظات والتدقيق المحكوم.
- [التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md) لتفاصيل فرض CI.
- [مرجع الأدوات](../03-reference/05-tooling-reference.md) لخيارات CLI ومخطط التقرير.

</div>
