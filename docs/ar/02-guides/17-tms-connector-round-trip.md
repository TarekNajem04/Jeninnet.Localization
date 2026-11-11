<div dir="rtl">

# رحلة موصّل TMS ذهاباً وإياباً

ينقل موصّل TMS الكتالوجات بين المستودع ونظام إدارة الترجمة دون تشعيب سير العمل
المحكوم. <code dir="ltr">Translation.Tms</code> هو العقد المحايد للمورّد
(<code dir="ltr">ITmsConnector</code>: سحب لقطة مترجمة، ودفع كتالوجات مترجمة مع
معاينة جافة وتبليغ تعارضات لكل مفتاح)؛ و<code dir="ltr">Translation.Tms.Crowdin</code>
ينفّذه على واجهة Crowdin REST API، و<code dir="ltr">Translation.Tms.Weblate</code>
على واجهة Weblate REST API (مستضافة ذاتياً أو سحابية). والموصّل أدوات سير عمل، لا مزوّد رسائل: لا يخدم
<code dir="ltr">IMessageLocalizer</code> أبداً، ولا يكتب ملفات كتالوجات أو مدخلات مخزن.
وتطبيق الدفع المعتمد يبقى التركيبة المحكومة للمنادي عبر
<code dir="ltr">IResourceAdminStore</code>.

## التعيين

| مفهوم الكتالوج | Crowdin | Weblate | ملاحظات |
|---|---|---|---|
| مفتاح الكتالوج | <code dir="ltr">identifier</code> للسلسلة عند وجوده، وإلا النص المصدر <code dir="ltr">text</code> | سياق الوحدة <code dir="ltr">context</code> عند وجوده، وإلا أول صيغة مصدر | مشاريع المفاتيح تحمل مفاتيح صريحة؛ وإلا فالنص المصدر هو المفتاح. |
| القالب | محتوى الترجمة <code dir="ltr">content</code> | أول صيغة هدف | <code dir="ltr">null</code> عندما يحمل المورّد المفتاح بلا ترجمة بعد. |
| ملاحظة المترجم | سياق السلسلة <code dir="ltr">context</code> | ملاحظة الوحدة <code dir="ltr">note</code> | تُعرض عند السحب؛ وتُرفع من ملاحظات الطلب عند الدفع. |
| جداول الجمع | صفوف الترجمة بصيغة الجمع | وحدات بأكثر من صيغة مصدر أو هدف | خارج المجموعة الفرعية المسطّحة؛ تُتخطى عند السحب، و<code dir="ltr">UnsupportedKey</code> عند الدفع. |

محتوى صيغ الجمع هو الحد الموثّق الوحيد: فالسلسلة التي تحمل ترجماتها اسم صيغة جمع
(Crowdin) أو الوحدة ذات الصيغ المتعددة (Weblate) لا تدخل خريطة السحب المسطّحة أبداً،
ودفع هدف مسطّح لها يبلّغ <code dir="ltr">UnsupportedKey</code>.

## الرحلة ذهاباً وإياباً

1. **السحب.** يعيد <code dir="ltr">PullAsync(culture)</code> لقطة المورّد: القوالب مع
   الملاحظات مفتاحاً بمفتاح الكتالوج، مع تسجيل لغة مصدر المورّد على النتيجة.
2. **التصدير إلى ملف تبادل.** اكتب الكتالوج (المسحوب أو الحالي) بمرمّزات تبادل PO/XLIFF
   وسلّم الملف للمترجمين، كما في [سير عمل المترجم](11-translator-workflow.md) تماماً.
3. **معاينة الدفع.** يقارن <code dir="ltr">PushAsync</code> مع
   <code dir="ltr">DryRun: true</code> الهدف المقترح مع لقطة المورّد الحية ويبلّغ لكل
   مفتاح دون رفع أي شيء.
4. **الدفع.** الطلب نفسه مع <code dir="ltr">DryRun: false</code> يرفع ملفاً مترجماً
   واحداً (<code dir="ltr">messages.{culture}.po</code> أو <code dir="ltr">.xliff</code>)
   عندما يتغيّر مفتاح واحد على الأقل. والقيم المقترحة الفارغة لا تمسح ترجمات المورّد
   أبداً.
