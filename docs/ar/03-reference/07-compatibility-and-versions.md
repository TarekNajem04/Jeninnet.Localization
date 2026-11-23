<div dir="rtl">

# التوافق والإصدارات (compatibility and versions)

يوضح هذا الفصل موقف الدعم الذي يفرضه المستودع فعلاً. وهو محافظ عمداً: أول إصدار عام هو إصدار
معاينة، ووعود التوافق هي تلك المرمّزة في البناء وخطوط أساس الواجهة (API baselines) والاختبارات.

## الإطار الهدف واللغة

- **مكتبات وقت التشغيل:** <code dir="ltr">net10.0</code> (LTS بدعم حتى نوفمبر 2028) و
  <code dir="ltr">net11.0</code> (STS). وتشحن كل تجميعة عابرة للمنصات الأصلين
  <code dir="ltr">lib/net10.0</code> و<code dir="ltr">lib/net11.0</code> من الكود نفسه بلا
  فروع خاصة بإطار.
- **المضيفات والعينات:** <code dir="ltr">net10.0</code> (وتستخدم مضيفات سطح المكتب <code dir="ltr">net10.0-windows</code>، ويستخدم MAUI الإطار <code dir="ltr">net10.0</code> وWinUI الإطار <code dir="ltr">net10.0-windows10.0.19041.0</code>). وتتبع المضيفات دورات حياتها الخاصة وتبقى على <code dir="ltr">net10</code> طوال خط المعاينة.
- **تكاملات المنصات:** يستهدف MAUI الإطار <code dir="ltr">net10.0</code>، ويستهدف WinUI الإطار <code dir="ltr">net10.0-windows10.0.19041.0</code>، ويتبع Avalonia مكتبات وقت التشغيل (<code dir="ltr">net10.0</code> و<code dir="ltr">net11.0</code>).
- **إصدار لغة C#:** 15.0. يوفّر المترجم مع .NET 11 SDK، وتستهدف التجميعات الناتجة
  إطاري التشغيل .NET 10 LTS و.NET 11 STS.
- **أنواع المراجع القابلة للعدم (nullable):** مفعّلة في كل مشروع.
- **التحذيرات:** تُعامل كأخطاء، مع محللات .NET وSonarAnalyzer وفرض أسلوب الكود وقت البناء.
- **أدوات Roslyn وقت البناء:** مولّد المفاتيح ومحلل قابلية التعريب وتجميعة إصلاح الكود تستهدف
  <code dir="ltr">netstandard2.0</code> كي تُحمَّل في كل مضيف Roslyn. ولا تُنفَّذ أبداً في
  التطبيق المنشور.
- **أداة التكافؤ:** <code dir="ltr">net10.0</code> أحادي الإطار، مغلّفة كأداة .NET باسم الأمر
  <code dir="ltr">jeninnet-localization-keys</code> (وأداة <code dir="ltr">net10</code> تعمل تحت SDK أحدث عبر الترحيل).

تستهدف مكتبات وقت التشغيل .NET 10 و.NET 11. ويُفرض حدّ التوافق ببناء واختبار وتغليف
واستعادة كلا الإطارين؛ ويُفحص خط أساس الواجهة العامة نفسه (المستقل عن الإطار) للتجميعات
العابرة للمنصات على كل إطار، وتغطي مجموعة اختبارات مضيفات Windows خطوط الأساس الخاصة
بالمنصات. ولا يوجد ادعاء دعم لإصدارات .NET الأقدم.

> نوافذ الدعم: بلغ .NET 8 و.NET 9 نهاية الدعم في نوفمبر 2026. وشحن
> <code dir="ltr">1.0.0-preview.1</code> مصفوفة <code dir="ltr">net8.0</code>/<code dir="ltr">net9.0</code>/<code dir="ltr">net10.0</code>؛
> وابتداءً من الشجرة الحالية يُبنى <code dir="ltr">net10.0</code> (LTS) و<code dir="ltr">net11.0</code> (STS) ويُختبَران ويُشحَنان.
> ويبقى مستهلكو الإصدارات القديمة على <code dir="ltr">1.0.0-preview.1</code>.

