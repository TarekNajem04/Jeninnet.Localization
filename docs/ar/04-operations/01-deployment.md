<div dir="rtl">

# النشر (deployment)

نشر تطبيق معرَّب هو ثلاث عمليات نشر على الأقل: التطبيق وحزمه، ومحتوى الكتالوج أو مخطط قاعدة
البيانات، والتكوين والأسرار التي تخبر العملية بمكان الكتالوج. وتُبقي المكتبة هذه الشواغل منفصلة.
يغطي هذا الفصل موضع الحزم، وسكربتات المخطط الملتزم بها، وEF Core مقابل SQL، والبذر، وشواغل تعدد
النسخ، وتدفئة الذاكرة المؤقتة، والأسرار، والتراجع.

## ما الذي يذهب إلى أين

يحتاج مضيف الخادم خط الأنابيب الأساسي، ومزوّد رسائل واحداً بالضبط، والتكاملات التي يستخدمها فقط.

| الهدف | الحزم | ملاحظات |
|---|---|---|
| تطبيق خادم أو سطح مكتب | <code dir="ltr">Jeninnet.Localization.Core</code> مع مزوّد واحد | يوفّر Core المُعرِّب والمنسّق والمحدد وجسر المخزن؛ ويوفّر المزوّد الكتالوج. |
| مخزن مُدار | <code dir="ltr">Jeninnet.Localization.Providers.Database.EntityFrameworkCore</code> | <code dir="ltr">ILocalizationStore</code>، لا مزوّد رسائل؛ ولا يغذّي <code dir="ltr">IMessageLocalizer</code>. |
| ASP.NET Core MVC | <code dir="ltr">Integrations.Mvc.Validation</code> | بيانات التحقق والعرض الوصفية، والعروض، ومساعد الوسم <code dir="ltr">&lt;loc&gt;</code>. |
| ثقافات الطلب | <code dir="ltr">Integrations.RequestLocalization</code> | يملأ وسيط الإطار من الثقافات المخدومة في المخزن. |
| Blazor | <code dir="ltr">Integrations.Blazor</code> | إدارة ثقافة محدودة الدائرة أو ساكنة النطاق، مع حارس WASM (WebAssembly). |
| عميل المتصفح | <code dir="ltr">Integrations.JavaScript</code> أو تصديره الساكن | قوالب من قائمة سماح فقط؛ ولا اتصال قاعدة بيانات أبداً. |

المتصفح حد ثقة منفصل. وترفض <code dir="ltr">AddBlazorWasmLocalization</code> مزوّداً لا ينفّذ
<code dir="ltr">IWasmCompatibleMessageProvider</code>؛ ومزوّد الذاكرة هو التنفيذ المدمج. ولا
يستطيع بناء المتصفح الوصول مباشرة إلى كتالوج SQL Server. صدّر المفاتيح المعتمدة عبر تكامل
JavaScript أو انقل البحث إلى الخادم.

مخزن EF Core هو حزمة وقت التشغيل الوحيدة المستبعدة عمداً من عقد Native AOT (Ahead-of-Time)/التقليص. ويجب أن
تستخدم عمليات النشر المقلمة بالكامل أو Native AOT مزوّداً غير EF. راجع
[AOT والتقليص](../aot-and-trimming.md).

## تخطيط المحتوى

تنسخ <code dir="ltr">dotnet publish</code> التجميعات، لا الملفات التي يعرفها المشروع كمحتوى مصدر
فقط. ولمزوّدات الملفات، اجعل الكتالوج جزءاً من مخرجات النشر صراحةً:

<div dir="ltr">

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
  <None Update="Localization\*.xml" CopyToOutputDirectory="PreserveNewest" />
  <None Update="Localization\*.po" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

</div>

حل المسارات من <code dir="ltr">AppContext.BaseDirectory</code>، لا من دليل العمل الحالي. فالمسار
الذي يعمل تحت <code dir="ltr">dotnet run</code> قد يفشل تحت خدمة أو نقطة دخول حاوية أو مهمة
مجدولة. وتتوقع تحميلات الاصطلاح
<code dir="ltr">{baseDirectory}/Localization/{culture}.json</code> (أو <code dir="ltr">.xml</code>/
<code dir="ltr">.po</code>). وتسجّل
<code dir="ltr">AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"])</code>
مزوّداً واحداً لكل ثقافة وتلفّهم في
<code dir="ltr">CultureAwareMessageProvider</code> واحد؛ ويتبع XML وPO الشكل نفسه. أما تسجيل
ثقافة واحدة فيقرأ خريطة مسطحة واحدة ويتجاهل وسيطة الثقافة.

محتوى ResX وsatellite مضمَّن أو يُنشر كحزم satellite. وكلاهما يحتاج اسم مورد البيان الأساسي
والتجميعة المالكة:

<div dir="ltr">

```csharp
services.AddResXLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
services.AddSatelliteLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
```

</div>

لا يمكن التحقق من الاسم الأساسي الخاطئ عند التسجيل؛ بل يظهر عند أول بحث كـ
<code dir="ltr">MissingManifestResourceException</code>. اختبر بحثاً واحداً لكل مزوّد أثناء
التحقق من الإصدار. وSQLite قاعدة بيانات مسنودة بملف: تعامل مع مسارها كحالة، وضعها على وحدة
تخزين قابلة للكتابة، وانسخها احتياطياً، ولا تخبزها في صورة للقراءة فقط. أما SQL Server وMySQL
وPostgreSQL فتنتمي إلى إصدار قاعدة البيانات، لا إلى صورة التطبيق.

## مخطط قاعدة البيانات لمزوّدات الرسائل

تقرأ مزوّدات رسائل قواعد البيانات الأربعة الجدول المرجعي:

<div dir="ltr">

```text
LocalizationMessages
    Id
    MessageKey
    Culture
    Template
    UNIQUE (MessageKey, Culture)
```

</div>

القيمة الافتراضية لـ <code dir="ltr">DatabaseLocalizationOptions.TableName</code> هي
<code dir="ltr">LocalizationMessages</code>، ويقبل معرّف SQL صالحاً حتى ثلاثة مقاطع تفصلها نقاط،
لذا <code dir="ltr">dbo.LocalizationMessages</code> صالح والنص العشوائي يُرفض عند تعيين الخيار.

ترحيل إعادة التسمية الملتزم به هو
[0001_localization_messages.sql](../../database/0001_localization_messages.sql). إنه **ترحيل
إعادة تسمية محروس**، لا سكربت إنشاء:

1. إذا وُجد <code dir="ltr">dbo.ValidationMessages</code> ولم يوجد
   <code dir="ltr">dbo.LocalizationMessages</code>، فأعد تسمية الجدول.
2. إذا كان ما زال يحوي <code dir="ltr">Key</code> ولا يحوي
   <code dir="ltr">MessageKey</code>، فأعد تسمية العمود.
3. إذا كان ما زال يحوي <code dir="ltr">MessageTemplate</code> ولا يحوي
   <code dir="ltr">Template</code>، فأعد تسمية ذلك العمود.

يحمل SQL Server القيود والهويات عبر <code dir="ltr">sp_rename</code>، فتبقى الصفوف محفوظة،
وتجعل الحُرّاس السكربت آمناً لإعادة التشغيل على قاعدة بيانات رُحّلت بالفعل. وقاعدة البيانات
الجديدة لا تحوي أي جدول، لذا شغّل سكربت الإنشاء المطابق من
[دليل قاعدة البيانات](../../database/README.md):
[SQL Server](../../database/schema/0001_create_localization_messages.sqlserver.sql)، أو
[PostgreSQL](../../database/schema/0001_create_localization_messages.postgresql.sql)، أو
[MySQL](../../database/schema/0001_create_localization_messages.mysql.sql)، أو
[SQLite](../../database/schema/0001_create_localization_messages.sqlite.sql). وكل سكربت
متطابق الأثر، وينشئ الأعمدة الأربعة المرجعية والزوج الفريد
<code dir="ltr">(MessageKey, Culture)</code>، ويترك جدولاً قائماً دون مساس. ثم ابذره. وطبّق تغيير
المخطط كخطوة قاعدة بيانات مُراجَعة، لا من بدء التطبيق، بعد نسخة احتياطية أو نقطة استعادة.

### إعادة التسمية تغيير كاسر

التطبيق القديم يسمّي <code dir="ltr">ValidationMessages</code> و<code dir="ltr">Key</code> و
<code dir="ltr">MessageTemplate</code>؛ والتطبيق الجديد يسمّي
<code dir="ltr">LocalizationMessages</code> و<code dir="ltr">MessageKey</code> و
<code dir="ltr">Template</code>. والعملية القديمة التي لم تُعَد نشرها ستفشل استعلاماتها بعد
الترحيل الأمامي. انشر بهذا الترتيب:

1. أوقف الكتابة إلى جدول الرسائل أو ادخل وضع صيانة للقراءة فقط.
2. انسخ قاعدة البيانات احتياطياً وطبّق السكربت الأمامي.
3. انشر إصدار التطبيق الذي يستخدم الأسماء المرجعية والمزوّد الواحد نفسه.
4. تحقق من بحث واحد لكل ثقافة مخدومة ومن غياب متعمد واحد، ثم استأنف الكتابة.

