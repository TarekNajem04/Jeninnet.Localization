<div dir="rtl">

# ملاحظات الإصدار (release notes)

هذه الصفحة سجل الإصدار الموجه للمستخدم. ويبقى <code dir="ltr">CHANGELOG.md</code> يوميات
التطوير؛ أما هذه الصفحة فتلخّص ما يحتويه الإصدار وما يجوز للمستهلك الاعتماد عليه.

## الحالة الحالية

**<code dir="ltr">1.0.0-preview.1</code> — أول إصدار معاينة عام، نُشر في 2026-10-31.** جميع الحزم
الـ35 متاحة على NuGet بالإصدار <code dir="ltr">1.0.0-preview.1</code>. وهو إصدار معاينة وليس
إصداراً مستقراً: ثبّت الإصدار الدقيق وراجع <code dir="ltr">CHANGELOG.md</code> قبل الترقية.

- [إصدار GitHub](https://github.com/TarekNajem04/Jeninnet.Localization/releases/tag/v1.0.0-preview.1)
- [حزم NuGet](https://www.nuget.org/packages/Jeninnet.Localization.Core)
- [دليل الترحيل](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/MIGRATION.md)

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

- تستهدف مكتبات وقت التشغيل <code dir="ltr">net8.0</code> و<code dir="ltr">net9.0</code> و<code dir="ltr">net10.0</code>؛ وإصدار لغة C# هو 14.0.
- تستهدف أدوات Roslyn وقت البناء <code dir="ltr">netstandard2.0</code> لأنها تعمل في المترجم.
- تستهدف أداة التكافؤ <code dir="ltr">net8.0</code> و<code dir="ltr">net10.0</code> وهي مغلّفة كأداة .NET.
- لا يوجد استهداف متعدد ولا ادعاء دعم لإصدارات .NET الأقدم.
- موقف AOT (Ahead-of-Time) والتقليص، بما فيه استبعاد EF Core وأسطح الانعكاس الموسومة، موثّق في
  [AOT والتقليص](../aot-and-trimming.md) و
  [الإصدارات والدعم](07-compatibility-and-versions.md).

## موقف التحقق

يخضع الإصدار لبوابة أدلة المستودع، لا لرقم إصدار:

- مجموعة الاختبارات الكاملة، بما فيها اختبارات البنية والعقود والمزوّدين والتكامل والتبادل
  والمحلل.
- 35 خط أساس عاماً للواجهة، وكلها ملتزمة به، واحد لكل تجميعة قابلة للتغليف.
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
