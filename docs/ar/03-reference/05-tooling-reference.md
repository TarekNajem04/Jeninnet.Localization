<div dir="rtl">

# مرجع الأدوات (tooling reference)

يشحن المستودع ثلاث أدوات وقت بناء: مولّد مفاتيح (key generator)، ومحلل قابلية تعريب
(localizability analyzer) مع إصلاح كود (code fix)، وأداة التكافؤ (parity tool) بسطر الأوامر.
ولا واحدة منها تبعية وقت تشغيل. يستهدف المولّد والمحلل
<code dir="ltr">netstandard2.0</code> لتوافق مضيف Roslyn؛ وأداة التكافؤ تطبيق كونسول
<code dir="ltr">net8.0</code> و<code dir="ltr">net10.0</code> مغلّف كأداة .NET.

## أداة التكافؤ

الحزمة: <code dir="ltr">Jeninnet.Localization.Tools.Keys</code>. الأمر:
<code dir="ltr">jeninnet-localization-keys</code>. لا توجد أوامر فرعية. وللأداة أمر فحص واحد
وتخرج برمز نتيجة.

<div dir="ltr">

```bash
dotnet tool install --global Jeninnet.Localization.Tools.Keys
jeninnet-localization-keys --catalog ./Localization --source ./src --report parity.json
```

</div>

من نسخة مستودع محلية قبل النشر:

<div dir="ltr">

```bash
dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys -- \
  --catalog ./Localization --source ./src --report parity.json
```

</div>

### الخيارات

| الخيار | مطلوب | قابل للتكرار | المعنى |
|---|---|---|---|
| <code dir="ltr">--catalog &lt;dir&gt;</code> | نعم | نعم | جذر الكتالوج المفحوص تكرارياً بحثاً عن <code dir="ltr">.json</code> و<code dir="ltr">.xml</code> و<code dir="ltr">.resx</code> و<code dir="ltr">.po</code>. |
| <code dir="ltr">--source &lt;dir&gt;</code> | نعم | نعم | جذر المصدر المفحوص تكرارياً بحثاً عن <code dir="ltr">.cs</code> و<code dir="ltr">.razor</code> و<code dir="ltr">.cshtml</code>. |
| <code dir="ltr">--keys-file &lt;file&gt;</code> | لا | نعم | ملف C# إضافي يُحلَّل لتعريفات <code dir="ltr">const string</code>. |
| <code dir="ltr">--exclude &lt;key&gt;</code> | لا | نعم | استبعاد مفتاح في كل مكان، أو مفتاح محدد الثقافة بالشكل <code dir="ltr">culture:key</code>. |
| <code dir="ltr">--default-culture &lt;tag&gt;</code> | لا | لا | الثقافة الافتراضية التي تغلق سلاسل الارتداد. الافتراضي <code dir="ltr">en</code>. |
| <code dir="ltr">--report &lt;path&gt;</code> | لا | لا | كتابة تقرير JSON في هذا المسار. |
| <code dir="ltr">--help</code> و<code dir="ltr">-h</code> و<code dir="ltr">-?</code> | لا | لا | طباعة الاستخدام. |
| <code dir="ltr">--version</code> | لا | لا | طباعة إصدار تجميعة الأداة. |

يلزم جذر <code dir="ltr">--catalog</code> واحد على الأقل وجذر
<code dir="ltr">--source</code> واحد. ويجب أن توجد الأدلة. وملف مفاتيح مفقود أو ثقافة افتراضية
مجهولة خطأ استخدام.

### رموز الخروج

| الرمز | المعنى |
|---|---|
| <code dir="ltr">0</code> | نظيف: لا نتائج. |
| <code dir="ltr">1</code> | نتيجة تكافؤ واحدة أو أكثر. |
| <code dir="ltr">2</code> | خطأ استخدام أو بيئة: وسائط سيئة، دليل مفقود، جذر إدخال غير قابل للقراءة. |

الأداة للتقارير فقط. لا تكتب كتالوجات، ولا تطبّق ترجمات، ولا تعدّل المصدر. والكتابة خارج النطاق؛
والمخزن المحكوم يملك الكتابات.

### قواعد الاستخراج

يُحلّل C# نحوياً بـ Roslyn؛ ولا يلزم تجميع. ويتعرف المستخرج على:

- وصولات <code dir="ltr">*Keys.Member</code> التي ينتهي معرّف مالكها بـ
  <code dir="ltr">Keys</code> ولا يُنادى عضوها.
