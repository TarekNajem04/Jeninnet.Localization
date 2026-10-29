<div dir="rtl">

# توافق AOT والتقليص

هذا المستند عقد التوافق لـ Native AOT (Ahead-of-Time) والتقليص (trimming) والنشر أحادي الملف. ويسجّل ما يفرضه
المستودع، وما يُستبعد عمداً، وكيف يثبته مستهلك اختبار الدخان.

## ما الذي يُفرض

كل مشروع مكتبة قابل للتغليف يستهدف إطار تشغيل مدعوماً (<code dir="ltr">net8.0</code> أو <code dir="ltr">net9.0</code> أو <code dir="ltr">net10.0</code>) يُبنى بـ
<code dir="ltr">IsAotCompatible=true</code> (المعيَّن في
<code dir="ltr">Directory.Build.props</code>)، وهذا يفعّل محللات التقليص وAOT (عائلات
<code dir="ltr">IL2026</code>/<code dir="ltr">IL2070</code>+/
<code dir="ltr">IL3050</code>) ويوسم بيانات التجميعة الوصفية
<code dir="ltr">IsTrimmable</code>. ويعامل المستودع التحذيرات كأخطاء، فلا يمكن أن يهبط نمط غير آمن
للتقليص دون ملاحظة.

أُعيد العمل تحت  (وأثبتت المحللات كل موضع):

- **JSON المولّد من المصدر حيثما يُستخدم مسلسل** — حِمول ترجمة Azure وGoogle، ومزوّد كتالوج JSON،
  وترميز سلاسل تفاعل Blazor JS، وتقرير التكافؤ المؤصَّل، وحِمول الذاكرة الموزعة، كلها
  تُسلسل/تفكّ التسلسل عبر توليد المصدر <code dir="ltr">JsonSerializerContext</code>. ولا يبقى أي
  نداء <code dir="ltr">JsonSerializer</code> قائم على الانعكاس في
  <code dir="ltr">src/</code> أو <code dir="ltr">integrations/</code>.
- **التقاط موضع نداء المفتاح المفقود** يستخدم
  <code dir="ltr">DiagnosticMethodInfo.Create</code> (البديل الذي يوصي به المحلل والآمن لـ AOT عن
  <code dir="ltr">StackFrame.GetMethod</code>).
- **نقطة نهاية موارد JavaScript** تربط <code dir="ltr">RequestDelegate</code> بدلاً من مفوّض
  حد أدنى لواجهة API مرتبط بوسائط، فلا تحمل معالجة المسار ربطاً قائماً على الانعكاس.
- **سمات التحقق المعرَّبة** تحل مصدر رسالتها عبر
  <code dir="ltr">IStringLocalizerFactory</code> (تماماً كما يفعل غلاف الإطار
  <code dir="ltr">IStringLocalizer&lt;T&gt;</code>) بدلاً من إنشاء
  <code dir="ltr">IStringLocalizer&lt;TObject&gt;</code> فوق النوع المتحقَّق منه وقت التشغيل عبر
  <code dir="ltr">MakeGenericType</code>.

## الاستثناءات الموثّقة (غير آمنة لـ AOT عمداً)

1. **<code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code>** —
   مستبعد من عقد AOT (<code dir="ltr">IsAotCompatible=false</code>). ويصرّح EF Core نفسه بأنه
   غير متوافق تماماً مع التقليص أو Native AOT (إنشاء <code dir="ltr">DbContext</code> موسوم بـ
   <code dir="ltr">RequiresUnreferencedCode</code>/<code dir="ltr">RequiresDynamicCode</code> من
   EF Core). وعلى المستهلكين الذين يشغّلون نشراً مقلماً/AOT بالكامل استخدام مزوّدي قواعد البيانات
   غير EF. والاستبعاد مصرَّح به في ملف المشروع ومعاد في README المزوّد.
2. **<code dir="ltr">Jeninnet.Localization.Generators.Keys</code> و
   <code dir="ltr">Jeninnet.Localization.Analyzers.Localizability*</code>** — تجميعات أدوات وقت
   بناء <code dir="ltr">netstandard2.0</code>؛ ولا تُنفَّذ أبداً في التطبيق المنشور، لذا لا ينطبق
   عليها عقد AOT وقت التشغيل.
3. **سطح حل المُعرِّب الموسوم في سمات التحقق** — عند استخدام التقليص، تُوسم
   <code dir="ltr">LocalizedCompareAttribute</code> و
   <code dir="ltr">LocalizedMaxLengthAttribute</code> و
   <code dir="ltr">LocalizedMinLengthAttribute</code> و
   <code dir="ltr">LocalizedRangeAttribute(Type, ...)</code> و
   <code dir="ltr">LocalizedCustomValidationAttribute(Type, ...)</code> بـ
   <code dir="ltr">RequiresUnreferencedCode</code>/<code dir="ltr">DynamicallyAccessedMembers</code>
   لأن أنواع DataAnnotations الأساسية تحل الأعضاء بالانعكاس (Compare باسم الخاصية، و
   MaxLength/MinLength بانعكاس Count، وRange عبر TypeDescriptor، والمدققات المخصصة باسم الدالة).
   والوسوم نقل صادق لعقود BCL (Base Class Library) نفسها: تطبيق السمات كوسوم على مستوى المصدر يبقى بلا تحذير، بينما
   الإنشاء الديناميكي فوق مسارات مقلمة يبلّغ عن خطر التقليص. وتبقى المنشئات القائمة على الأعداد/
   السلاسل آمنة للتقليص بالكامل.