5. **التطبيق.** استورد الترجمات المعتمدة عبر المخزن المحكوم
   (<code dir="ltr">IResourceAdminStore</code>)، وهو مسار الكتابة نفسه الذي تستخدمه
   واردات التبادل.
6. **البوابة.** شغّل فحص [التكافؤ](13-parity-and-quality-gates.md) قبل السحب (خط أساس
   نظيف) وبعد التطبيق (لا مفاتيح مفقودة جديدة، ولا انحراف عناصر نائبة).

## التسجيل

<div dir="ltr">

```csharp
services.AddCrowdinTmsConnector(options => {
    options.ApiToken = "<Crowdin personal access token>";
    options.ProjectId = 123456;
    options.FileId = 789;
});
```

</div>

يختار <code dir="ltr">ProjectId</code> مشروع Crowdin؛ و<code dir="ltr">FileId</code> هو ملف
المشروع الذي تلتصق به الرفوعات المترجمة (السحب لا يحتاجه أبداً، والدفع الحي يشترطه).
وتضبط مؤسسات Enterprise مضيفها <code dir="ltr">Endpoint</code> الخاص. وكائن الخيارات
لقطة إنشاء؛ وتعديله بعد التسجيل لا يغيّر شيئاً.

<div dir="ltr">

```csharp
services.AddWeblateTmsConnector(options => {
    options.ApiToken = "<Weblate API token>";
    options.Endpoint = "https://hosted.weblate.org";
    options.Project = "my-project";
    options.Component = "messages";
});
```

</div>

<code dir="ltr">Endpoint</code> هو أصل خادم Weblate (الخدمة المستضافة أو نسخة
مستضافة ذاتياً — لا يوجد مضيف سحابي ثابت)؛ و<code dir="ltr">Project</code> و
<code dir="ltr">Component</code> هما اللاحقتان اللتان تختاران مجموعة الترجمة. وتنطبق
قاعدة لقطة الإنشاء نفسها.

## مثال السحب

<div dir="ltr">

```csharp
ITmsConnector connector = /* from DI */;

TmsPullResult pulled = await connector.PullAsync(
    CultureInfo.GetCultureInfo("ar"), cancellationToken);

// Templates keyed by catalog key; null means carried but untranslated.
string? template = pulled.Templates["App.Title"];
string? note = pulled.Notes.GetValueOrDefault("App.Title");
```

</div>

## مثال الدفع

<div dir="ltr">

```csharp
TmsPushRequest request = new() {
    Culture = CultureInfo.GetCultureInfo("ar"),
    Source = sourceTemplates,      // governance: unknown-key and stale-source detection
    Target = translatedTemplates,  // proposed translations
    Notes = translatorNotes,       // carried in the uploaded file
    Format = TmsCatalogFormat.Po,
    DryRun = true,                 // preview first
};

TmsPushReport preview = await connector.PushAsync(request, cancellationToken);
foreach (TmsPushEntry entry in preview.Entries) {
    Console.WriteLine($"{entry.Key}: {entry.Status} — {entry.Detail}");
}

// The same request with DryRun = false uploads messages.ar.po when keys changed.
```

</div>

## نتائج التعارض

يعيد الدفع استخدام مفردات <code dir="ltr">CatalogKeyStatus</code> للتبادل، فتُقرأ تعارضات
TMS مثل تقارير الاستيراد تماماً:

| الحالة | المعنى |
|---|---|
| <code dir="ltr">Skipped</code> | القيمة المقترحة فارغة؛ وترجمات المورّد القائمة لا تُمسح أبداً. |
| <code dir="ltr">UnknownKey</code> | المفتاح ليس في مشروع المورّد. |
| <code dir="ltr">StaleSource</code> | نص مصدر المورّد يختلف عن مصدر الطلب؛ والاقتراح بُني على محتوى قديم. |
| <code dir="ltr">Unchanged</code> | ترجمة المورّد تطابق المقترح أصلاً. |
| <code dir="ltr">UnsupportedKey</code> | سلسلة بصيغة جمع، أو (في رفوعات XLIFF) مفتاح ينقصه قالب المصدر في الطلب. |
| <code dir="ltr">Updated</code> | رُفعت الترجمة (أو كانت ستُرفع في المعاينة الجافة). |