## مصفوفة الدعم

| المجال | <code dir="ltr">net10.0</code> | <code dir="ltr">net11.0</code> | ملاحظات |
|---|---|---|---|
| مكتبات التشغيل (42 عابرة للمنصات) | بناء واختبار وتغليف | بناء واختبار وتغليف | الكود نفسه بلا فروع إطار؛ خط أساس واحد مستقل عن الإطار |
| مجموعة اختبارات المكتبة | خضراء | خضراء | التوقعات نفسها على الهدفين؛ رجل CI مستقل لكل إطار |
| مشروع القياسات | أخضر | يُبنى؛ لا يستطيع BDN تشغيله | BenchmarkDotNet 0.15.8 أقدم من net11 (راجع صفحة الأدلة) |
| دخان NativeAOT | نشر وتشغيل (win-x64 وlinux-x64) | نشر وتشغيل (win-x64 وlinux-x64) | دبابيس ILCompiler لكل إطار (<code dir="ltr">10.0.15</code> / <code dir="ltr">11.0.0</code>) |
| تحقق المستهلكين | استعادة وبناء واختبار وتشغيل | استعادة وبناء واختبار وتشغيل | من الخلاصة المغلّفة، بمجلدات إخراج لكل إطار |
| تكاملا MAUI وWinUI | <code dir="ltr">net10.0</code> / <code dir="ltr">net10.0-windows10.0.19041.0</code> | غير مستهدف | دورات حياة المضيفين (حزم MAUI 10 وWinAppSDK 1.8)؛ يبقيان على net10 طوال خط المعاينة |
| العينات | <code dir="ltr">net10.0</code> | غير مستهدفة | تتبع محددات المضيفين لا مصفوفة المكتبات |

### المصفوفة المتحقَّقة (2026-11-23)

المجموعات التالية شُغّلت فعلياً على الشجرة الحالية. وكلمة "متحقَّق" تعني بناءً مع دليل تشغيل أو
اختبار مناسب، وليست وعداً مستقبلياً.

| نظام التشغيل | بناء واختبارات النواة (<code dir="ltr">net10.0</code> / <code dir="ltr">net11.0</code>) | مجموعات المضيفين | دخان Native AOT |
|---|---|---|---|
| Windows | متحقَّق (محلياً وعلى <code dir="ltr">windows-latest</code>) | WinUI وWPF وWinForms وMAUI وAvalonia وBlazor وMVC وAPI: البناء متحقَّق، واختبارات مضيفات سطح المكتب والمنصات خضراء | <code dir="ltr">win-x64</code> متحقَّق على الهدفين (<code dir="ltr">SMOKE OK</code>) |
| Linux | متحقَّق (<code dir="ltr">ubuntu-latest</code>) | غير منطبق (مجموعات المضيفين خاصة بـ Windows) | <code dir="ltr">linux-x64</code> متحقَّق على الهدفين (CI) |
| macOS | متحقَّق (<code dir="ltr">macos-latest</code>) | غير منطبق | غير متحقَّق |

محركات قواعد البيانات (عمليات بحث حقيقية وقت التشغيل، على الهدفين):

| المحرك | الإصدار المتحقَّق | النتيجة |
|---|---|---|
| SQL Server | 2025 (حاوية) | بحث وقت التشغيل أخضر |
| MySQL | 9 (حاوية) | بحث وقت التشغيل أخضر |
| PostgreSQL | 17 (حاوية) | بحث وقت التشغيل أخضر |
| SQLite | في الذاكرة / ملف | مجموعات مخزن EF Core والمزوّد خضراء في التشغيل الافتراضي |

اختبارات وقت تشغيل قواعد البيانات اختيارية: تقرأ <code dir="ltr">JENINNET_TEST_SQLSERVER_CONNECTION</code>
و<code dir="ltr">JENINNET_TEST_MYSQL_CONNECTION</code> و<code dir="ltr">JENINNET_TEST_POSTGRESQL_CONNECTION</code>،
وتُبلِغ "غير حاسم" عند غياب المتغير كي يبقى تشغيل CI بلا خوادم أخضر. والجدول أعلاه يسجّل تشغيلاً
بوجود المحركات الثلاثة؛ ومصفوفة CI لا توفّرها.