- النصوص الحرفية في الوسيط الأول لـ
  <code dir="ltr">GetMessageTemplateAsync</code> و<code dir="ltr">GetMessageTemplatesAsync</code>
  و<code dir="ltr">GetPluralTemplateAsync</code> و
  <code dir="ltr">GetPluralTemplateWithContextAsync</code> و<code dir="ltr">LocalizeAsync</code>
  و<code dir="ltr">Localize</code> و<code dir="ltr">LocalizePluralAsync</code> و
  <code dir="ltr">LocalizeWithContextAsync</code> و<code dir="ltr">GetString</code>.
- المفهرسات أحادية السلسلة على مستقبلات ينتهي اسمها بـ
  <code dir="ltr">Localizer</code> (<code dir="ltr">localizer["Key"]</code>).
- تعريفات <code dir="ltr">const string</code> في صفوف <code dir="ltr">*Keys</code> وفي مدخلات
  <code dir="ltr">--keys-file</code>.

تُفحص عروض Razor نصياً بحثاً عن وصولات الأعضاء والنصوص الحرفية في مواضع المفاتيح. وتُتخطى الملفات
المولّدة (<code dir="ltr">*.g.cs</code> و<code dir="ltr">*.Designer.cs</code>) ومجلدا
<code dir="ltr">bin</code>/<code dir="ltr">obj</code>. وتُحصد الثوابت المولّدة على القرص
كتعريفات.

### مخطط التقرير

يكتب <code dir="ltr">--report</code> ملف JSON يحتوي
<code dir="ltr">schemaVersion: 2</code>، و<code dir="ltr">tool</code>، و
<code dir="ltr">toolVersion</code>، و<code dir="ltr">defaultCulture</code>، و
<code dir="ltr">catalogs</code>، و<code dir="ltr">ignoredFiles</code>، و
<code dir="ltr">sources</code>، و<code dir="ltr">keyDefinitions</code>، و
<code dir="ltr">excludedKeys</code>، و<code dir="ltr">matrix</code>، و
<code dir="ltr">findings</code>، و<code dir="ltr">summary</code>. والنتيجة كائن
<code dir="ltr">{ kind, key, culture, message, locations[] }</code>؛ وفي الملخص
<code dir="ltr">total</code> وعداد واحد لكل نوع (<code dir="ltr">missingKeys</code> و
<code dir="ltr">unusedKeys</code> و<code dir="ltr">templateDrifts</code> و
<code dir="ltr">formatGaps</code> و<code dir="ltr">placeholderDrifts</code> و
<code dir="ltr">unresolvedReferences</code> و<code dir="ltr">definitionConflicts</code> و
<code dir="ltr">catalogErrors</code>).

حالة <code dir="ltr">matrix[].cultures[].state</code> هي <code dir="ltr">served</code> أو
<code dir="ltr">fallback</code> أو <code dir="ltr">missing</code>. ويُرفع
<code dir="ltr">schemaVersion</code> عند تغيير كاسر للشكل؛ والإصدار 2 أضاف
<code dir="ltr">placeholder-drift</code> و<code dir="ltr">placeholderDrifts</code>. وكل
المسارات نسبية إلى دليل العمل وبشرطات مائلة أمامية. وتُرتَّب النتائج حسب النوع، ثم المفتاح، ثم
الثقافة، ثم المسار، ثم السطر.

### أنواع النتائج

- <code dir="ltr">missing-key</code> / <code dir="ltr">unused-key</code> — مشار إليه لكن غير
  مخدوم، أو مخدوم لكن غير مشار إليه.
- <code dir="ltr">template-drift</code> / <code dir="ltr">format-gap</code> — الصيغ تختلف، أو
  صيغة واحدة تحذف مفتاحاً مخدوماً.
- <code dir="ltr">placeholder-drift</code> — مجموعة العناصر النائبة تختلف عن الثقافة المرجعية.
- <code dir="ltr">unresolved-reference</code> / <code dir="ltr">definition-conflict</code> — اسم
  <code dir="ltr">*Keys</code> بلا تعريف أو بقيم متعارضة.
- <code dir="ltr">catalog-error</code> — كتالوج غير قابل للقراءة أو مشوّه، مع رقم سطر.

لا يوجد مخرج SARIF. تقرير JSON ومخرجات الطرفية الحتمية هما الصيغتان الوحيدتان.

## مولّد المفاتيح

الحزمة: <code dir="ltr">Jeninnet.Localization.Generators.Keys</code>. إنه مولّد مصدر تزايدي
Roslyn يُشحن كأصل محلل مع <code dir="ltr">DevelopmentDependency=true</code>. ولا يصبح تبعية وقت
تشغيل أبداً.

