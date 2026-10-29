<div dir="rtl">

# التدقيق والتاريخ (audit and history)

كتالوج الإنتاج محتوى له تبعات. يغيّر أحدهم تسمية سعر أو إخلاء مسؤولية قانونياً أو رسالة تحقق، ثم
يسأل شخص لاحقاً: من غيّرها، ومتى، وما كان النص قبل ذلك؟ يجيب التدقيق والتاريخ عن هذه الأسئلة
لمسار الإدارة المحكوم.

يوجد سطحان:

- **حدث تدقيق حي** يُرفع مرة واحدة لكل صف معدَّل بعد تثبيت تعديل محكوم.
- **تاريخ محفوظ** بإصدارات كتالوج رتيبة، وقراءات عند إصدار (as-of-version)، وتحقق من سلسلة
  التجزئة (hash chain).

يشتركان في أنواع البيانات الوصفية لكنهما يخدمان غرضين مختلفين: المراقبة مقابل المساءلة.

## ما يُسجَّل

يحمل <code dir="ltr">ResourceAdminAuditEventArgs</code>:

| الخاصية | المعنى |
|---|---|
| <code dir="ltr">Timestamp</code> | وقت تثبيت التعديل (UTC). |
| <code dir="ltr">Actor</code> | الفاعل المقدَّم من المستدعي، وفارغ عند عدم التعريف. |
| <code dir="ltr">Operation</code> | العملية المحكومة، مثل <code dir="ltr">CreateEntry</code> أو <code dir="ltr">RenameKey</code>. |
| <code dir="ltr">ResourceSet</code> | اسم مجموعة الموارد (الاسم الجديد عند إعادة تسمية مجموعة). |
| <code dir="ltr">Culture</code> | ثقافة الصف المعدَّل؛ وفارغة لصفوف الملاحظات. |
| <code dir="ltr">Key</code> | المفتاح (المفتاح المصدر عند إعادة التسمية). |
| <code dir="ltr">NewKey</code> | هدف إعادة التسمية، أو <code dir="ltr">null</code> لغير إعادة التسمية. |
| <code dir="ltr">EntryId</code> | الهوية المحفوظة للصف. |
| <code dir="ltr">OldValue</code> | القالب قبل التعديل، أو <code dir="ltr">null</code> عند الإنشاء. |
| <code dir="ltr">NewValue</code> | القالب بعد التعديل، أو <code dir="ltr">null</code> عند الحذف وإعادة التسمية. |

يُرفع الحدث **مرة واحدة لكل صف معدَّل** بعد تثبيت التعديل. وتُرفع التعديلات متعددة الصفوف بترتيب
الثقافات الترتيبي. وتنتج إعادة تسمية مفتاح عبر ثقافتين إدخالين. وينتج تسلسل ملاحظة يمس صف ملاحظة
إدخالاً إضافياً بثقافة فارغة. ولا ترفع المعاينات التجريبية شيئاً لأنها لا تعدّل شيئاً. ولا ترفع
التعارضات شيئاً لأن شيئاً لم يتغير.

<code dir="ltr">ResourceAdminAuditContext</code> و
<code dir="ltr">ResourceAdminAuditRow</code> هما حاملتا البيانات الوصفية المشتركتان خلف الحدث
والسجل المحفوظ. وإذا بنيت أدوات حول التدقيق، فاعتمد على الخصائص بدلاً من إنشاء هذه الأنواع؛ فالمُنشئات
تفصيل تنفيذي تجمعه البنية.

## التاريخ المحفوظ

يمدّ <code dir="ltr">IResourceHistoryStore</code> الواجهة
<code dir="ltr">ILocalizationStore</code> بالأثر الدائم. وكالإدارة، القدرة ذات مستويين: تنفيذ
الواجهة هو التصريح على مستوى النوع؛ و<code dir="ltr">SupportsHistory</code> هو الجواب الصادق على
مستوى النسخة. استخدم الدالة المساعدة للدخول عبر فحص القدرة:

<div dir="ltr">

```csharp
using Jeninnet.Localization.Abstractions.Store;

var history = store.RequireHistory(); // throws StoreHistoryUnsupportedException when absent
```

</div>

الأعضاء هي:

| العضو | الغرض |
|---|---|
| <code dir="ltr">GetCurrentVersionAsync</code> | أحدث إصدار دفعة مطبَّق، أو <code dir="ltr">0</code> قبل أول تعديل محكوم. |
| <code dir="ltr">GetHistoryAsync(sinceVersion)</code> | السجلات ذات <code dir="ltr">Version &gt; sinceVersion</code>، مرتبة بتسلسل الإلحاق. و<code dir="ltr">0</code> يقرأ الأثر كله. |
| <code dir="ltr">GetStringsAsOfVersionAsync(resourceSet, culture, version)</code> | حالة الكتالوج في نهاية إصدار، مطابقة لإعادة التشغيل. و<code dir="ltr">0</code> هي الحالة الفارغة قبل التاريخ. |
| <code dir="ltr">VerifyHistoryAsync</code> | إعادة حساب سلسلة التجزئة والإبلاغ عن أول سجل غير صالح، إن وجد. |

يحمل <code dir="ltr">ResourceHistoryRecord</code> المحفوظ بيانات الحدث الوصفية إضافة إلى:

- **<code dir="ltr">Sequence</code>** — ترتيب الإلحاق الذي يعيّنه المخزن. إنه يتزايد بصرامة عبر
  الأثر ومستقل عن الإصدار؛ وقد تنتج دفعة واحدة سجلات كثيرة بإصدار واحد.
- **<code dir="ltr">Version</code>** — إصدار دفعة التعديل. الإصدارات رتيبة؛ وكل صف في دفعة واحدة
  يشارك الإصدار نفسه.
- **<code dir="ltr">OldValueHash</code> / <code dir="ltr">NewValueHash</code>** — تجزئات القالب
  قبل التعديل وبعده بعد التنقيح (redaction). للإنشاء تجزئة قديمة null؛ وللحذف وإعادة التسمية
  تجزئة جديدة null.

### التنقيح

لا يُحفظ نص القالب أبداً في سجل التدقيق. يحمل السجل تجزئة SHA-256 (Secure Hash Algorithm, 256-bit) بالنظام السادس عشري الصغير
لبايتات القالب بترميز UTF-8 (Unicode Transformation Format, 8-bit)، محسوبة بـ <code dir="ltr">ResourceValueHash.Compute</code>.
والتجزئة غير مملّحة عمداً كي يتجزأ القالب نفسه بالطريقة نفسها عبر السجلات والمخازن؛ والغرض هو
المساواة وكشف التغيير، لا السرية ضد القوة الغاشمة دون اتصال.

يوميات إعادة التشغيل (replay journal) مختلفة. يجب أن تعيد القراءات عند إصدار إنتاج الكتالوج، لذا
تحفظ اليوميات القيم الفعلية. تعامل مع اليوميات بفئة السرية نفسها التي تتعامل بها مع الكتالوج
الحي. وقاعدة التنقيح تنطبق على أثر التدقيق، لا على بيانات الكتالوج المؤصَّلة.

### إعادة التسمية والملاحظات

تحافظ إعادة تسمية مفتاح على هويات الصفوف وتسجّل المفتاح القديم في
<code dir="ltr">Key</code>، والهدف في <code dir="ltr">NewKey</code>، وتجزئة قالب المصدر في
<code dir="ltr">OldValueHash</code>. والتجزئة الجديدة null لأن إعادة التسمية ليست تغيير قيمة.
وتسجّل يوميات إعادة التشغيل شاهد قبر (tombstone) للمفتاح القديم وإدراجاً أو تحديثاً للمفتاح
الجديد، كي تعيد القراءات عند إصدار إنتاج إعادة التسمية.

ملاحظات المترجمين على مستوى المفتاح ولا تخص ثقافة، لذا تحمل سجلات تدقيقها
<code dir="ltr">Culture</code> فارغة. والملاحظات متتبَّعة للمساءلة كالقوالب: تغيير ملاحظة مباشر
وكل تغيير صف ملاحظة متسلسل يرفع إصدار الكتالوج ويضيف سجل تدقيق. والملاحظات ليست جزءاً من إعادة
تشغيل القوالب عند إصدار؛ فاليوميات تغطي القوالب فقط.