## الإصدارات

- يوجد إصدار واحد على مستوى الحل، هو <code dir="ltr">1.0.0-preview.2</code>، مصرَّح به في
  <code dir="ltr">Directory.Build.props</code>.
- اسم المشروع واسم التجميعة ومساحة الأسماء الجذرية ومعرّف الحزمة موحّدة. ويجب ألا تصرّح المشاريع
  بـ <code dir="ltr">&lt;Version&gt;</code> الخاصة بها.
- <code dir="ltr">1.0.0-preview.1</code> هو أول إصدار عام، وهو منشور على NuGet لحزم <code dir="ltr">preview.1</code> الخمس والثلاثين.
  وهو إصدار معاينة، لا إصدار مستقر. ويضيف فرع <code dir="ltr">preview.2</code> حزمة الاكتشاف
  ومزوّدي INI وTOML ومزوّد DeepL وعقد موصّل TMS مع تنفيذي Crowdin وWeblate ومزوّد التركيب ومغلّفات التخزين المؤقت (44 مشروعاً قابلاً للتغليف).
- <code dir="ltr">CHANGELOG.md</code> مصدر ملاحظات الإصدار. ويسجّل التغييرات العامة المقصودة تحت
  <code dir="ltr">### Changed</code>؛ وقد يعدّل إصدار المعاينة السطح العام.
- توجد ستة أعضاء <code dir="ltr">[Obsolete]</code> متعمَّدة: فمسار الكتابة الخام في
  <code dir="ltr">ILocalizationResourceSet</code> (<code dir="ltr">Add</code>/
  <code dir="ltr">Update</code>/<code dir="ltr">Delete</code>، بالتحميلات المتزامنة وغير المتزامنة)
  مهمل لكنه باقٍ للبذر والاختبارات، بينما يجب أن تمر الكتالوجات المُدارة عبر
  <code dir="ltr">IResourceAdminStore</code>. ويسمح اختبار المعمارية بهؤلاء الستة حصراً؛ وأي
  <code dir="ltr">[Obsolete]</code> آخر يُفشل البناء. وتحتوي واجهة المعاينة المنشورة عدا ذلك على
  أسماء مرجعية فقط؛ وتحدث إعادات التسمية مباشرةً لا عبر طبقات توافق مهملة.

خطوط أساس الواجهة الملتزم بها هي بوابة التوافق. والتغيير الذي يعدّل توقيعاً عاماً حدث متعمَّد
ومُراجَع، لا حادث يتسلل إلى بناء.

## خطوط أساس الواجهة