4. **اكتشاف الموارد القائم على السمات والكود أولاً
** — تعكس
   <code dir="ltr">FromAssembly</code>/<code dir="ltr">FromType</code> فوق بيانات
   <code dir="ltr">[LocalizedResource]</code> الوصفية التي قد يحذفها التقليص، وهي موسومة بـ
   <code dir="ltr">RequiresUnreferencedCode</code>، فيحصل المستدعون في نشر trimmed/AOT على تحذير
   IL2026 المنتشر. والمزامن المحكوم نفسه (<code dir="ltr">ResourceSynchronizer</code>) لا يحمل انعكاساً؛
   ويمرّر المستهلكون المقلمون قيم <code dir="ltr">ResourceDeclaration</code> صريحة بدلاً من ذلك.

## مصفوفة موقف الحزم

تعني **Compatible** أن <code dir="ltr">IsAotCompatible=true</code> وأن محللات trim/AOT مفروضة.
وتعني **Compatible with annotations** أن التجميعة متوافقة باستثناء المنشئات الموثّقة القائمة على
الانعكاس، والتي تنشر <code dir="ltr">RequiresUnreferencedCode</code>. وتعني **Excluded** أن
المشروع يعيّن <code dir="ltr">IsAotCompatible=false</code>. وتعني **Build-time** أن الحزمة تعمل في
مضيف المترجم/الأداة، لا في التطبيق المنشور.

| الحزمة | الموقف |
|---|---|
| [<code dir="ltr">Jeninnet.Localization.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Core</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Storage</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Memory</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Json</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Xml</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.ResX</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Satellite</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Po</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Pseudo</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.SqlServer</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SqlServer/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.MySql</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.MySql/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.PostgreSQL</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.SQLite</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SQLite/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | مستبعدة |
| [<code dir="ltr">Jeninnet.Localization.Translation.Azure</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Translation.Google</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Google/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Integrations.DataAnnotations</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md) | متوافقة مع وسوم |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Mvc.Validation</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Mvc.Validation/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Blazor</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Integrations.JavaScript</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.JavaScript/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Diagnostics</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Integrations.RequestLocalization</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.RequestLocalization/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Csv</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Po</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Interchange.ResX</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Xliff</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md) | متوافقة |
| [<code dir="ltr">Jeninnet.Localization.Generators.Keys</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) | وقت البناء فقط |
| [<code dir="ltr">Jeninnet.Localization.Analyzers.Localizability</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) | وقت البناء فقط |
| [<code dir="ltr">Jeninnet.Localization.Tools.Keys</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md) | أداة وقت بناء |

<code dir="ltr">Jeninnet.Localization.Analyzers.Localizability.CodeFixes</code> غير قابل للتغليف؛
إنه رفيق وقت بناء يُشحن داخل حزمة المحلل ويتبع موقف وقت البناء نفسه. راجع
[README الخاص به](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability.CodeFixes/README.md).

## الميزات المفسَّرة فقط تحت AOT

بحسب قرار ، محرك قواعد جمع CLDR (Common Locale Data Repository) قائم على الجداول ولا يجمع قواعد وقت التشغيل، لذا يشارك الجمع
في AOT بلا استثناءات. ولا تتطلب أي ميزة في هذه المكتبة مترجم JIT (Just-in-Time)؛ والشواغل الوحيدة المفسَّرة فقط
هي عقود انعكاس الأعضاء المذكورة أعلاه.

## مستهلك اختبار الدخان (شكل CI)

ينشر <code dir="ltr">samples/Jeninnet.Localization.Smoke.Aot</code> NativeAOT ويمارس
التعريب/التنسيق/التخزين المؤقت/الترجمة (الترجمة بنقل HTTP وهمي كي يعمل مسار JSON المولّد من
المصدر). الأمر:

<div dir="ltr">

```powershell
dotnet publish samples/Jeninnet.Localization.Smoke.Aot -c Release -r <rid>
```

</div>

بوابة سلسلة الأدوات: يتطلب Native AOT وجود MSVC على Windows أو
<code dir="ltr">clang</code> + <code dir="ltr">zlib1g-dev</code> على Linux؛ ويشغّل CI النشر
والتنفيذ على <code dir="ltr">ubuntu-latest</code> (نظام مصفوفة الاختبار) بعد توفير تلك الحزم. وعلى
جهاز بلا سلسلة الأدوات يفشل النشر عند مرحلة الربط الأصلي — وتلك هي البوابة، لا تخفيض صامت. ويُفحص
ثنائي اختبار الدخان نفسه بتشغيله (رمز الخروج <code dir="ltr">0</code>، و
<code dir="ltr">SMOKE OK</code>).

</div>