### المدخلات

تُصرَّح ملفات الكتالوج كملفات إضافية، ولا تُعتبر إلا الأسماء المنتهية بـ
<code dir="ltr">.loc.json</code>:

<div dir="ltr">

```xml
<ItemGroup>
  <AdditionalFiles Include="Localization\en.loc.json" />
  <AdditionalFiles Include="Localization\ar.loc.json" />
</ItemGroup>
```

</div>

كل ملف كائن JSON مسطح يربط مفاتيح نصية بقوالب نصية. ويرفض المولّد:

- JSON مشوّهاً أو جذراً غير كائن؛
- مفاتيح أو قيماً غير نصية؛
- مفاتيح مكررة في ملف واحد؛
- مفتاحين يُعقَّمان إلى المعرّف نفسه؛
- مفتاحاً بلا محارف معرّف؛
- مساحة أسماء مكوَّنة غير صالحة.

### تكوين مساحة الأسماء

مساحة الأسماء الافتراضية هي <code dir="ltr">{AssemblyName}.Generated</code>. تجاوزها بخاصية
MSBuild مرئية للمترجم:

<div dir="ltr">

```xml
<PropertyGroup><LocalizationKeysNamespace>MyApp.Localization</LocalizationKeysNamespace></PropertyGroup>
<ItemGroup><CompilerVisibleProperty Include="LocalizationKeysNamespace" /></ItemGroup>
```

</div>

وبدون <code dir="ltr">CompilerVisibleProperty</code> يستخدم المولّد مساحة الأسماء الافتراضية
بصمت. وينجح البناء، لذا يسهل تفويت مساحة الأسماء الخاطئة.

### الإخراج

يصدر المولّد وحدة ترجمة واحدة تحتوي:

<div dir="ltr">

```csharp
public static partial class LocalizationKeys {
    public const string APP_TITLE = "App.Title";
    public const string CART_ITEMS = "Cart.Items";
    public static readonly string[] All = new string[] { "App.Title", "Cart.Items" };
}
```

</div>

المعرّف بحالة الثعبان العلوية. وتقسّم الفواصل وحدود الحروف الكبيرة والمختصرات الكلمات؛ ويحصل
الرقم البادئ على شرطة سفلية. والثوابت و<code dir="ltr">All</code> مرتبة ترتيبياً. وتساهم
كتالوجات متعددة في صف واحد. والإخراج حتمي ومتطابق بايتياً لمدخل ثابت.

يصدر المولّد مفاتيح فقط، ولا يصدر نص رسالة أبداً. ويتبع كل كتالوج مصرَّح به، لذا فمفتاح موجود فقط
في ملف ترجمة يولّد ثابتاً أيضاً.

### التشخيصات

| المعرّف | الخطورة | المعنى |
|---|---|---|
| <code dir="ltr">JLNKEY001</code> | خطأ | الكتالوج ليس JSON سليم الشكل، أو جذره ليس كائناً. |
| <code dir="ltr">JLNKEY002</code> | خطأ | إدخال لا يربط مفتاحاً نصياً بقالب نصي. |
| <code dir="ltr">JLNKEY003</code> | خطأ | المفتاح نفسه يتكرر أكثر من مرة في ملف واحد. |
| <code dir="ltr">JLNKEY004</code> | خطأ | مفتاحان متمايزان يُعقَّمان إلى معرّف C# نفسه. |
| <code dir="ltr">JLNKEY005</code> | خطأ | مفتاح لا يحتوي محارف معرّف. |
| <code dir="ltr">JLNKEY006</code> | خطأ | مساحة الأسماء المكوَّنة ليست معرّف C# منقطاً صالحاً. |

كل فشل خطأ بناء مع ملف وسطر عندما يوفرهما المحتوى. والتوليد تزايدي؛ والكتالوجات غير المتغيرة تعيد
استخدام نتائج تحليل مخزّنة.

## محلل قابلية التعريب

الحزمة: <code dir="ltr">Jeninnet.Localization.Analyzers.Localizability</code>؛ وتحمل الحزمة أيضاً
تجميعة إصلاح الكود. والقاعدة الوحيدة <code dir="ltr">JLNLOC001</code>.

### ما يُوسم

- إسنادات نص واجهة المستخدم على مستقبلات واجهة: <code dir="ltr">Text</code> و
  <code dir="ltr">Title</code> و<code dir="ltr">Header</code> و<code dir="ltr">Content</code> و
  <code dir="ltr">Caption</code> و<code dir="ltr">ToolTip</code> و
  <code dir="ltr">PlaceholderText</code> على أنواع WinForms وWPF وWeb Forms و
  <code dir="ltr">ComponentBase</code>.
