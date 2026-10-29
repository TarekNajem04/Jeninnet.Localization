<div dir="rtl">

# استكشاف الأخطاء (troubleshooting)

تظهر إخفاقات التعريب عادةً كأحد أربعة أعراض: نص مفقود، أو نص بلغة خاطئة، أو فشل التطبيق عند بدء
التشغيل، أو فشل طلب أثناء البحث عن سلسلة. والمهمة الأولى هي تحديد الطبقة التي أنتجت العرض.
يعيد <code dir="ltr">IMessageLocalizer</code> القيمة <code dir="ltr">null</code> عند الغياب ولا
يختلق علامة أبداً. ويعيد جسر المخزن <code dir="ltr">[MISSING:key]</code> مع
<code dir="ltr">ResourceNotFound = true</code>. واستثناء المزوّد ليس أياً منهما. ابدأ بتحديد أيها
تنظر إليه، ثم اتبع القسم المناسب.

## ترجمة مفقودة أو [MISSING:key]

| السبب المحتمل | التشخيص | الإصلاح |
|---|---|---|
| خطأ إملائي في المفتاح، أو اختلاف حالة، أو مسافة بيضاء عابرة | ابحث في الكتالوجات عن المفتاح الدقيق؛ فالمزوّدات المسنودة بقاموس تقارن ترتيبياً وحساسة للحالة. | صحح المفتاح أو إدخال الكتالوج. |
| ثقافة خاطئة أو مسار ارتداد خاطئ | سجّل الثقافتين المطلوبة والمحيطة؛ وقارن بأسماء ملفات الكتالوج. | مرّر الثقافة المقصودة صراحةً أو أصلح اختيار ثقافة الطلب. |
| مزوّد غير مسجّل، أو مزوّد خاطئ مسجّل | حلّ <code dir="ltr">IMessageProvider</code>؛ وبصفر مزوّدين يعيد كل بحث <code dir="ltr">null</code>. | سجّل مزوّداً واحداً بالضبط. |
| ملف غير منشور | افحص المسار المشتق من <code dir="ltr">AppContext.BaseDirectory</code>؛ وتتحقق مزوّدات الملفات من الوجود عند التسجيل لـ JSON وXML وPO. | أضف قاعدة نسخ المحتوى وأعد النشر. |
| بحث مخزن لا خط أنابيب رسائل | <code dir="ltr">[MISSING:key]</code> يأتي من <code dir="ltr">LocalizedStringEntry.Missing</code>؛ وخط أنابيب الرسائل يعيد <code dir="ltr">null</code>. | أصلح مجموعة الموارد أو الثقافة أو المفتاح؛ أو استخدم <code dir="ltr">IMessageLocalizer</code> إن كان ذلك مقصوداً. |
| قيمة مخزّنة فارغة | يسمح المخزن المحكوم بقيمة فارغة، والقيمة الفارغة إصابة تكبت الارتداد. | خزّن القالب الحقيقي أو احذف الإدخال الفارغ. |

فعّل مصب المفاتيح المفقودة إذا احتجت دليلاً إنتاجياً. ويسجّل
<code dir="ltr">LoggerMissingKeySink</code> تحذيراً منظماً واحداً لكل غياب مع المفتاح والثقافة
وموضع النداء؛ ولا يُستشار المصب إلا بعد أن تغيب سلسلة الارتداد كاملة. ولتدقيق أوسع، شغّل
<code dir="ltr">jeninnet-localization-keys</code>؛ فهو يبلّغ عن المفاتيح المفقودة والمخدومة
بالارتداد وغير المستخدمة، بمواقع الملف/السطر.

لبحث واحد، أعد إنتاجه خارج الواجهة:

<div dir="ltr">

```csharp
var value = await localizer.LocalizeAsync("Cart.Items", parameters, new CultureInfo("ar"));
Console.WriteLine(value is null ? "(miss)" : value);
```

</div>

