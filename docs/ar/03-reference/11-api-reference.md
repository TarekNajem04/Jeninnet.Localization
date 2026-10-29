<div dir="rtl">

# دليل مرجع API (API reference guide)

مرجع API في هذا الموقع يولّده DocFX من توثيق XML للمشاريع الـ33 القابلة للتغليف عبر المنصات. وهو رفيق البحث
حسب الصنف للفصول المرجعية السردية.

## ما الذي يتضمنه

- الأنواع والأعضاء العامة والمحمية فقط. ولا تكون الأنواع الداخلية والخاصة والاختبارية والعينية
  وأنواع المستهلك جزءاً من المرجع المولّد.
- كل التجميعات الـ33 القابلة للتغليف عبر المنصات ممثَّلة: Core والمزوّدون والترجمة والتكاملات والتبادل والمولّد والمحلل
  وحزم الأدوات. وتُوثَّق حزمتا MAUI وWinUI الخاصتان بـWindows في README الخاص بكل حزمة.
- تُجمَّع الصفحات حسب مساحة الأسماء. وتصل خريطة الحزم بين الحزمة ومشروعها وREADME:
  [خريطة الحزم](../appendix/02-package-map.md).

ملف YAML المولّد تحت <code dir="ltr">docs/api/</code> غير ملتزم به؛ ويُعاد بناؤه من مشاريع المصدر عبر
سير عمل التوثيق. وتبقى الفصول المرجعية السردية مكان السلوك والثوابت والحدود.

## أدوات وقت البناء

الحزم التالية لوقت البناء فقط. وتوثّق صفحات API الخاصة بها سطح المترجم/الأداة، لا واجهة تعريب وقت
تشغيل:

- <code dir="ltr">Jeninnet.Localization.Generators.Keys</code> — مولّد المصدر. راجع
  [README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md).
- <code dir="ltr">Jeninnet.Localization.Analyzers.Localizability</code> — محلل
  <code dir="ltr">JLNLOC001</code>. راجع
  [README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md).
- <code dir="ltr">Jeninnet.Localization.Tools.Keys</code> — أداة .NET
  <code dir="ltr">jeninnet-localization-keys</code>. راجع
  [README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

تحمل حزمة المحلل أيضاً رفيق إصلاح الكود غير القابل للتغليف؛ وواجهته ليست سطح حزمة منفصلاً.

## كيف تتنقل فيه

استخدم إدخال **مرجع API** في تنقل الموقع. وتتبع شجرة مساحات الأسماء مساحات الأسماء العامة
للتجميعة؛ ويغطي مربع البحث أسماء الأنواع والأعضاء عبر كل الحزم.

ترتبط الفصول السردية مباشرةً بالعضو الذي تشرحه. مثلاً،
<xref:Jeninnet.Localization.Abstractions.IMessageLocalizer> هو عقد المُعرِّب، و
<xref:Jeninnet.Localization.Abstractions.Store.ILocalizationStore> هو عقد المخزن.

## الحدود

- تصف الصفحات المولّدة التوقيعات وتعليقات XML، لا سلوك وقت التشغيل. اقرأ الفصل السردي المرتبط
  لقواعد الارتداد والتخزين المؤقت والفشل والخيوط.
- التجميعة التي لا واجهة عامة لها ستنتج صفحة مساحة أسماء فارغة؛ ويفحص التحقق من التغطية التجميعات
  المفقودة، لا الأعضاء المفقودة.
- المرجع المولّد غير مؤصَّل منفصلاً. إنه يعكس شجرة المصدر الحالية، وهي غير منشورة؛ وخطوط أساس
  الواجهة هي بوابة التوافق.

## توثيق ذو صلة

- [خريطة الحزم](../appendix/02-package-map.md)
- [واجهة Core API](01-core-api.md)
- [واجهة التخزين](02-storage-api.md)
- [مرجع المزوّدين](03-provider-reference.md)
- [التوافق والإصدارات](07-compatibility-and-versions.md)
- [AOT (Ahead-of-Time) والتقليص](../aot-and-trimming.md)

## الترخيص

MIT — راجع [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

</div>