- إدخالات <code dir="ltr">ViewData["..."]</code> و<code dir="ltr">ViewBag....</code>.
- <code dir="ltr">MessageBox.Show("...")</code>.
- منشئات الاستثناءات، بما فيها <code dir="ltr">ValidationResult</code>.
- رسائل التحقق في <code dir="ltr">AddModelError</code>، ووسائط
  <code dir="ltr">ErrorMessage</code>، وسمات <code dir="ltr">Display</code>/
  <code dir="ltr">DisplayName</code>.
- <code dir="ltr">Results.Problem("...")</code> و
  <code dir="ltr">TypedResults.Problem("...")</code>.

المعرّفات واستدعاءات التسجيل وتأكيدات الاختبارات والمهيّئات المحلية والسلاسل الفارغة والتراكيب
غير المفتاحية صامتة بحكم البنية. وتُوسم الإسنادات عبر
<code dir="ltr">string.Format</code> أو <code dir="ltr">string.Concat</code> بصيغة حرفية؛ أما
التراكيب الأخرى فحدّ موثّق.

### الخطورة والكبت

الواصف افتراضياً <code dir="ltr">Hidden</code>. فعّل خطورة أعلى بخاصية MSBuild
<code dir="ltr">LocalizabilitySeverity</code> (<code dir="ltr">suggestion</code> أو
<code dir="ltr">warning</code> أو <code dir="ltr">error</code>) المعلنة مرئية عبر
<code dir="ltr">&lt;CompilerVisibleProperty Include="LocalizabilitySeverity" /&gt;</code>، أو
عبر <code dir="ltr">dotnet_diagnostic.JLNLOC001.severity = warning</code> في
<code dir="ltr">.editorconfig</code>. ويفوز <code dir="ltr">.editorconfig</code> عند التعارض.
ويعمل الكبت القياسي:
<code dir="ltr">#pragma warning disable JLNLOC001</code> أو
<code dir="ltr">[SuppressMessage]</code> أو قسم لكل مجلد.

تساوي <code dir="ltr">suggestion</code> و<code dir="ltr">hidden</code> القيمة
<code dir="ltr">DiagnosticSeverity.Hidden</code>. وتُسند <code dir="ltr">warning</code> و
<code dir="ltr">error</code> إلى شدتيهما المسمّاتين.

### إصلاح الكود

عندما يطابق النص الموسوم تماماً ثابت <code dir="ltr">const string</code> في نوع ينتهي اسمه بـ
<code dir="ltr">Keys</code> — مكتوباً يدوياً أو مولّداً — يستبدل الإصلاح النص بمرجع مؤهل بالكامل
إلى ذلك الثابت. ويحافظ الاستبدال على زخارف النص الحرفية (trivia) ولا يحتاج
<code dir="ltr">using</code> جديداً.

وليس للإصلاح موفّر إصلاح الكل. والنصوص بلا ثابت مطابق لا تحصل على إصلاح. ولا يستخرج المحلل
السلاسل إلى كتالوجات أبداً؛ فذلك يبقى قرار تأليف صريحاً، وتبلّغ أداة التكافؤ عن الفجوات الناتجة.

## مثال عملي

أضف <code dir="ltr">en.loc.json</code> كعنصر <code dir="ltr">AdditionalFiles</code>، وعيّن
<code dir="ltr">LocalizationKeysNamespace</code>، وأضف
<code dir="ltr">CompilerVisibleProperty</code> المطابق، وابنِ. ويكشف الصف المولّد
<code dir="ltr">CART_ITEMS</code> لمواضع النداء.

## مآزق

**مأزق:** استخدام <code dir="ltr">Content</code> بدلاً من
<code dir="ltr">AdditionalFiles</code>؛ فلا تُولَّد ثوابت.

**مأزق:** حذف <code dir="ltr">CompilerVisibleProperty</code>؛ فمساحة الأسماء تتراجع بصمت.

## إلى أين بعد ذلك

- [المفاتيح المكتوبة بقوة](../02-guides/05-strongly-typed-keys.md) لسير العمل.
- [التكافؤ وبوابات الجودة](../02-guides/13-parity-and-quality-gates.md) لربط CI.
- [مرجع التشخيص](../../en/03-reference/06-diagnostics-reference.md) للمعرّفات في جدول واحد.

</div>