إذا أعاد هذا قيمة ولم تعرضها الصفحة، فالصفحة تستخدم مُعرِّباً مختلفاً أو ثقافة مختلفة أو جسر
المخزن بدلاً من خط أنابيب الرسائل.

## ثقافة خاطئة

تتحكم <code dir="ltr">CurrentCulture</code> في تنسيق الأرقام والتواريخ؛ وتختار
<code dir="ltr">CurrentUICulture</code> موارد واجهة المستخدم. وتستخدم التحميلات المريحة
<code dir="ltr">CurrentUICulture</code>. ومهمة خلفية تغيّر
<code dir="ltr">CurrentCulture</code> دون <code dir="ltr">CurrentUICulture</code> ستظل تحل اللغة
القديمة.

أسباب شائعة أخرى:

- تُلتقط الثقافة الافتراضية عند إنشاء <code dir="ltr">DefaultMessageLocalizer</code>. وتعديل
  <code dir="ltr">LocalizationOptions.DefaultCulture</code> بعد ذلك لا يغيّر مُعرِّباً قائماً.
- يلتقط <code dir="ltr">IStringLocalizer</code> طويل العمر ثقافة واجهة المستخدم عند الإنشاء.
  أنشئ مُعرِّباً جديداً عند تغيّر الثقافة.
- في ASP.NET Core، تأتي ثقافات الطلب من الوسيط. وبلا جسر تعريب الطلب، لا تجعل ملفات مزوّد عربية
  اللغة <code dir="ltr">ar</code> ثقافة مخدومة. وبلا مخزن وبلا ثقافات صريحة، تكون المجموعة
  المخدومة الثقافة الافتراضية فقط.
- اسم الثقافة المررَّر إلى المزوّد دقيق. فطلب <code dir="ltr">de</code> لا يقرأ
  <code dir="ltr">de-CH.json</code>؛ فمشي الآباء يصعد لا ينزل.

شخّص بتسجيل <code dir="ltr">CultureInfo.CurrentUICulture.Name</code>، والثقافة المررَّرة إلى
المُعرِّب، ووسم <code dir="ltr">localization.culture</code> على نشاط البحث. أصلح تكوين ثقافة
الطلب بدلاً من إضافة كتالوجات مكررة.

## مفاجآت الارتداد

يعمل الارتداد كما صُمم عندما:

- يعرض مفتاح ألماني مفقود الإنجليزية، لأن الثقافة الافتراضية هي الخطوة الأخيرة؛
- يُحل <code dir="ltr">de-CH</code> من <code dir="ltr">de</code>، لأن سلسلة الآباء تصعد؛
- يستعلم بحث الثقافة الثابتة عن الثقافة الافتراضية فقط، عندما تكون مكوَّنة؛
- تُستعلم الثقافة الافتراضية مرتين عند الغياب عندما تكون هي الثقافة المطلوبة أيضاً؛
- لا يقرأ طلب <code dir="ltr">de</code> الملف <code dir="ltr">de-CH.json</code>؛
- توقف قيمة هدف فارغة الارتداد لأنها إصابة.

الارتداد الناجح ليس تغطية ترجمة. استخدم أداة التكافؤ والتعريب الزائف واختبار مزوّد تسجيل لتأكيد
التسلسل الدقيق للثقافات التي تُستعلم عنها. لا تضف مزوّداً ثانياً "لسد الفجوات"؛ فمزوّدان يرميان.

## خطأ تسجيل عدة مزوّدين

يفشل الحل بـ <code dir="ltr">InvalidOperationException</code> تبدأ رسالته:

<div dir="ltr">

```text
Exactly one Localization Provider must be registered, but 2 Providers were resolved: ...
```

</div>

يسرد المحدد أسماء أنواع المزوّدين المحلولة. هذا ليس تعارض دمج؛ فتسلسل المزوّدين غير مدعوم. ومن
الأسباب الشائعة تسجيل JSON ومزوّد قاعدة بيانات "للارتداد"، أو تسجيل مزوّد ترجمة إلى جانب مزوّد
ملفات، أو تسجيل مزوّد مرتين عبر مساري تركيب.

