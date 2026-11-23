<div dir="rtl">

# خريطة الحزم (package map)

يوجد 44 مشروعاً قابلاً للتغليف في المستودع. ولكل واحد قاعدة الهوية نفسها: اسم المشروع يساوي اسم
التجميعة يساوي مساحة الأسماء الجذرية يساوي معرّف الحزمة، وإصدار الحل
<code dir="ltr">1.0.0-preview.3</code>. ونشرت خلاصة <code dir="ltr">preview.1</code> خمساً وثلاثين حزمة؛
وقدّمت خلاصة <code dir="ltr">preview.2</code> مجموعة الحزم الـ44 بإضافة حزمة الاكتشاف ومزوّدي INI وTOML
ومزوّد DeepL وعقد موصّل TMS مع تنفيذي Crowdin وWeblate ومغلّفات التخزين المؤقت. وتشحن
<code dir="ltr">preview.3</code> الحزم الـ44 نفسها بتبعيات محدَّثة وأعمال التثبيت؛ والحزم المحلية تأتي من
<code dir="ltr">dotnet pack</code> ومجلد
<code dir="ltr">artifacts/</code>. وتشحن كل حزمة عابرة للمنصات أصلي
<code dir="ltr">lib/net10.0</code> و<code dir="ltr">lib/net11.0</code> من الكود نفسه؛ ويبقى تكاملا
MAUI وWinUI على <code dir="ltr">net10</code> مع مضيفيهما.

قاعدتان تحددان معظم اختيارات الحزم:

1. **اختر أي مزوّد رسائل، واحداً بالضبط.** يحل خط الأنابيب
   <code dir="ltr">IInnerMessageProvider</code> واحداً. وإضافة مزوّد ثانٍ خطأ تكوين، لا طبقة
   ارتداد.
2. **الثلاثي الأساسي هو أساس العقود.** <code dir="ltr">Abstractions</code> و
   <code dir="ltr">Core</code> و<code dir="ltr">Storage</code> هي الأساس المشترك. ويحتاج خط
   أنابيب رسائل عامل إلى <code dir="ltr">Core</code> مع مزوّد واحد؛ ويأتي
   <code dir="ltr">Abstractions</code> تبعياً، ويأتي <code dir="ltr">Storage</code> تبعياً عبر
   مزوّدي JSON وXML وPO. أشر إلى <code dir="ltr">Storage</code> مباشرةً فقط عندما تنفّذ مزوّداً أو
   أداة.

تسرد المجموعات أدناه التبعيات المباشرة فقط، بالأسماء المختصرة بعد أول ذكر؛ فمخطط الحزم صغير، لكن
NuGet يحل الإغلاق التعدي.

## حزمة الاكتشاف (1)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization/README.md) | حزمة اكتشاف للثلاثي الأساسي؛ ولا تحمل كوداً ولا تسجّل مزوّداً. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Core</code> و<code dir="ltr">Storage</code>. |

## العقود وخط الأنابيب (3)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md) | عقود المزوّدين والمُعرِّبات والمنسّقات وقواعد الجمع والتشخيص والمخزن. | لا شيء (BCL (Base Class Library) فقط). |
| [<code dir="ltr">Jeninnet.Localization.Storage</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md) | نماذج بيانات الرسائل المرجعية التي يتشاركها مزوّدو الملفات وقواعد البيانات. | لا شيء (BCL فقط). |
| [<code dir="ltr">Jeninnet.Localization.Core</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) | خط أنابيب وقت التشغيل، والمُعرِّب والمنسّق الافتراضيان، ومحدد المزوّد، وجسر المخزن، وتسجيل DI (Dependency Injection). | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Microsoft.Extensions.Localization</code> و<code dir="ltr">Caching.Abstractions</code> و<code dir="ltr">AspNetCore.Http.Abstractions</code> و<code dir="ltr">Logging.Abstractions</code>. |

