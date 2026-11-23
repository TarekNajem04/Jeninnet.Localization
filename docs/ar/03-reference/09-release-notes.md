<div dir="rtl">

# ملاحظات الإصدار (release notes)

هذه الصفحة سجل الإصدار الموجه للمستخدم. ويبقى <code dir="ltr">CHANGELOG.md</code> يوميات
التطوير؛ أما هذه الصفحة فتلخّص ما يحتويه الإصدار وما يجوز للمستهلك الاعتماد عليه.

## الحالة الحالية

**<code dir="ltr">1.0.0-preview.2</code> — أحدث إصدار معاينة منشور، بتاريخ 2026-11-18.** حزمه
الأربع والأربعون متاحة على NuGet بالإصدار <code dir="ltr">1.0.0-preview.2</code>. أما
<code dir="ltr">1.0.0-preview.3</code> فهو قيد التثبيت ولم يُنشر بعد. وهو إصدار معاينة وليس
إصداراً مستقراً: ثبّت الإصدار الدقيق وراجع <code dir="ltr">CHANGELOG.md</code> قبل الترقية.

- [أحدث إصدار على GitHub](https://github.com/TarekNajem04/Jeninnet.Localization/releases)
- [حزم NuGet](https://www.nuget.org/packages/Jeninnet.Localization.Core)
- [دليل الترحيل](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/MIGRATION.md)

## 1.0.0-preview.3 — مرشّح التثبيت (لم يُنشر بعد)

لا يضيف هذا الإصدار واجهة عامة جديدة. بل يجمّد السطح ويزيل مخاطر الإصدار قبل <code dir="ltr">1.0.0</code>.

- **تجميد الواجهة.** خطوط الأساس الأربعة والأربعون متطابقة في المحتوى مع الشجرة الحالية؛ وأعضاء
  <code dir="ltr">[Obsolete]</code> الستة المتعمدة للـ CRUD الخام لم تتغيّر. ولا يُقرّ
  <code dir="ltr">api-compat</code> إلا بإعادة التسمية المسجّلة أدناه.
- **بوابات المحللات والجودة.** حُلّت 21 رسالة IDE على مستوى info، وسجل رسائل المحللات صفر،
  ويفرض CI الآن تشخيصات النمط والمحللات على مستوى info. وبوابة Sonar خضراء وبتكرار جديد 0%.
- **حداثة التبعيات.** SonarAnalyzer 10.37 وWindows SDK BuildTools 10.0.28000 وتصحيح Npgsql 10.0.4
  داخل الخط. وخمس ترقيات كبرى مؤجلة بشروط خروج مسجّلة.
- **خدمة .NET 11.** SDK/runtime <code dir="ltr">11.0.100</code>/<code dir="ltr">11.0.0</code>
  وتوافق Roslyn <code dir="ltr">5.12</code> ودخان Native AOT أخضر على الهدفين
  (<code dir="ltr">win-x64</code>، و<code dir="ltr">linux-x64</code> في CI).
- **الأدلة.** تسجّل صفحة التوافق المصفوفة المتحقَّقة لنظام التشغيل × الإطار × AOT × المضيف ×
  قاعدة البيانات، بما فيها عمليات بحث حقيقية على SQL Server 2025 وMySQL 9 وPostgreSQL 17.
- **الأداء.** أعاد تحديث <code dir="ltr">2026-11-23</code> قياس كل مسار ساخن موثّق. وقد اكتُشف
  انحدار في إصابة المخزن المؤقت أحدثته إعادة هيكلة إزالة التكرار في هذا الإصدار وأُصلح: قيست
  إصابات المخزن المؤقت في الذاكرة عند <code dir="ltr">86–100 ns</code> قبل الإصلاح و
  <code dir="ltr">68–72 ns</code> بعده، عودة إلى نطاق
  <code dir="ltr">preview.2</code>. والظروف والنتائج مسجّلة في
  [أدلة الأداء](../04-operations/07-performance-evidence.md).
- **سلسلة التوريد.** يُرفق كل إصدار SBOM بصيغة SPDX 2.2 مولَّداً من الحزم المحزومة، وإثبات منشأ
  بناء من GitHub. وحزم المعاينة غير موقّعة بشهادة؛ وسلسلة التحقق وقرار التوقيع المؤجل موثّقان في
  [الأمان](../04-operations/02-security.md).

### الترقية من 1.0.0-preview.2

- **كاسر (نطاق المعاينة):** أصبح <code dir="ltr">ProviderFeature</code> هو
  <code dir="ltr">ProviderFeatures</code>. حدّث مراجع النوع؛ وأسماء الأعضاء وقيمها لم تتغيّر.
- **إضافي:** خيارات المزوّدين ترث الآن القاعدة المشتركة المجردة
  <code dir="ltr">TranslationProviderOptions</code>. واستخدام الخصائص الحالي متوافق مع المصدر،
  ويستمر ربط الإعدادات في العمل.
- **السلوك:** لم يتغيّر أي بحث أو ارتداد أو تنسيق أو اختيار مزوّد. والمجموعة الكاملة
  (2139 ناجحاً / 1 متجاوزاً لكل إطار مع تشغيل خدمات قواعد البيانات) هي الدليل.
- **الحزم:** ثبّت <code dir="ltr">1.0.0-preview.3</code> عند نشره؛ وحتى ذلك الحين يبقى
  <code dir="ltr">1.0.0-preview.2</code> أحدث معاينة منشورة.

## 1.0.0-preview.2 — عمق المنظومة

- **44 حزمة**، بإضافة حزمة الاكتشاف <code dir="ltr">Jeninnet.Localization</code> ومزوّدي INI وTOML
  ومزوّد DeepL للترجمة الآلية وعقد TMS المحايد مع موصّلي Crowdin وWeblate ومزوّد التركيب
  ومغلّفات التخزين المؤقت لمزوّدي الرسائل.
- **مصفوفة التشغيل:** <code dir="ltr">net10.0</code> (LTS) و<code dir="ltr">net11.0</code> (STS)
  من الكود نفسه؛ ويبقى MAUI على <code dir="ltr">net10.0</code> وWinUI على
  <code dir="ltr">net10.0-windows10.0.19041.0</code>.
- **الجودة:** قُبلت مديونية تكرار بنسبة 22.7% عند قطع المعاينة ثم أُزيلت بعدها؛ وبوابة Sonar خضراء
  على الفرع الرئيسي الحالي، وسجل رسائل المحللات أصبح إلزامياً في CI.

## 1.0.0-preview.1 — أول إصدار عام

يتضمن الإصدار الأول المكتبة الكاملة التي يصفها هذا الكتاب:

- **خط الأنابيب الأساسي والعقود** — <code dir="ltr">Jeninnet.Localization.Abstractions</code>
  و<code dir="ltr">Core</code> و<code dir="ltr">Storage</code>: مزوّدون مطابقون تماماً، وارتداد
  ثقافة بمالك واحد، وتنسيق، ودعم الجمع وICU، وجسر
  <code dir="ltr">IStringLocalizer</code> المسنود بالمخزن، والتسجيل.
- **مزوّدو الرسائل** — Memory وJSON وXML وResX وSatellite وPO/MO (Machine Object) وPseudo وSQL Server
  وPostgreSQL وMySQL وSQLite، ومخزن مجموعات موارد EF Core.
- **مزوّدو الترجمة** — Azure Translator v3 وGoogle Cloud Translation، مع التخزين المؤقت والتجميع
  والمسارد وذاكرة الترجمة والمرونة في جهة Azure.
- **تكاملات الإطار** — ثقافة مضيف Avalonia وMAUI وWinUI، وتعريب طلب ASP.NET Core، وتحقق MVC وتعريب العرض، وDataAnnotations، وBlazor
  (نطاق الدائرة والساكن)، وتسليم JavaScript، والتشخيص وفحوص الصحة.
- **التبادل** — صيغ CSV وPO وResX وXLIFF مع مفاتيح وحالات وملاحظات مترجم مشتركة.
- **الأدوات** — مولّد المفاتيح وقت الترجمة، ومحلل <code dir="ltr">JLNLOC001</code> وإصلاح
  الكود، وأداة التكافؤ <code dir="ltr">jeninnet-localization-keys</code>.
- **العينات وتطبيق المرجع** — تطبيق BookStore المرجعي عبر ستة مضيفات، والعينة الكلاسيكية بنمط
  <code dir="ltr">ResourceManager</code>.

جرد الحزم الكامل والتبعيات وإرشاد "أي حزمة أحتاج؟" في
[خريطة الحزم](../appendix/02-package-map.md).

## التثبيت

سير عمل النشر هو:

<div dir="ltr">

```powershell
dotnet add package Jeninnet.Localization.Core --version 1.0.0-preview.1
dotnet add package Jeninnet.Localization.Providers.Json --version 1.0.0-preview.1
```

</div>

للتطوير المحلي من الشجرة المصدرية، احزم الحل في الموجز المحلي وأضف
<code dir="ltr">--source</code>:

<div dir="ltr">

```powershell
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
```

</div>

توثّق ملفات README للحزم التسجيل والخيارات والسلوك لكل حزمة.
ويغطي [فصل التثبيت](../01-getting-started/02-installation.md) إعداد المشروع وأول تسجيل.

## الأطر والإصدارات المدعومة

- تستهدف مكتبات وقت التشغيل <code dir="ltr">net10.0</code> (LTS) و
  <code dir="ltr">net11.0</code> (STS) من الكود نفسه دون فروع شرطية لكل إطار؛ وإصدار لغة C# هو 15.0. (شحن
  <code dir="ltr">1.0.0-preview.1</code> مصفوفة <code dir="ltr">net8.0</code>/<code dir="ltr">net9.0</code>/<code dir="ltr">net10.0</code>؛
  وتقاعدت المصفوفة الثلاثية بعد نهاية دعم الإطارين الأقدم في نوفمبر 2026.)
- تستهدف أدوات Roslyn وقت البناء <code dir="ltr">netstandard2.0</code> لأنها تعمل في المترجم.
- تستهدف أداة التكافؤ <code dir="ltr">net10.0</code> وهي مغلّفة كأداة .NET.
- تحافظ تكاملات المضيفين على أهداف أحادية الإطار خاصة بها (MAUI <code dir="ltr">net10.0</code>، وWinUI
  <code dir="ltr">net10.0-windows10.0.19041.0</code>)؛ وتبقى العينات على <code dir="ltr">net10.0</code>.
- موقف AOT (Ahead-of-Time) والتقليص، بما فيه استبعاد EF Core وأسطح الانعكاس الموسومة، موثّق في
  [AOT والتقليص](../aot-and-trimming.md) و
  [الإصدارات والدعم](07-compatibility-and-versions.md).

## موقف التحقق

يخضع الإصدار لبوابة أدلة المستودع، لا لرقم إصدار:

- مجموعة الاختبارات الكاملة، بما فيها اختبارات البنية والعقود والمزوّدين والتكامل والتبادل
  والمحلل.
- 44 خط أساس عاماً للواجهة، وكلها ملتزمة به، واحد لكل تجميعة قابلة للتغليف.
- التحقق من الحزم وحل المستهلك، الذي يستعيد من الموجز المحزوم لا من مراجع المشروع.
- مستهلك اختبار الدخان Native AOT في CI.
- تطبيق المرجع عبر API وMVC وBlazor Server وWinForms وWPF وBlazor Hybrid.

تتغير الأعداد الدقيقة مع نمو المجموعة؛ و<code dir="ltr">CHANGELOG.md</code> ومشروع الاختبارات
هما مصدر الحقيقة.

## سياسة التغيير في إصدارات المعاينة

<code dir="ltr">1.0.0-preview.1</code> هو أول مجموعة حزم منشورة، لذا لا توجد حزمة Jeninnet سابقة
للترقية منها. وقد يعدّل المشروع السطح العام بين إصدارات المعاينة؛ ويُسجَّل كل تغيير عام مقصود في
<code dir="ltr">CHANGELOG.md</code> وتُعاد خطوط أساس الواجهة الملتزم بها في التغيير نفسه. وبعد
الإصدار المستقر <code dir="ltr">1.0.0</code> تصبح خطوط الأساس بوابة الإصدار الموصوفة في
[الإصدارات والدعم](07-compatibility-and-versions.md).

## القيود المعروفة

الحدود التي يجب أن يقرأها المقيّم قبل اعتماد المكتبة مجموعة في
[القيود المعروفة](10-known-limitations.md). وهي خيارات تصميم متعمدة، لا قائمة
أخطاء.

## توثيق ذو صلة

- [التثبيت وأول تسجيل](../01-getting-started/02-installation.md)
- [الإصدارات والدعم](07-compatibility-and-versions.md)
- [القيود المعروفة](10-known-limitations.md)
- [خريطة الحزم](../appendix/02-package-map.md)
- [النشر](../04-operations/01-deployment.md)
- [دليل الترحيل](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/MIGRATION.md)
- [سجل تغييرات المستودع](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CHANGELOG.md)
- [README المستودع](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md)

## الترخيص

MIT — راجع [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

</div>