شخّص بحل <code dir="ltr">IEnumerable&lt;IInnerMessageProvider&gt;</code> وفحص النسخ، أو بمراجعة
جذر التركيب بحثاً عن نداءات <code dir="ltr">Add*Localization</code>. أصلح باختيار نظام سجل واحد
ودمج المحتوى الآخر فيه. ومخزن EF Core ليس مزوّداً ولا يثير هذا الخطأ؛ وإضافته لا تمنح
<code dir="ltr">IMessageLocalizer</code> مصدراً.

## تعارضات المخزن والتزامن المتفائل

تبلّغ التعديلات المحكومة عن تعارضات النطاق المتوقعة بدلاً من الرمي. افحص
<code dir="ltr">ResourceAdminResult.Applied</code> و<code dir="ltr">Conflict.Kind</code>
(<code dir="ltr">AlreadyExists</code> أو <code dir="ltr">NotFound</code> أو
<code dir="ltr">TargetExists</code>). فإنشاء مفتاح موجود، أو تحديث مفتاح مفقود، أو إعادة تسمية
هدفها موجود بالفعل نتيجة عادية، لا استثناء.

تُرمى إخفاقات التحقق من عائلة <code dir="ltr">ArgumentException</code>: مفاتيح فارغة، أو ثقافات
مجهولة، أو قيمة null. ويرمي المخزن للقراءة فقط
<code dir="ltr">StoreReadOnlyException</code> قبل إنتاج نتيجة؛ استخدم
<code dir="ltr">store.RequireAdmin()</code> للدخول عبر فحص القدرة. وترمي قراءات التاريخ على مخزن
بلا تاريخ <code dir="ltr">StoreHistoryUnsupportedException</code>.

قد يستنفد تنازع الكتّاب خارج العملية إعادات عدّاد الإصدار ويرمي
<code dir="ltr">DbUpdateConcurrencyException</code> بهذه الرسالة:

<div dir="ltr">

```text
The catalog version counter could not be advanced because another writer is mutating the catalog
concurrently. Retry the mutation.
```

</div>

أعد محاولة التعديل كله، وأضف قيود قاعدة بيانات أو سلسل الكتابات خارجياً، وأبقِ CRUD (Create, Read, Update, Delete) الخام لـ
<code dir="ltr">ILocalizationResourceSet</code> خارج المسار المحكوم. فالـ CRUD الخام غير مؤصَّل
وغير مدقَّق وغير محكوم. وإذا بدا أثر ناقصاً، فافحص هل استخدمت أداة CRUD الخام.

## اتصال قاعدة البيانات

لا تلتقط مزوّدات قواعد البيانات استثناءات ADO.NET ولا تحوّلها إلى غيابات. سترى استثناء المورّد:
<code dir="ltr">SqlException</code> أو <code dir="ltr">MySqlException</code> أو
<code dir="ltr">NpgsqlException</code> أو <code dir="ltr">SqliteException</code>. وتظهر بهذه
الطريقة إخفاقات الاتصال وتسجيل الدخول وTLS والمهل والجداول المفقودة.

افحص بالترتيب:

1. سلسلة الاتصال وأذونات الحساب؛
2. وجود الجدول والأعمدة (<code dir="ltr">Id</code> و<code dir="ltr">MessageKey</code> و
   <code dir="ltr">Culture</code> و<code dir="ltr">Template</code>)؛
3. تطابق <code dir="ltr">TableName</code> المكوَّن مع المخطط الفعلي؛
4. إمكانية الوصول إلى قاعدة البيانات من مضيف التطبيق؛
5. أن ترحيل المخطط التزم فعلاً.

يفشل اسم جدول غير صالح أبكر، عند تعيين
<code dir="ltr">DatabaseLocalizationOptions.TableName</code>، بـ
<code dir="ltr">ArgumentException</code>. ويتحقق التسجيل من أن سلسلة الاتصال واسم الجدول غير
فارغين وبالشكل الصحيح؛ ولا يتصل عمداً. استخدم فحص صحة المزوّد أو استعلاماً قياسياً مباشراً
لاختبار الاتصال.

