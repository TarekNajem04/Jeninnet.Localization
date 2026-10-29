<div dir="rtl">

# ملاحظات الإصدار (release notes)

هذه الصفحة سجل الإصدار الموجه للمستخدم. ويبقى <code dir="ltr">CHANGELOG.md</code> يوميات
التطوير؛ أما هذه الصفحة فتلخّص ما يحتويه الإصدار وما يجوز للمستهلك الاعتماد عليه.

## الحالة الحالية

**غير منشور.** لم تُنشر أي حزم NuGet. إصدار المنتج المصرَّح به في البناء هو
<code dir="ltr">1.0.0.0</code> (وإصدار الحزمة <code dir="ltr">1.0.0</code>)، وهو عنصر نائب
تطويري حتى أول إصدار عام. وإلى ذلك الحين، استخدم سير عمل الموجز المحلي في
[التثبيت](../01-getting-started/02-installation.md).

## 1.0 — أول إصدار عام (مخطط)

يُقصد أن يتضمن الإصدار الأول المكتبة الكاملة التي يصفها هذا الكتاب:

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

سير عمل النشر المقصود هو:

<div dir="ltr">

```powershell
dotnet add package Jeninnet.Localization.Core
dotnet add package Jeninnet.Localization.Providers.Json
```

</div>

ما دام المستودع غير منشور، احزم الحل في الموجز المحلي وأضف
<code dir="ltr">--source</code>:

<div dir="ltr">

```powershell
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
```

</div>

توثّق ملفات README للحزم التسجيل والخيارات والسلوك لكل حزمة.
ويغطي [فصل التثبيت](../01-getting-started/02-installation.md) إعداد المشروع وأول تسجيل.

## التوافق

- تستهدف مكتبات وقت التشغيل <code dir="ltr">net8.0</code> و<code dir="ltr">net9.0</code> و<code dir="ltr">net10.0</code>؛ وإصدار لغة C# هو 14.0.
- تستهدف أدوات Roslyn وقت البناء <code dir="ltr">netstandard2.0</code> لأنها تعمل في المترجم.
- تستهدف أداة التكافؤ <code dir="ltr">net8.0</code> و<code dir="ltr">net10.0</code> وهي مغلّفة كأداة .NET.
- لا يوجد استهداف متعدد ولا ادعاء دعم لإصدارات .NET الأقدم.
- موقف AOT (Ahead-of-Time) والتقليص، بما فيه استبعاد EF Core وأسطح الانعكاس الموسومة، موثّق في
  [AOT والتقليص](../aot-and-trimming.md) و
  [التوافق والإصدارات](07-compatibility-and-versions.md).

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

## التغييرات الكاسرة قبل 1.0

لم تُنشر أي واجهة عامة، لذا لا يوجد عبء ترحيل على المستهلكين بعد. ومع ذلك يسجّل المستودع كل تغيير
عام مقصود في <code dir="ltr">CHANGELOG.md</code> بالبادئة **Breaking:** ويحدّث خطوط أساس الواجهة
الملتزم بها في التغيير نفسه. وإلى أن تُنشر أول حزمة، تُسمح إعادات التسمية المرجعية؛ وبعد النشر
يصبح خط الأساس بوابة التوافق الموصوفة في
[التوافق والإصدارات](07-compatibility-and-versions.md).

## القيود المعروفة

الحدود التي يجب أن يقرأها المقيّم قبل اعتماد المكتبة مجموعة في
[القيود المعروفة](10-known-limitations.md). وهي خيارات تصميم متعمدة، لا قائمة
أخطاء.

## توثيق ذو صلة

- [التثبيت وأول تسجيل](../01-getting-started/02-installation.md)
- [التوافق والإصدارات](07-compatibility-and-versions.md)
- [القيود المعروفة](10-known-limitations.md)
- [خريطة الحزم](../appendix/02-package-map.md)
- [النشر](../04-operations/01-deployment.md)
- [سجل تغييرات المستودع](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CHANGELOG.md)
- [README المستودع](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md)

## الترخيص

MIT — راجع [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

</div>