لكل تجميعة قابلة للتغليف خط أساس ملتزم به تحت
<code dir="ltr">tests/Jeninnet.Localization.Tests/ApiBaselines/*.Api.txt</code>. والاختبار
<code dir="ltr">PublicApiSurface_MatchesCommittedBaselines</code> يحمّل كل تجميعة، ويُفرّغ سطحها
المصدَّر، ويقارنه سطراً بسطر. وإضافة عامة أو إزالة أو تغيير توقيع غير مقصود يفشل مع فرق.

- الجرد 44 تجميعة قابلة للتغليف. وتشترك التجميعات الـ42 العابرة للمنصات في خط أساس واحد تحت كل إطار تشغيل مدعوم؛ بينما تستهدف MAUI الإطار <code dir="ltr">net10.0</code> ويستهدف WinUI الإطار <code dir="ltr">net10.0-windows10.0.19041.0</code>، ويُتحقق من كليهما عبر مجموعة اختبارات مضيفات Windows.
- إعادة التوليد صريحة: عيّن <code dir="ltr">JENINNET_API_REGEN=1</code> وشغّل اختبار إعادة
  التوليد. وبدون المتغير يكون الاختبار غير حاسم، فلا تستطيع عملية اختبار عادية إعادة كتابة خطوط
  الأساس بصمت.
- خطوط الأساس لكل تجميعة، وحتمية، ودون اتصال. ولا تُستخدم أداة مقارنة واجهات تابعة لطرف ثالث.
- يعمل التحقق من الحزم في CI مع <code dir="ltr">EnablePackageValidation=true</code>، ويتحقق حل
  المستهلك من الحزم المغلّفة من الموجز المحلي.

القاعدة العملية للمساهمين: إذا فشل اختبار خط الأساس، فقرر هل تغيير الواجهة مقصود. فإن كان كذلك،
أعد توليد خط الأساس في التغيير نفسه واشرح السبب. وإن لم يكن، فأصلح الكود. لا تعد التوليد لجعل
بناء أحمر أخضر.

## عملية التغيير الكاسر

يسجّل المستودع قرارات معمارية (ADs) قبل التنفيذ. وملخص العملية، دون روابط خاصة:

1. **الاقتراح.** التغيير الذي يمس عقداً عاماً أو اتجاه تبعية أو هوية حزمة يبدأ اقتراح تصميم
   ويُسجَّل قراراً معمارياً.
2. **المراجعة.** يسجّل القرار العقد والبدائل والنتائج. وهنا يُحسم "هل يجب أن يكون هذا واجهة جديدة
   أم إعادة تسمية؟".
3. **التنفيذ.** يهبط التغيير مع الاختبارات والتوثيق المحدَّث.
4. **تحديث خط الأساس.** تُعاد ملفات <code dir="ltr">.Api.txt</code> المتأثرة عمداً.
5. **سجل التغييرات.** يُدرج التغيير العام المقصود تحت <code dir="ltr">### Changed</code> مع
   ملاحظة ترحيل عند الحاجة.
6. **التحقق من الحزم.** يجب أن تظل الحزم المغلّفة وحل المستهلك صالحين.

ما دام خط الإصدار معاينة، تظل التغييرات على السطح العام مسموحة، لكنها تتبع قواعد الظهور نفسها.
ويحتوي سجل التغييرات مثالاً ملموساً: أُعيدت تسمية نوع حالة XLIFF (XML Localization Interchange File Format) إلى
<code dir="ltr">CatalogKeyStatus</code> المشترك عبر عائلة التبادل.

بعد الإصدار المستقر <code dir="ltr">1.0.0</code>، تصبح الآلية نفسها وعد الواجهة: يُتوقع أن يتطلب
التغيير الذي يعدّل توقيعاً عاماً قرار إصدار رئيسي، وتحديث خط أساس، وملاحظة ترحيل، ومراجعة مخطط
تبعيات الحزم. ولم ينشر المستودع وتيرة إصدار ولا نافذة دعم طويل الأمد، لذا لا يُدَّعى أي وعد من هذا
النوع هنا.

## AOT والتقليص

كل مكتبة وقت تشغيل قابلة للتغليف تستهدف إطار تشغيل مدعوماً (<code dir="ltr">net10.0</code>
و<code dir="ltr">net11.0</code>) تُبنى بـ
<code dir="ltr">IsAotCompatible=true</code> ما لم تُستبعد صراحةً. وهذا يفعّل محللات
trim/AOT (Ahead-of-Time) ويوسم بيانات وصفية قابلة للتقليص؛ والتحذيرات أخطاء.

الاستثناءات موثّقة في [AOT والتقليص](../aot-and-trimming.md):

- **مخزن EF Core** — <code dir="ltr">IsAotCompatible=false</code> لأن EF Core ليس آمناً بالكامل
  للتقليص أو AOT.
- **تجميعات المولّد والمحلل** — أدوات وقت بناء <code dir="ltr">netstandard2.0</code>؛ ولا ينطبق
  عليها عقد وقت التشغيل.
- **أسطح الانعكاس الموسومة** — سمات التحقق التي تحل الأعضاء بالانعكاس و
  <code dir="ltr">LocalizedResourceDiscovery</code> موسومة بـ
  <code dir="ltr">RequiresUnreferencedCode</code> / <code dir="ltr">DynamicallyAccessedMembers</code>.
  والوسوم نقل صادق لسلوك BCL (Base Class Library).
- **JSON المولّد من المصدر** يُستخدم حيثما يُستخدم مسلسل؛ ونقطة نهاية JavaScript تربط
  <code dir="ltr">RequestDelegate</code>؛ والتقاط موضع نداء المفتاح المفقود يستخدم
  <code dir="ltr">DiagnosticMethodInfo.Create</code>.

قواعد الجمع قائمة على جداول ولا تجمع قواعد وقت التشغيل، لذا يشارك الجمع في AOT بلا استثناءات.
وينشر مستهلك Native AOT التجريبي ويشغّل مسارات التعريب والتنسيق والتخزين المؤقت والترجمة لكل
إطار في CI (<code dir="ltr">net10.0</code> و<code dir="ltr">net11.0</code>).

## قواعد تبعيات الحزم

مخطط التبعيات ضحل عمداً:

| الطبقة | يجوز أن تشير إلى | يجب ألا تشير إلى |
|---|---|---|
| <code dir="ltr">Abstractions</code> | BCL | Core، والمزوّدات، والتكاملات، وتنفيذات التخزين |
| <code dir="ltr">Core</code> | <code dir="ltr">Abstractions</code> | المزوّدات، والتكاملات، وEF Core |
| <code dir="ltr">Storage</code> | BCL | Core، والمزوّدات |
| تنفيذات المزوّدين | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Storage</code> لنماذج الملفات؛ و<code dir="ltr">Database.Abstractions</code> لمورّدي قواعد البيانات | Core |
| مخزن EF Core | <code dir="ltr">Abstractions</code>، وEF Core، وتجريدات التكوين | Core |
| حزم التبادل | <code dir="ltr">Interchange.Abstractions</code> | أنواع وقت التشغيل في المزوّد أو Core |
| مزوّدات الترجمة | <code dir="ltr">Abstractions</code>، وبنية HTTP/Options | Core |
| تكامل Avalonia | <code dir="ltr">Abstractions</code>، وAvalonia، و<code dir="ltr">Microsoft.Extensions.DependencyInjection.Abstractions</code> | Core |
| تكامل MAUI | <code dir="ltr">Abstractions</code>، و<code dir="ltr">Microsoft.Maui.Core</code>، و<code dir="ltr">Microsoft.Maui.Essentials</code>، و<code dir="ltr">Microsoft.Extensions.DependencyInjection.Abstractions</code> | Core |
| تكامل WinUI | <code dir="ltr">Abstractions</code>، و<code dir="ltr">Microsoft.WindowsAppSDK</code>، و<code dir="ltr">Microsoft.Extensions.DependencyInjection.Abstractions</code> | Core |
| تكامل تحقق MVC | <code dir="ltr">Core</code> (حافة <code dir="ltr">Integration → Core</code> الموثّقة الوحيدة)، وإطار ASP.NET Core المشترك | التكاملات الأخرى |
| Blazor وJavaScript والتشخيص وRequestLocalization | <code dir="ltr">Abstractions</code>، ومراجع ASP.NET Core أو إطار الاستضافة عند اللزوم | Core |
| تكامل DataAnnotations | BCL، وDataAnnotations، و<code dir="ltr">Microsoft.Extensions.Localization.Abstractions</code> | Core، وAbstractions |
| الأدوات | BCL، وRoslyn | مكتبات وقت التشغيل (أداة التكافؤ مستقلة) |

يسرد الجدول الحواف بين المكتبات؛ وحزم BCL و<code dir="ltr">Microsoft.Extensions.*</code>
المعتمدة حواف إضافية يوثّقها كل مشروع.

قاعدة **المزوّد الواحد بالضبط** قاعدة وقت تشغيل، لا قاعدة مرجع حزمة. ويجوز للمضيف أن يشير إلى عدة
حزم مزوّدين، لكن عليه تسجيل مزوّد واحد بالضبط. ويحل المحدد النسخ ويرمي عندما يوجد أكثر من واحد.

<div dir="ltr">

```csharp
services.AddLocalization();
services.AddJsonLocalization(options => options.FilePath = "Localization/en.json");
// Do not add a second provider registration.
```

</div>

التكاملات التي تحتاج أنواع ASP.NET Core تعلن <code dir="ltr">FrameworkReference</code> على
<code dir="ltr">Microsoft.AspNetCore.App</code>. ويعتمد تكامل التشخيص فقط على
<code dir="ltr">Microsoft.Extensions.Diagnostics.HealthChecks</code> وAbstractions، لذا يعمل في أي
مضيف <code dir="ltr">Microsoft.Extensions.Hosting</code>.

لا توجد تبعية على حزمة OpenTelemetry. يستخدم الرصد أسماء <code dir="ltr">ActivitySource</code>
و<code dir="ltr">Meter</code> في BCL؛ ويربط المستهلكون جهة SDK بأنفسهم.

## قائمة تحقق التوافق للتغيير

- هل يعدّل التغيير توقيعاً عاماً؟ إن كان كذلك، فحدّث خط الأساس عمداً.
- هل يغيّر خيارات مزوّد أو افتراضياته أو تحققه؟ حدّث مرجع المزوّد والاختبارات؛ فالخيارات لقطات
  إنشاء.
- هل يغيّر معرّف تشخيص أو اسم نشاط أو اسم مقياس أو وسماً؟ تعامل معه كتغيير كاسر للوحات المعلومات
  والكبت.
- هل يضيف مرجع مشروع يخالف الجدول أعلاه؟ ستفشل الاختبارات المعمارية.
- هل يضيف حزمة غير آمنة للتقليص؟ وسمها بصدق أو استبعد المشروع من عقد AOT.
- هل يغيّر صيغة الكتالوج أو شكل التبادل؟ ارفع إصدار مخطط التقرير أو سجّل الترحيل؛ فرحلات الذهاب
  والعودة المتطابقة بايتياً ثابت اختباري.
- هل يضيف حزمة مزوّد جديدة؟ أضفها إلى جرد الحزم وخطوط أساس الواجهة ومرجع المزوّدين.

## مثال عملي: تغيير واجهة متعمَّد

إعادة تسمية دالة عامة تغيير كاسر. حدّث مواضع النداء والاختبارات، وشغّل اختبارات الحل لرؤية فرق
خط الأساس، وأعد توليد ملفات <code dir="ltr">.Api.txt</code> المتأثرة بـ
<code dir="ltr">JENINNET_API_REGEN=1</code>، وراجع الفرق كجزء من التغيير. أضف إدخال سجل تغييرات
**Breaking:** مع ملاحظة ترحيل، ثم شغّل التحقق من الحزم وحل المستهلك. اختبار خط الأساس هو البوابة
التي تمنع أن تكون إعادة التسمية حادثة.

## مآزق

**مأزق:** إضافة <code dir="ltr">&lt;Version&gt;</code> لكل مشروع. الإصدار على مستوى الحل مركزي؛
ويرفض الاختبار المعماري التجاوز.

**مأزق:** إضافة حافة تبعية جديدة دون تحديث النموذج المعماري. يفشل اختبار المخطط، ويصبح جرد الحزم
مضللاً.

**مأزق:** افتراض أن مخزن EF آمن لـ AOT. إنه مستبعد صراحةً؛ استخدم مزوّداً غير EF لعمليات نشر
trimmed أو Native AOT.

## إلى أين بعد ذلك

- [ملاحظات الإصدار](09-release-notes.md) لحالة النشر الحالية ونطاق 1.0 المخطط.
- [القيود المعروفة](10-known-limitations.md) لحدود الدعم خلف هذه الوعود.
- [AOT والتقليص](../aot-and-trimming.md) للعقد المفروض ومستهلك اختبار الدخان.
- [خريطة الحزم](../appendix/02-package-map.md) لجرد الحزم الكامل.
- [مرجع الأدوات](05-tooling-reference.md) لسلوك حزم وقت البناء.
- [الترحيل من Microsoft.Extensions.Localization](08-migration-from-ms-extensions.md)
  لترحيل مدفوع بالتوافق.

</div>