## مصادقة مزوّد الترجمة وحدود المعدل

يفشل التكوين المفقود عند التسجيل أو عند إنشاء المزوّد بـ
<code dir="ltr">InvalidOperationException</code>
(<code dir="ltr">Azure Translator ApiKey is not configured.</code> أو
<code dir="ltr">Google Translation ApiKey is not configured.</code> أو رسالة تحقق من نقطة
النهاية). هذه مشكلة تكوين، لا فشل بعيد.

تظهر الإخفاقات البعيدة كـ <code dir="ltr">HttpRequestException</code> مع الحالة في الرسالة:

<div dir="ltr">

```text
Azure Translator returned 401 Unauthorized.
Google Translation returned 429 Too Many Requests.
```

</div>

يعيد Azure محاولة إخفاقات الشبكة والمهل وHTTP 408 و429 و5xx حتى
<code dir="ltr">RetryMaxAttempts</code> بتراجع أسي. وعندما يكون القاطع مفتوحاً، يفشل النداء سريعاً
بـ <code dir="ltr">BrokenCircuitException</code> وبلا طلب HTTP. ويظهر انتهاء
<code dir="ltr">RequestTimeout</code> كـ <code dir="ltr">OperationCanceledException</code>. ولا
يملك Google إعادة محاولة أو ذاكرة أو تحميلاً مسبقاً أو دورة تحديث؛ وتُعاد 429 إلى المستدعي فوراً.

شخّص بحالة الاستجابة ومقاييس <code dir="ltr">localization.translation.*</code> وحدث
<code dir="ltr">AcquisitionFailed</code> في Azure. افحص المفتاح والمنطقة ونقطة النهاية وحصة
الاشتراك والشبكة الصادرة. ولمزوّد Google، تذكّر أن المفتاح في سلسلة الاستعلام؛ افحص سجلات البوابة
للتأكد من التنقيح قبل افتراض أن المفتاح غير صالح.

## إخفاقات أداة التكافؤ

تخرج <code dir="ltr">jeninnet-localization-keys</code> بالقيمة <code dir="ltr">0</code> للنظافة،
و<code dir="ltr">1</code> للنتائج، و<code dir="ltr">2</code> لأخطاء الاستخدام أو البيئة. والخروج
<code dir="ltr">1</code> بيانات قابلة للتنفيذ، لا انهيار. اقرأ مصفوفة
<code dir="ltr">findings</code> في تقرير JSON: الأنواع هي
<code dir="ltr">missing-key</code> و<code dir="ltr">unused-key</code> و
<code dir="ltr">template-drift</code> و<code dir="ltr">format-gap</code> و
<code dir="ltr">placeholder-drift</code> و<code dir="ltr">unresolved-reference</code> و
<code dir="ltr">definition-conflict</code> و<code dir="ltr">catalog-error</code>.

أسباب شائعة لنتائج غير متوقعة:

- مفتاح مشار إليه عبر الانعكاس أو شكل سلسلة لا تعرفه الأداة؛
- ملف كتالوج خارج الجذر المفحوص؛
- الثقافة الافتراضية خاطئة، فتُحسب حالات الارتداد مقابل سلسلة خاطئة؛
- ملفات مولّد <code dir="ltr">.loc.json</code> وُضعت في دليل وقت تشغيل مكتشَف تلقائياً، حيث يصبح
  جذر الملف رمز ثقافة؛
- فجوة مقصودة غير مستبعدة بـ <code dir="ltr">--exclude key</code> أو
  <code dir="ltr">--exclude culture:key</code>.

الخروج <code dir="ltr">2</code> يعني عادةً جذر <code dir="ltr">--catalog</code> أو
<code dir="ltr">--source</code> مفقوداً، أو مساراً غير موجود، أو ثقافة افتراضية مجهولة. أصلح
الاستدعاء قبل قراءة النتائج.

## مشكلات AOT والتقليص