يعكس [سكربت التراجع](../../database/0001_localization_messages.rollback.sql) إعادات التسمية
بالترتيب العكسي. إنه محروس ويحفظ الصفوف، لكنه يعيد الأسماء لا البيانات. وأعد التطبيق بالترتيب
العكسي في النافذة نفسها؛ فالمخطط القديم لا معنى له إلا للتطبيق القديم.

تستطيع نسخة قراءة (read replica) خدمة عمليات البحث فقط بعد أن تطبّق تغيير المخطط نفسه وتلحق
بالبذر. ويصبح تأخر النسخة تأخر محتوى: قد يوجد مفتاح على الأساسي ويغيب عن النسخة لفترة قصيرة.
ويعامل المزوّد ذلك كغياب، فيرتد المُعرِّب أو يعيد <code dir="ltr">null</code>. وإذا كان ذلك غير
مقبول، فوجّه قراءات التعريب إلى الأساسي أو أضف ذاكرة مؤقتة بنافذة قدم محدودة. ولا توجّه مخزن
الإدارة إلى نسخة أبداً؛ فالتعديلات المحكومة ورفع الإصدارات وسجلات التدقيق ويوميات إعادة التشغيل
تحتاج أساسياً قابلاً للكتابة.

## ترحيلات EF Core مقابل سكربتات SQL

هناك مخططان، وهما ليسا الشيء نفسه. تملك مزوّدات ADO.NET الجدول
<code dir="ltr">LocalizationMessages</code> بالأعمدة <code dir="ltr">Id</code> و
<code dir="ltr">MessageKey</code> و<code dir="ltr">Culture</code> و
<code dir="ltr">Template</code>. ويملك مخزن EF Core الجداول
<code dir="ltr">LocalizedStrings</code> و<code dir="ltr">AuditRecords</code> و
<code dir="ltr">StringVersions</code> و<code dir="ltr">CatalogState</code> و
<code dir="ltr">StringNotes</code>.

لا يشحن المستودع ترحيلات EF Core. ويذكر README مخزن EF أن إدارة المخطط مسؤولية المستهلك. وقاعدة
بيانات جديدة أُنشئت بـ <code dir="ltr">EnsureCreated</code> تتضمن النموذج الحالي، بما فيه جداول
المساءلة والملاحظات؛ و<code dir="ltr">EnsureCreated</code> بلا أثر عندما تحوي قاعدة البيانات أي
جدول، لذا تحتاج قاعدة بيانات مشتركة خطوة مخطط صريحة. راجع
[مخطط مخزن EF Core](06-ef-core-store-schema.md) للجداول وفحوص التوفير وقواعد التعايش. لا تفترض
أن سكربت <code dir="ltr">LocalizationMessages</code> الملتزم به يرقّي مخزن EF؛ فهو لا يمس تلك
الجداول. ويمكن أن يتعايش المخططان في قاعدة بيانات واحدة، لكنهما غير متزامنين. وإذا استخدم مستهلك
<code dir="ltr">IMessageLocalizer</code> و<code dir="ltr">IStringLocalizer</code> معاً، فابذر
وتحقق من المسارين أو استخدم مساراً واحداً.

## البذر

لا تحتاج مزوّدات الملفات بذراً وقت التشغيل: فالكتالوجات تُشحن مع الإصدار. ولا تملك مزوّدات رسائل
قواعد البيانات باذراً مدمجاً؛ استخدم إدراجاً أو تحديثاً متطابق الأثر (upsert) أو مهمة ETL تكتب
<code dir="ltr">MessageKey</code> و<code dir="ltr">Culture</code> و
<code dir="ltr">Template</code>، وأبقِ قيد الزوج الفريد كي لا تنشئ إعادة المحاولة صفوفاً مكررة.

يستخدم مخزن EF Core الدالة <code dir="ltr">ResourceSynchronizer</code> مع قيم
<code dir="ltr">ResourceDeclaration</code>. عاين أولاً، ثم طبّق بخيارات مطابقة إلا
<code dir="ltr">DryRun</code>:

<div dir="ltr">

```csharp
var declarations = new[] {
    new ResourceDeclaration("Bookstore", "App.Title", "Bookstore"),
    new ResourceDeclaration("Bookstore", "Cart.Items", "You have {count} items.")
};

var synchronizer = new ResourceSynchronizer(admin);
var preview = await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "deploy"
    });

if (preview.Conflicts > 0) {
    throw new InvalidOperationException("Catalog seed conflicts: " + preview.Conflicts);
}

await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], Actor = "deploy"
    });
```

</div>

تحدث الإضافات افتراضياً؛ وتغيير قيمة ثقافة افتراضية موجودة يتطلب
<code dir="ltr">AllowUpdates</code>. وثقافات الهدف محافظة، وتنشئ
<code dir="ltr">CreateMissingTargetCultures</code> عناصر نائبة فارغة تكبت الارتداد. والتشغيل
الثاني متطابق الأثر ولا ينتج تعديلات أو إدخالات تدقيق. تعامل مع التقرير كدليل إصدار، لا كقيمة
منطقية.