### التحقق من سلسلة التجزئة

يعيد <code dir="ltr">VerifyHistoryAsync</code> حساب السلسلة فوق السجلات المحفوظة ويعيد
<code dir="ltr">ResourceHistoryVerification</code>:

- <code dir="ltr">IsValid</code> — هل يتحقق الأثر كله.
- <code dir="ltr">VerifiedRecords</code> — عدد السجلات المتحققة قبل أول فشل، أو الإجمالي عند
  الصلاحية.
- <code dir="ltr">FirstInvalidSequence</code> — تسلسل أول سجل مكسور، أو <code dir="ltr">null</code>
  عند الصلاحية.

أي تعديل أو إدراج أو إزالة يكسر السلسلة ما لم تُعَد كتابة كل تجزئة لاحقة. والسلسلة **غير
مرساة** (unanchored): المهاجم القادر على إعادة الكتابة بالكامل لا يُكتشف إلا مقابل مرساة
خارجية، وهي خارج النطاق. تعامل مع التحقق كدليل عبث في ظل ضوابط تشغيلية عادية.

## استهلاك الحدث الحي

اشترك على المخزن الذي يدير التعديلات. الحدث مستقل عن التخزين ومتزامن.

<div dir="ltr">

```csharp
using Jeninnet.Localization.Abstractions.Store;

admin.Audited += (_, entry) => {
    logger.LogInformation(
        "Catalog {Operation} on {ResourceSet}/{Culture}/{Key} by {Actor} at {Timestamp}",
        entry.Operation,
        entry.ResourceSet,
        entry.Culture,
        entry.Key,
        entry.Actor,
        entry.Timestamp);
};
```

</div>

يعمل المعالج بعد تثبيت المعاملة. وإذا رمى، ينتشر الاستثناء رغم أن التعديل مثبَّت بالفعل: الكتالوج
هو مصدر الحقيقة، والمراقب الفاشل يجب ألا يتراجع عن تغيير مثبَّت. أبقِ المعالجات سريعة وغير رامية.

الحدث الحي ليس دائماً: لا إعادة تشغيل، ولا تسلسل، ولا إصدار. للمساءلة، احفظ المعلومات بنفسك أو
استخدم <code dir="ltr">IResourceHistoryStore</code>؛ لا تعامل سطر سجل كنظام تدقيق كامل.

## استهلاك التاريخ المحفوظ

<div dir="ltr">

```csharp
var history = store.RequireHistory();

var currentVersion = await history.GetCurrentVersionAsync(cancellationToken);
var records = await history.GetHistoryAsync(sinceVersion: 0, cancellationToken);

foreach (var record in records) {
    Console.WriteLine(
        $"{record.Sequence,6} v{record.Version,-4} {record.Operation,-18} " +
        $"{record.ResourceSet}/{record.Culture}/{record.Key} by {record.Actor}");
}

var asOf = await history.GetStringsAsOfVersionAsync(
    "Bookstore",
    "en",
    version: currentVersion,
    cancellationToken);
```

</div>

تستخدم <code dir="ltr">GetHistoryAsync(sinceVersion)</code> حداً حصرياً: احتفظ بأعلى إصدار
رأيته ومرّره لإلحاق السجلات الأحدث فقط.

<code dir="ltr">GetStringsAsOfVersionAsync</code> مطابقة لإعادة التشغيل: النتيجة تساوي حالة
الكتالوج بعد تطبيق كل دفعة تعديل حتى الإصدار المطلوب وشاملاً. وتعيد فقط الإدخالات التي كانت موجودة
عند ذلك الإصدار. والإصدار <code dir="ltr">0</code> هو الحالة الفارغة قبل التاريخ. استخدمها
للتحقيق في انحدار أو لتصدير كتالوج سابق، لا كآلية تراجع تلقائي.

ينتمي <code dir="ltr">VerifyHistoryAsync</code> إلى مهمة تشغيلية. شغّله وفق جدول، ونبّه عند نتيجة
غير صالحة، واحتفظ بالأدلة الخارجية التي تحتاجها إذا تطلب نموذج التهديد تثبيتاً.

## مثال عملي: تعديل منظَّم