يحصي <code dir="ltr">ConflictCount</code> نوعي <code dir="ltr">UnknownKey</code> و
<code dir="ltr">StaleSource</code>. ويُضبط <code dir="ltr">UploadedFileName</code> فقط
عندما يرفع الدفع الحي ملفاً.

## رحلة الملاحظات ذهاباً وإياباً

تركب ملاحظات المترجم الاتجاهين حيث تحملها الصيغة: سياق السلسلة
<code dir="ltr">context</code> عند السحب، وتعليقات <code dir="ltr">#.</code> في PO (أو
ملاحظات المترجم في XLIFF) في الملف المرفوع عند الدفع. والملاحظات لا تبوّب التطبيق
أبداً، ولا يكتبها الموصّل في ملفات كتالوجات؛ وطبّقها عبر
<code dir="ltr">IResourceAdminStore.SetNoteAsync</code> مثل أي استيراد تبادل.

## التشغيل

- **المصادقة.** يسافر رمز الوصول الشخصي في ترويسة <code dir="ltr">Authorization</code>
  (<code dir="ltr">Bearer</code> لـ Crowdin و<code dir="ltr">Token</code> لـ
  Weblate). ولا يُسجَّل أبداً ولا يظهر في رسائل الاستثناء أبداً. عامله مثل أي
  مفتاح ترجمة آلية: أسرار محددة النطاق بالبيئة، ولا تسجيل طلبات يلتقط الترويسات.
- **النداءات.** محاولة واحدة لكل تبادل HTTP مع ميزانية <code dir="ltr">Timeout</code>
  لكل عملية (الافتراضي 30 ثانية)؛ ويظهر تجاوزها
  <code dir="ltr">OperationCanceledException</code>. وتظهر إخفاقات المورّد
  <code dir="ltr">TmsConnectorException</code> مع رمز الحالة ومقتطف من الجسم. ويتبع
  الترقيم مؤشر المورّد (Crowdin: 500 صف لكل صفحة؛ Weblate: روابط الصفحة
  <code dir="ltr">next</code>، 1000 صف لكل صفحة). وتعمل رفوعات Weblate
  <code dir="ltr">method=translate</code> فلا يتغيّر إلا ما قورن.
- **فحوص قبل/بعد.** الموصّل لا يشغّل التكافؤ بنفسه. شغّل أداة التكافؤ قبل السحب وبعد
  التطبيق؛ راجع [التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md).

## مآزق

**مأزق:** الدفع دون معاينة جافة أولاً. فالمعاينة الجافة سطح مراجعة المصادر القديمة
والمفاتيح المجهولة؛ والدفع الحي بمصدر قديم يرفع ترجمة لمحتوى قديم.

**مأزق:** توجيه Weblate إلى الخادم أو المكوّن الخطأ. فـ
<code dir="ltr">Endpoint</code> هو أصل الخادم لا مسار API كامل، ويجب أن يقبل المكوّن
صيغة الدفع (رفع PO إلى مكوّن XLIFF-only يفشل من الخادم
بـ<code dir="ltr">TmsConnectorException</code> لا بتقرير لكل مفتاح).

**مأزق:** توقع رحلة ذهاب وإياب لجداول الجمع. إنها لا تحدث: فسلاسل صيغ الجمع تُتخطى
عند السحب وتُرفض عند الدفع. وترجم الجموع عبر ملفات التبادل، حيث الجداول صريحة.

**مأزق:** معاملة الموصّل كمصدر معرِّب. فتسجيله
<code dir="ltr">IInnerMessageProvider</code> خطأ تصنيفي؛ وقاعدة المزوّد الواحد لا
تتغيّر، وحزمة الموصّل لا تنفّذ واجهة المزوّد.

## إلى أين بعد ذلك

- [سير عمل المترجم](11-translator-workflow.md) لحلقة الترجمة المبنية على الملفات.
- [صيغ التبادل](../03-reference/04-interchange-formats.md) للمجموعتين الفرعيتين PO/XLIFF
  التي تُبنى منهما الرفوعات.
- [التكافؤ وبوابات الجودة](13-parity-and-quality-gates.md) لفرض CI حول الرحلة.
- [الترجمة الآلية](../02-guides/12-machine-translation.md) للبديل وقت الطلب (أثناء
  التشغيل).

</div>