## مزوّدو الرسائل (16)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Providers.Memory</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md) | يخدم قاموساً مسطحاً واحداً غير قابل للتغيير؛ الخيار الحتمي للاختبارات والنماذج وتراكيب WASM (WebAssembly). | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Json</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md) | يخدم كتالوجات JSON مسطحة، ملفاً لكل ثقافة في التسجيل متعدد الثقافات، مع إعادة تحميل ساخنة اختيارية. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Storage</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Ini</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Ini/README.md) | يخدم كتالوجات INI مسطحة (<code dir="ltr">key = value</code> مع أقسام <code dir="ltr">[Section]</code> اختيارية)، ملفاً لكل ثقافة، مع إعادة تحميل ساخنة اختيارية. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Storage</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Toml</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Toml/README.md) | يخدم كتالوجات TOML مسطحة (<code dir="ltr">key = "value"</code> بقيم نصية فقط، مع أقسام <code dir="ltr">[Section]</code> اختيارية)، ملفاً لكل ثقافة، مع إعادة تحميل ساخنة اختيارية. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Storage</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Caching</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Caching/README.md) | مغلّفات تخزين مؤقت بمدة صلاحية (داخل الذاكرة وموزّعة) لأي مزوّد رسائل، مع دمج الطلبات المتزامنة والتحميل المسبق والتحديث وأحداث الاكتساب. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Caching.Abstractions</code> و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Options</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Xml</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md) | يخدم مستندات رسائل XML، ملفاً لكل ثقافة في التسجيل متعدد الثقافات، مع إعادة تحميل ساخنة اختيارية. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Storage</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.ResX</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) | يخدم موارد <code dir="ltr">.resx</code> المضمّنة عبر <code dir="ltr">ResourceManager</code> ببحث ثقافة دقيق. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Satellite</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md) | يخدم موارد محايدة مترجمة وتجميعات satellite، بما فيها حزم مؤلَّفة في مكان آخر. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Po</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md) | يخدم نص gettext PO (Portable Object) أو ثنائيات MO (Machine Object)، بما فيها السياقات وجداول الجمع لكل صيغة. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Storage</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Pseudo</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md) | ينتج ثقافة اصطناعية معرَّبة زائفاً لكشف الفائض والتسريبات. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/README.md) | نوع أساس وخيارات مشتركة لمزوّدي قواعد بيانات ADO.NET؛ ولا تشير إليها التطبيقات مباشرةً. | <code dir="ltr">Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.SQLite</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SQLite/README.md) | يقرأ جدول <code dir="ltr">LocalizationMessages</code> من SQLite. | <code dir="ltr">Database.Abstractions</code>؛ و<code dir="ltr">Microsoft.Data.Sqlite</code> و<code dir="ltr">DependencyInjection</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.SqlServer</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SqlServer/README.md) | يقرأ جدول <code dir="ltr">LocalizationMessages</code> من SQL Server. | <code dir="ltr">Database.Abstractions</code>؛ و<code dir="ltr">Microsoft.Data.SqlClient</code> و<code dir="ltr">DependencyInjection</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.MySql</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.MySql/README.md) | يقرأ جدول <code dir="ltr">LocalizationMessages</code> من MySQL. | <code dir="ltr">Database.Abstractions</code>؛ و<code dir="ltr">MySql.Data</code> و<code dir="ltr">DependencyInjection</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.PostgreSQL</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | يقرأ جدول <code dir="ltr">LocalizationMessages</code> من PostgreSQL. | <code dir="ltr">Database.Abstractions</code>؛ و<code dir="ltr">Npgsql</code> و<code dir="ltr">DependencyInjection</code>. |
| [<code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | مخزن مجموعات الموارد فوق EF Core وSQL Server، مع التدقيق والتاريخ والملاحظات والمزامنة. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Microsoft.EntityFrameworkCore.SqlServer</code> و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Configuration.Abstractions</code>. |

حزمة EF Core مخزن، لا مزوّد رسائل. تنفّذ
<code dir="ltr">ILocalizationStore</code> و<code dir="ltr">IResourceAdminStore</code> و
<code dir="ltr">IResourceHistoryStore</code>؛ ولا تنفّذ
<code dir="ltr">IInnerMessageProvider</code>. وتقرأ مزوّدات ADO.NET الأربعة
<code dir="ltr">LocalizationMessages</code>؛ ويملك مخزن EF الجداول
<code dir="ltr">LocalizedStrings</code> و<code dir="ltr">AuditRecords</code> و
<code dir="ltr">StringVersions</code> و<code dir="ltr">CatalogState</code> و
<code dir="ltr">StringNotes</code>. وهذه مخططات منفصلة.

## مزوّدو الترجمة (3)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Translation.Azure</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md) | مزوّد Azure Translator v3 مع ذاكرة TTL (Time To Live)، ودمج الطلبات المتزامنة، والتجميع، والتحميل المسبق، والتحديث، والمسرد، وذاكرة الترجمة، وإعادة المحاولة، والقاطع، وتحديد المعدل. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Microsoft.Extensions.Http</code>. |
| [<code dir="ltr">Jeninnet.Localization.Translation.DeepL</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.DeepL/README.md) | مزوّد DeepL API v2 مع ذاكرة TTL، ودمج الطلبات المتزامنة، والتجميع، والتحميل المسبق، والتحديث، والمسرد، وذاكرة الترجمة، وإعادة المحاولة، والقاطع، وتحديد المعدل. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Microsoft.Extensions.Http</code>. |
| [<code dir="ltr">Jeninnet.Localization.Translation.Google</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Google/README.md) | مزوّد Google Translate v2 بالمسار الأدنى أحادي المفتاح، وبلا ذاكرة مزوّد أو تجميع. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Microsoft.Extensions.Http</code>. |

مزوّد الترجمة هو مزوّد الرسائل الوحيد، لا طبقة خلف مزوّد. ونص مصدره هو مفتاح البحث.

## موصّلات TMS (3)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Translation.Tms</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms/README.md) | عقد موصّل TMS (نظام إدارة الترجمة) المحايد للمورّد: سحب ودفع ومعاينة جافة وتبليغ تعارضات بمفردات <code dir="ltr">CatalogKeyStatus</code> يشترك فيها كل مورّد. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Interchange.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Translation.Tms.Crowdin</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms.Crowdin/README.md) | موصّل Crowdin: سحب اللقطات المترجمة ودفع كتالوجات PO/XLIFF مع معاينات جافة. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Translation.Tms</code>؛ و<code dir="ltr">Interchange.Po</code> و<code dir="ltr">Interchange.Xliff</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Microsoft.Extensions.Http</code> و<code dir="ltr">Microsoft.Extensions.Options</code>. |
| [<code dir="ltr">Jeninnet.Localization.Translation.Tms.Weblate</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms.Weblate/README.md) | موصّل Weblate: سحب اللقطات المترجمة ودفع كتالوجات PO/XLIFF مع معاينات جافة. | <code dir="ltr">Abstractions</code> و<code dir="ltr">Translation.Tms</code>؛ و<code dir="ltr">Interchange.Po</code> و<code dir="ltr">Interchange.Xliff</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Microsoft.Extensions.Http</code> و<code dir="ltr">Microsoft.Extensions.Options</code>. |

الموصّلات أدوات سير عمل، لا مزوّدات رسائل: تنقل الكتالوجات بين المستودع والمورّد، وتطبيق
الدفع المعتمد يبقى التركيبة المحكومة للمنادي.

## التكاملات (9)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Integrations.Avalonia</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Avalonia/README.md) | خدمة الثقافة وحفظها في ملف لمضيفات Avalonia، مع مساعد اتجاه RTL. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Avalonia</code> و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Maui</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Maui/README.md) | خدمة الثقافة وحفظها في تفضيلات MAUI لمضيفات MAUI المحمولة وسطح المكتب. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Microsoft.Maui.Core</code> و<code dir="ltr">Microsoft.Maui.Essentials</code> و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.WinUI</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.WinUI/README.md) | خدمة الثقافة وحفظها في بيانات تطبيق Windows لمضيفات WinUI 3. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">Microsoft.WindowsAppSDK</code> و<code dir="ltr">DependencyInjection.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.DataAnnotations</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md) | سمات تحقق معرَّبة تُحل عبر <code dir="ltr">IStringLocalizer&lt;&gt;</code>. | <code dir="ltr">Microsoft.Extensions.Localization.Abstractions</code>؛ بلا تبعية Jeninnet. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Mvc.Validation</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Mvc.Validation/README.md) | تحقق MVC وبيانات العرض الوصفية، ومُعرِّبات آمنة لـ HTML، وتعريب العرض، ومساعد الوسم <code dir="ltr">&lt;loc&gt;</code>. | <code dir="ltr">Core</code>؛ ومرجع إطار <code dir="ltr">Microsoft.AspNetCore.App</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.RequestLocalization</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.RequestLocalization/README.md) | يبني خيارات تعريب الطلب من المخزن ويضيّق النتائج إلى الثقافات المخدومة. | <code dir="ltr">Abstractions</code>؛ ومرجع إطار <code dir="ltr">Microsoft.AspNetCore.App</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Blazor</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md) | إدارة ثقافة محدودة الدائرة وساكنة النطاق، مع حارس مزوّد WASM. | <code dir="ltr">Abstractions</code>؛ ومرجع إطار <code dir="ltr">Microsoft.AspNetCore.App</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.JavaScript</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.JavaScript/README.md) | نقطة نهاية مؤصَّلة وتصدير ساكن لموارد JavaScript بقائمة سماح وETags. | <code dir="ltr">Abstractions</code>؛ ومرجع إطار <code dir="ltr">Microsoft.AspNetCore.App</code>. |
| [<code dir="ltr">Jeninnet.Localization.Integrations.Diagnostics</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md) | فحوص صحة للمزوّد والمخزن؛ والتتبعات والمقاييس في Abstractions. | <code dir="ltr">Abstractions</code>؛ و<code dir="ltr">DependencyInjection.Abstractions</code> و<code dir="ltr">Diagnostics.HealthChecks</code>. |

تكامل <code dir="ltr">Integrations.DataAnnotations</code> مفصول عمداً عن مخطط وقت تشغيل Jeninnet.
ويستخدم تكاملا MVC وBlazor مراجع إطار بدلاً من تثبيت حزم للإطار المشترك في ASP.NET Core.

## التبادل (5)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Interchange.Abstractions</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md) | مفردات حالة مشتركة ومرمّز ملفات كتالوج لعائلة التبادل. | لا شيء (BCL فقط). |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Csv</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md) | تصدير CSV بمعيار RFC 4180 واستيراد تحقق-ثم-تطبيق لسير عمل الجداول. | <code dir="ltr">Interchange.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Po</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md) | تصدير واستيراد نص PO لسير عمل gettext/Poedit. | <code dir="ltr">Interchange.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Interchange.ResX</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md) | تصدير واستيراد ResX 2.0 المرجعي لمحرري ResX. | <code dir="ltr">Interchange.Abstractions</code>. |
| [<code dir="ltr">Jeninnet.Localization.Interchange.Xliff</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md) | تصدير واستيراد XLIFF (XML Localization Interchange File Format) 2.0 لمورّدي الترجمة. | <code dir="ltr">Interchange.Abstractions</code>. |

حزم التبادل أدوات. وهي ليست مزوّدات وقت تشغيل، واستيراد ملف لا يغيّر كتالوجاً يعمل ما لم يطبّق
المضيف النتيجة.

## الأدوات وأصول وقت البناء (3)

| الحزمة | الغرض | التبعيات المباشرة |
|---|---|---|
| [<code dir="ltr">Jeninnet.Localization.Generators.Keys</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) | مولّد Roslyn تزايدي يصدر ثوابت مفاتيح مكتوبة بقوة من كتالوجات <code dir="ltr">*.loc.json</code> و<code dir="ltr">*.resx</code> و<code dir="ltr">*.po</code> وXLIFF 2.0. | <code dir="ltr">Microsoft.CodeAnalysis.CSharp</code> (خاص)، و<code dir="ltr">System.Collections.Immutable</code>؛ و<code dir="ltr">netstandard2.0</code>. |
| [<code dir="ltr">Jeninnet.Localization.Analyzers.Localizability</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) | محلل Roslyn وإصلاح كود (<code dir="ltr">JLNLOC001</code>) يوسم السلاسل المضمّنة الموجهة للمستخدم. | <code dir="ltr">Microsoft.CodeAnalysis.CSharp</code> (خاص)، و<code dir="ltr">System.Collections.Immutable</code>؛ و<code dir="ltr">netstandard2.0</code>. |
| [<code dir="ltr">Jeninnet.Localization.Tools.Keys</code>](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md) | أداة .NET <code dir="ltr">jeninnet-localization-keys</code> لاستخراج المفاتيح وتكافؤ الكتالوج. | <code dir="ltr">Microsoft.CodeAnalysis.CSharp</code>؛ وأداة <code dir="ltr">net10.0</code>. |

المولّد والمحلل حزمتا <code dir="ltr">DevelopmentDependency</code> مع تعطيل مخرجات البناء. وهما
يُشحنان كأصول محلل، لا كمكتبات وقت تشغيل، وتحمل حزمة المحلل المحلل وإصلاح الكود معاً تحت
<code dir="ltr">analyzers/dotnet/cs</code>. والمشروع الشقيق
<code dir="ltr">Jeninnet.Localization.Analyzers.Localizability.CodeFixes</code> غير قابل للتغليف؛
وينتقل ملف DLL داخل حزمة المحلل.

## الملخص

| المجموعة | العدد | أشر إليها عندما |
|---|---|---|
| حزمة الاكتشاف | 1 | تريد الثلاثي الأساسي بمرجع واحد؛ وتضيف مزوّداً واحداً بالضبط. |
| العقود وخط الأنابيب | 3 | تبدأ تطبيقاً أو تنفّذ نقطة امتداد. |
| مزوّدو الرسائل | 17 | تحتاج مصدراً واحداً بالضبط لقوالب الرسائل (ويمكن تغليفه بمغلّف تخزين مؤقت أو تركيبه مرتّباً)؛ وأضف حزمة المخزن فقط لإدارة مجموعات الموارد. |
| مزوّدو الترجمة | 3 | الترجمة الآلية وقت الطلب قرار منتج صريح. |
| موصّلات TMS | 3 | تدير الترجمات في TMS (Crowdin للسحابة وWeblate ذاتي الاستضافة) بسحب ودفع ومعاينات جافة. |
| التكاملات | 9 | تحتاج Avalonia أو MAUI أو WinUI أو MVC أو ثقافات الطلب أو Blazor أو تسليم JavaScript أو فحوص صحة. |
| التبادل | 5 | تتبادل الكتالوجات مع المترجمين أو أدوات أخرى. |
| الأدوات وأصول وقت البناء | 3 | تريد مفاتيح وقت الترجمة أو اقتراحات تأليف أو بوابة تكافؤ في CI. |
| **الإجمالي** | **44** | |

لأصغر إعداد عامل، أشر إلى <code dir="ltr">Core</code> ومزوّد واحد. وأضف
<code dir="ltr">Storage</code> صراحةً فقط إذا نفّذت مزوّداً أو أداة، وأضف المخزن فقط إذا احتجت
<code dir="ltr">IStringLocalizer</code> أو مجموعات موارد محكومة، وأضف التكاملات واحداً تلو الآخر
حسب حاجة المضيف. راجع
[التثبيت وأول تسجيل](../01-getting-started/02-installation.md) لجولة اختيار الحزم، و
[النشر](../04-operations/01-deployment.md) لمكان تشغيل كل حزمة.

</div>