يغيّر مسؤول <code dir="ltr">Legal.Disclaimer</code> من الإصدار A إلى B بالفاعل
<code dir="ltr">alice</code>.

1. تتحقق <code dir="ltr">UpdateEntryAsync</code> من المعرّفات والثقافة.
2. معاملة واحدة تحدّث الكتالوج، وتلحق إدراجاً أو تحديثاً في يوميات إعادة التشغيل، وتلحق سجلاً
   واحداً للتدقيق بسلسلة تجزئة يحمل التجزئتين، وتقدّم إصدار الكتالوج.
3. بعد التثبيت، يُطلق <code dir="ltr">Audited</code> مع <code dir="ltr">OldValue = A</code> و
   <code dir="ltr">NewValue = B</code> و<code dir="ltr">Actor = "alice"</code>.
4. تقرأ مهمة امتثال <code dir="ltr">GetHistoryAsync(lastSeenVersion)</code> وتحفظ السجل.
5. ينادي مراجع <code dir="ltr">GetStringsAsOfVersionAsync("Legal", "en", previousVersion)</code>
   لرؤية النص السابق؛ أما سجل التدقيق نفسه فيحمل تجزئات فقط.

## الحدود

- **المسار المحكوم فقط.** يغطي التاريخ تعديلات
  <code dir="ltr">IResourceAdminStore</code>. وCRUD الخام لـ
  <code dir="ltr">ILocalizationResourceSet</code> غير مؤصَّل ولا يترك سجلات تدقيق.
- **لا سجلات للتشغيل التجريبي.** التشغيل التجريبي لا يعدّل شيئاً وبالتالي لا يدقق شيئاً.
- **لا سجلات للتعارضات.** التعديل المرفوض لا يغيّر شيئاً ولا ينتج إدخال تدقيق؛ ويُعاد التعارض في
  <code dir="ltr">ResourceAdminResult</code>.
- **الملاحظات مجاورة للقوالب، لا إعادة تشغيل لها.** تغييرات الملاحظات تُدقَّق وتُؤصَّل، لكن
  القراءات عند إصدار تعيد القوالب فقط.
- **التاريخ قدرة مخزن.** لا تنفّذه مزوّدات الملفات والمخازن للقراءة فقط وترفض عبر
  <code dir="ltr">StoreHistoryUnsupportedException</code>. ويمرّر مزخرف المخزن المؤقت قراءات
  التاريخ إلى المخزن الداخلي ولا يخزّنها مؤقتاً أبداً.

## مآزق

**مأزق:** افتراض أن الحدث الحي أثر تدقيق كامل. إنه داخل العملية ومتزامن، بلا إعادة تشغيل وبلا
إصدار. استخدم التاريخ المحفوظ للمساءلة.

**مأزق:** ترك معالج تدقيق يرمي. التعديل مثبَّت بالفعل؛ ويتلقى المستدعي استثناءً لتغيير نجح. احتوِ
إخفاقات المراقب داخل المعالج.

**مأزق:** توقع <code dir="ltr">OldValue</code>/<code dir="ltr">NewValue</code> على
<code dir="ltr">ResourceHistoryRecord</code>. السجل المحفوظ يحمل تجزئات فقط. لاسترداد النص
التاريخي، استخدم قراءة عند إصدار مقابل يوميات إعادة التشغيل.

**مأزق:** افتراض أن سلسلة التجزئة تثبت الأصالة ضد مهاجم متميز. إنها تكشف العبث العادي؛ ويمكن
إعادة كتابة سلسلة غير مرساة بواسطة من يستطيع إعادة كتابة كل سجل لاحق.

**مأزق:** خلط CRUD (Create, Read, Update, Delete) الخام بالتعديلات المحكومة وتوقع أثر كامل. المسار الخام لا يترك سجلات، لذا
سيُظهر تحقيق لاحق فجوات لا تفسير لها.

## إلى أين بعد ذلك

- [مجموعات الموارد والمخزن](06-resource-sets-and-store.md) للعمليات المحكومة التي تنتج هذه
  السجلات.
- [الأمان](../04-operations/02-security.md) للسرية والتحكم في الوصول حول تخزين التدقيق.
- [واجهة التخزين](../03-reference/02-storage-api.md) لعقد التاريخ الكامل.

</div>