## تدفئة الذاكرة المؤقتة والتكوين وترتيب الإصدار

تحمّل مزوّدات الملفات كسولاً عند أول بحث؛ وتقرأ الطلبات اللاحقة جدولاً غير قابل للتغيير. ولتدفئتها،
استكشف مفتاحاً واحداً لكل ثقافة مخدومة بعد بناء المضيف. ولإجبار تحميل ملف وإظهار كتالوج مشوّه
فوراً، حل المزوّد المحدد ونادِ <code dir="ltr">RefreshAsync</code>؛ فهو يرمي عند الفشل. والتحميل
الكسول الفاشل لا يُخزَّن مؤقتاً، لذا قد يعيد بحث لاحق محاولته. ولا تملك مزوّدات رسائل قواعد
البيانات ذاكرة رسائل مدمجة؛ وتدفئتها هي ذاكرة قاعدة البيانات المؤقتة ومجمع الاتصالات. ومزخرفا
التخزين المؤقت للمخزن والموزع كسولان أيضاً. وتدعم ترجمة Azure الدالة
<code dir="ltr">PreloadAsync(keys, culture, cancellationToken)</code> عندما يكون التخزين المؤقت
مفعّلاً؛ ولا يملك Google ذاكرة مزوّد. وتدفّئ فحوص الصحة خط الأنابيب فقط عند تكوين مفتاح مسبار
صريح.

خيارات المزوّد لقطات إنشاء. إعادة التحميل تعيد اكتساب المصدر؛ ولا تقرأ كائن الخيارات مرة أخرى،
لذا خطط لإعادة تشغيل لتغييرات التكوين. استخدم .NET User Secrets لتطوير ASP.NET Core المحلي،
ومخزن أسرار المنصة أو متغيرات البيئة في الإنتاج. لا تضع المفاتيح في
<code dir="ltr">appsettings.json</code>، ولا تسجّل كائن الخيارات: فخيارات الترجمة تحمل مفتاح API
كخاصية عادية. ثبّت نقاط نهاية الترجمة على المضيف المتوقع واستخدم HTTPS؛ فالمهاجم القادر على تغيير
التكوين يستطيع توجيه المزوّد إلى مضيف يتلقى المفتاح.

ترتيب الإصدار:

1. جمّد تعديلات الكتالوج وسجّل إصدار الكتالوج الحالي.
2. انسخ قاعدة البيانات احتياطياً، أو أرشف دليل المحتوى الدقيق لمزوّد ملفات.
3. طبّق تغييرات المخطط بسكربت مُراجَع. ولمخزن EF، طبّق الترحيل الذي يملكه المستهلك وشغّل
   <code dir="ltr">VerifyHistoryAsync</code> بعده.
4. انشر التطبيق والمحتوى معاً؛ وأكّد وجود ملف كل ثقافة مكوَّنة.
5. ابدأ نسخة واحدة. حل مفتاحاً في كل ثقافة مخدومة، واختبر غياباً متعمداً، وشغّل فحوص صحة
   المزوّد/المخزن.
6. انشر النسخ المتبقية. كتالوجات قواعد البيانات تتشارك الصفوف؛ ويجب أن يكون محتوى الملفات متطابقاً
   على كل نسخة.
7. راقب الغيابات وخطوات الارتداد وزمن قاعدة البيانات وأخطاء الترجمة لنافذة إصدار واحدة. وعند
   التراجع، اعكس التطبيق والمخطط معاً؛ فقراءات عند إصدار تعيد إنتاج الحالة التاريخية لكنها ليست
   آلية تراجع تلقائي.

## مآزق

**مأزق:** التعامل مع SQL الملتزم به كسكربت إنشاء. إنه يعيد تسمية جدول قديم وأعمدته فقط؛ وقاعدة
بيانات جديدة تحتاج إنشاء الجدول المرجعي أولاً.

**مأزق:** حذف قاعدة نسخ المحتوى. تسجيل JSON وXML وPO يرمي
<code dir="ltr">FileNotFoundException</code>، وهو مبكر لكن فقط إذا بدأ المضيف بمسار المحتوى ذلك.

**مأزق:** افتراض أن المخزن وجدول الرسائل متزامنان. إنهما نطاقان منفصلان؛ وحّدهما عمداً أو استخدم
مساراً واحداً.

## إلى أين بعد ذلك

- [الأمان](02-security.md) و[الأداء](03-performance.md).
- [استكشاف الأخطاء](04-troubleshooting.md).
- [مجموعات الموارد والمخزن](../02-guides/06-resource-sets-and-store.md).

</div>