التقليص وNative AOT (Ahead-of-Time) مغطيان في [AOT والتقليص](../aot-and-trimming.md). والمشكلات الشائعة:

- **مخزن EF Core في بناء AOT.** إنه مستبعد بحكم التصميم
  (<code dir="ltr">IsAotCompatible=false</code>). استخدم مزوّد قاعدة بيانات غير EF أو لا تقلّص
  ذلك المسار.
- **اكتشاف الموارد القائم على الانعكاس.** الدالتان
  <code dir="ltr">LocalizedResourceDiscovery.FromAssembly</code> و
  <code dir="ltr">FromType</code> موسومتان بـ
  <code dir="ltr">RequiresUnreferencedCode</code>؛ ويمرّر المستهلكون المقلمون قيم
  <code dir="ltr">ResourceDeclaration</code> صريحة إلى
  <code dir="ltr">ResourceSynchronizer</code>.
- **سمات التحقق ذات الأعضاء المنعكسة.** تنشر منشئات سمات التحقق المعرَّبة القائمة على النوع وسوم
  التقليص في BCL (Base Class Library). وتبقى منشئات الأعداد والسلاسل آمنة للتقليص.
- **الموارد المضمّنة.** تحقق من بقاء اسم المورد الأساسي بعد التقليص والنشر؛ فالاسم الأساسي المفقود
  ما زال يظهر كـ <code dir="ltr">MissingManifestResourceException</code> عند أول بحث.

شغّل مسار النشر والتنفيذ لمستهلك اختبار الدخان على معرّف وقت التشغيل الهدف. فالبناء الناجح
ليس تحقق AOT؛ والربط الأصلي وثنائي اختبار الدخان هما البوابة.

## مفاجآت التنسيق وإعادة التحميل

يرمي <code dir="ltr">IcuMessageFormatter</code> الاستثناء
<code dir="ltr">FormatException</code> للقوالب المشوّهة وأنواع الوسائط المجهولة ووسائط المحدد
المفقودة وعدم تطابق الأنواع وغياب فرع <code dir="ltr">other</code> والمحددات المكررة؛ وتتضمن
الرسالة موضع النمط. وتتطلب تراكيب الأرقام والتواريخ قيماً رقمية وتاريخية، لا سلاسل. والمنسّق
الافتراضي أكثر تسامحاً: يبقى العنصر النائب المجهول حرفياً، وتستخدم تحويلات
<code dir="ltr">ToString()</code> الثقافة المحيطة، لذا مرّر سلاسل منسّقة مسبقاً أو استخدم منسّق
ICU (International Components for Unicode) عندما تهم الأرقام الصحيحة ثقافياً.

إعادة التحميل الساخنة موجودة فقط لـ JSON وXML وPO، وفقط عند تفعيل
<code dir="ltr">WatchForChanges</code> بـ <code dir="ltr">ReloadDebounce</code> موجب. وتحتفظ
إعادة التحميل الفاشلة بآخر جدول سليم وترفع
<code dir="ltr">AcquisitionFailed</code>؛ وتحمّل <code dir="ltr">RefreshAsync</code> فوراً وترمي
عند الفشل. وخيارات المزوّد لقطات إنشاء، لذا
<code dir="ltr">services.Configure&lt;JsonLocalizationOptions&gt;(...)</code> لا تفعل شيئاً مع
<code dir="ltr">AddJsonLocalization</code>، وتغيير مسار أو سلسلة اتصال يتطلب إعادة تشغيل. ونداء
<code dir="ltr">AddLocalization()</code> و<code dir="ltr">AddLocalization(configure)</code> معاً
يرمي <code dir="ltr">InvalidOperationException</code>.

## إلى أين بعد ذلك

- [النشر](01-deployment.md) و[الأمان](02-security.md) و[الأداء](03-performance.md).
- [AOT والتقليص](../aot-and-trimming.md) لإخفاقات وقت النشر.
- [الثقافات والارتداد](../02-guides/03-cultures-and-fallback.md) لنموذج الارتداد.

</div>
