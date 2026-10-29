<div dir="rtl">

# التكافؤ وبوابات الجودة (parity and quality gates)

قد يكون بناء التعريب أخضر بينما المنتج غير مترجم. تثبت مفاتيح وقت الترجمة (compile-time keys) أن
مفتاحاً موجود في كتالوج مصرَّح به؛ لكنها لا تثبت أن كل ثقافة مخدومة تملكه، ولا أن الصيغ تتفق، ولا
أن مترجماً حافظ
على العناصر النائبة. التكافؤ هو الفحص الذي يسد تلك الفجوة.

أداة التكافؤ هي <code dir="ltr">jeninnet-localization-keys</code>. تستخرج مراجع المفاتيح من
المصدر، وتقرأ كل كتالوج تجده، وتتيح للمحلل الداخلي
<code dir="ltr">ParityAnalyzer</code> بناء مصفوفة اكتمال لكل ثقافة ونتائج حتمية بمواقع الملف
والسطر. وتخرج بقيمة غير صفرية عند الاختلاف.

## ما هو الفحص وما ليس هو

إنه فحص ساكن للتقارير فقط. لا ينشئ إدخالات كتالوج ولا يعيد تسميتها ولا يحذفها؛ ولا يطبّق
ترجمات؛ ولا يعدّل المصدر؛ ولا يتحقق من التنسيق وقت التشغيل. لكنه:

- يحصد تعريفات <code dir="ltr">const string</code> من صفوف <code dir="ltr">*Keys</code>، بما فيها
  الملفات المولّدة؛
- يجد النصوص الحرفية في مواضع المفاتيح مثل <code dir="ltr">localizer["Key"]</code> و
  <code dir="ltr">GetMessageTemplateAsync("Key", ...)</code> و
  <code dir="ltr">LocalizeAsync</code> و<code dir="ltr">LocalizePluralAsync</code> و
  <code dir="ltr">LocalizeWithContextAsync</code> و<code dir="ltr">GetString</code>؛
- يفحص عروض Razor نصياً؛
- يقرأ كتالوجات JSON وXML وResX وPO؛
- يقارن معرّفات العناصر النائبة عبر الثقافات؛
- يبلّغ عن الكتالوجات غير القابلة للاستخدام مع رقم سطر.

يُحلّل C# بصيغة Roslyn فقط؛ ولا يلزم تجميع، لذا يعمل الفحص حتى عندما لا ينجح بناء الحل. والمقايضة
أن مفتاحاً مركّباً وقت التشغيل، أو مشار إليه عبر غلاف غير معروف، قد يُبلَّغ عنه خطأً كمستخدم أو
مفقود. وقائمة الاستبعاد موجودة لتلك الحالات بالضبط.

## تشغيل الفحص

قبل نشر الأداة إلى موجز:

<div dir="ltr">

```bash
dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys -- \
  --catalog ./Localization --source ./src \
  --default-culture en --report artifacts/parity.json
```

</div>

بعد النشر، استبدل خطوة التشغيل بـ
<code dir="ltr">dotnet tool install --global Jeninnet.Localization.Tools.Keys</code> ثم
<code dir="ltr">jeninnet-localization-keys ...</code>.

| الخيار | المعنى |
|---|---|
| <code dir="ltr">--catalog &lt;dir&gt;</code> | جذر الكتالوج المفحوص تكرارياً. قابل للتكرار ومطلوب. |
| <code dir="ltr">--source &lt;dir&gt;</code> | جذر المصدر لملفات <code dir="ltr">.cs</code> و<code dir="ltr">.razor</code> و<code dir="ltr">.cshtml</code>. قابل للتكرار ومطلوب. |
| <code dir="ltr">--keys-file &lt;file&gt;</code> | ملف C# إضافي يُحلَّل لتعريفات ثوابت المفاتيح. قابل للتكرار. |
| <code dir="ltr">--exclude &lt;key&gt;</code> | استبعاد مفتاح في كل مكان، أو مفتاح محدد الثقافة بالشكل <code dir="ltr">ar:Key.Name</code>. قابل للتكرار. |
| <code dir="ltr">--default-culture &lt;tag&gt;</code> | الثقافة الافتراضية التي تغلق كل سلسلة ارتداد. الافتراضي <code dir="ltr">en</code>. |
| <code dir="ltr">--report &lt;path&gt;</code> | كتابة تقرير JSON المؤصَّل. |
| <code dir="ltr">--help</code> و<code dir="ltr">--version</code> | الاستخدام أو إصدار الأداة. |

رموز الخروج جزء من العقد: <code dir="ltr">0</code> نظيف، و<code dir="ltr">1</code> نتائج تكافؤ،
و<code dir="ltr">2</code> خطأ استخدام أو بيئة (دليل مفقود، أو جذر إدخال غير قابل للقراءة). ولا
تُصدر الأداة SARIF. ومخرجاتها الوحيدة القابلة للقراءة آلياً هي تقرير JSON المؤصَّل
(<code dir="ltr">schemaVersion: 2</code>)؛ ارفعه كأثر أو حوّله في خطوتك الخاصة. ومخرجات الطرفية
حتمية وتصلح لاختبار ذهبي.

## مصفوفة الاكتمال

لكل مفتاح مشار إليه ولكل ثقافة تملك كتالوجاً واحداً قابلاً للقراءة على الأقل، تسجّل المصفوفة حالة
واحدة:

- **<code dir="ltr">served</code>** — المفتاح موجود في كتالوجات تلك الثقافة نفسها.
- **<code dir="ltr">fallback</code>** — المفتاح غائب محلياً لكنه موجود في ثقافة أب أو في الثقافة
  الافتراضية.
- **<code dir="ltr">missing</code>** — المفتاح غائب من السلسلة كلها.

تطابق السلسلة وقت التشغيل: الثقافة المطلوبة، ثم الآباء، ثم تُستبعد الثقافة الثابتة، ثم الثقافة
الافتراضية. وخلية <code dir="ltr">fallback</code> ليست نتيجة
<code dir="ltr">missing-key</code>، لكنها تُظهر أن الثقافة غير مترجمة لذلك المفتاح. يحافظ
الارتداد على قابلية استخدام التطبيق؛ وتخبرك المصفوفة أين تكون التغطية اسمية فقط.

يحتاج ResX اصطلاح تسمية. يخدم الملف المحايد الثقافة الافتراضية؛ وتحمل الأسماء بنمط satellite
الثقافة بعد النقطة الأولى (<code dir="ltr">Messages.ar.resx</code> يخدم
<code dir="ltr">ar</code>). وتُدرج الأسماء غير المعروفة تحت
<code dir="ltr">ignoredFiles</code> بدلاً من تخطيها بصمت.

## أنواع النتائج

| النتيجة | المعنى | الإصلاح النموذجي |
|---|---|---|
| <code dir="ltr">missing-key</code> | مفتاح مشار إليه مفقود في ثقافة وسلسلة ارتدادها. | أضف الإدخال أو صحح المرجع. |
| <code dir="ltr">unused-key</code> | مفتاح كتالوج لا يُشار إليه في المصدر. | احذفه أو علّمه محجوزاً أو استبعده بسبب. |
| <code dir="ltr">template-drift</code> | ثقافة/مفتاح واحد يُخدم بقوالب مختلفة عبر الصيغ. | اجعل صيغة واحدة مرجعية وأعد توليد الأخرى. |
| <code dir="ltr">format-gap</code> | ثقافة تخدم المفتاح في صيغة لكن ليس في صيغة أخرى مخدومة. | أضف الإدخال أو أوقف خدمة تلك الصيغة. |
| <code dir="ltr">placeholder-drift</code> | مجموعة العناصر النائبة لثقافة تختلف عن مجموعة الثقافة المرجعية. | أعد العنصر النائب أو احذفه. |
| <code dir="ltr">unresolved-reference</code> | مرجع <code dir="ltr">*Keys.Name</code> بلا تعريف ثابت. | أضف الثابت أو صحح الخطأ الإملائي. |
| <code dir="ltr">definition-conflict</code> | اسم ثابت واحد معرَّف بقيم مختلفة. | أبقِ تعريفاً واحداً. |
| <code dir="ltr">catalog-error</code> | كتالوج لا يمكن قراءته أو تحليله. | صحح الصيغة؛ والنتيجة تحمل سطراً. |

تُرتَّب النتائج حسب النوع، ثم المفتاح، ثم الثقافة، ثم المسار، ثم السطر. وكل المسارات نسبية إلى دليل
العمل وبشرطات مائلة أمامية.

ماسح العناصر النائبة متوازن الأقواس ويفهم ICU (International Components for Unicode). يساهم <code dir="ltr">{name}</code> بـ
<code dir="ltr">name</code>؛ و<code dir="ltr">{0}</code> بـ <code dir="ltr">0</code>؛ و
<code dir="ltr">{count, plural, one {# item} other {# items}}</code> بـ
<code dir="ltr">count</code> فقط لأن العناصر القابلة للوضع داخل الفروع شرطية؛ و
<code dir="ltr">{amount, number, ::.00}</code> بـ <code dir="ltr">amount</code>. والأقواس غير
المتوازنة و<code dir="ltr">{}</code> نص حرفي، وتُتخطى القوالب الفارغة كغير مترجمة.

المجموعات ترتيبية، لذا إعادة ترتيب العناصر النائبة المسمّاة ليست انحرافاً. أما إسقاط
<code dir="ltr">{0}</code> أو <code dir="ltr">{1}</code> الموضعي أو إضافته فهو انحراف. وتأتي
المجموعة المرجعية من الثقافة الافتراضية عندما تخدم المفتاح محلياً، وإلا من أول ثقافة ترتيبياً
تخدمه. ولا تُقارن إلا الثقافات التي تخدم المفتاح محلياً؛ والارتداد شأن المصفوفة. وهذا يلتقط عيب
الترجمة الأكثر شيوعاً: جملة أعيدت صياغتها فقدت عنصراً نائباً، فيتركه وقت التشغيل حرفياً أو يرمي
منسّق ICU.

## المفاتيح غير المستخدمة والإيجابيات الكاذبة

<code dir="ltr">unused-key</code> صاخب عمداً. يرى المستخرج المراجع الساكنة، لا المفاتيح المبنية
بدمج السلاسل، أو المختارة بواسطة مستهلك خارجي، أو المشتركة مع مستودع آخر. اجعل الاستثناء صريحاً:

<div dir="ltr">

```bash
jeninnet-localization-keys --catalog ./Localization --source ./src \
  --exclude Demo.MissingEverywhere \
  --exclude ar:Demo.Partial \
  --exclude Public.Api.Key
```

</div>

يجب أن يكون لكل استبعاد سبب في المراجعة. وقائمة استبعاد طويلة تشير إلى مشكلة في استراتيجية
المفاتيح أو حدود الاستخراج، لا إلى عيب في الأداة.

ولـ <code dir="ltr">missing-key</code> حد ديناميكي مشابه. فدالة غلاف تأخذ مفتاحاً وتنادي
<code dir="ltr">IMessageLocalizer</code> داخلياً لا تُعرَف إلا إذا طابق اسمها قائمة المستخرج
المنسّقة. نادِ الدوال المرجعية مباشرة، أو أضف المرجع إلى ملف
<code dir="ltr">--keys-file</code>.

قد تكون نتيجة <code dir="ltr">placeholder-drift</code> إيجابية كاذبة عندما تحتاج لغتان فعلاً عناصر
نائبة مختلفة. هذا نادر ويجب مراجعته؛ استبعد المفتاح والثقافة المحددين مع تعليق بدلاً من استبعاد
المفتاح استبعاداً شاملاً.

## انضباط خط الأساس

1. **قبل الترجمة.** شغّل الفحص على كتالوج المصدر. أصلح مفاتيح المصدر المفقودة والمراجع غير
   المحلولة. هذا هو خط الأساس الذي يتلقاه المترجمون.
2. **بعد الاستيراد.** شغّله مرة أخرى. توقّع خلايا <code dir="ltr">served</code> جديدة ولا خلايا
   <code dir="ltr">missing</code> جديدة. راجع كل
   <code dir="ltr">placeholder-drift</code> و<code dir="ltr">StaleSource</code>.
3. **قبل الإصدار.** شغّله على ملفات الكتالوج الدقيقة التي ستُشحن. فالبناء الأخضر بكتالوج قديم
   ليس دليلاً.
4. **بعد إعادة تسمية المفاتيح.** يصبح المفتاح القديم غير مستخدم وقد يصبح الجديد مفقوداً. شغّل
   الفحص في مجموعة التغيير نفسها.

احتفظ بتقرير JSON كأثر CI. وإظهار الفرق بين تقريرين يبيّن ما غيّرته الجولة فعلاً. وإذا التزمت
بتقرير ذهبي، فثبّت إصدار الأداة وتوقع تغيّره مع الكتالوج؛ لا تستخدم لقطة طرفية ذهبية كبوابة وحيدة.

## ربط CI

<div dir="ltr">

```yaml
name: localization-parity

on:
  pull_request:
    paths:
      - "src/**"
      - "Localization/**"
      - "tests/**"

jobs:
  parity:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: actions/setup-dotnet@v6.0.0
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - name: Parity check
        run: >
          dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys --
          --catalog ./Localization
          --source ./src
          --default-culture en
          --report artifacts/parity.json
      - name: Upload parity report
        if: always()
        uses: actions/upload-artifact@v7.0.2
        with:
          name: parity-report
          path: artifacts/parity.json
```

</div>

تفشل الخطوة عندما تخرج الأداة بالقيمة <code dir="ltr">1</code>. أنشئ دليل التقرير أولاً. ولأداة
منشورة، استبدل خطوة التشغيل بأمر التثبيت و<code dir="ltr">jeninnet-localization-keys</code>.

## مثال عملي: اكتشاف استيراد سيئ

يغيّر استيراد ألماني <code dir="ltr">Checkout.Summary</code> من
<code dir="ltr">Your order contains {count} items.</code> إلى
<code dir="ltr">Ihre Bestellung enthält Artikel.</code> وقد غاب العنصر النائب.

<div dir="ltr">

```text
[placeholder-drift] Checkout.Summary [de] Localization/de.json:42
  Key 'Checkout.Summary' has placeholder drift for culture 'de':
  missing {count} (reference culture 'en' uses placeholders {count}).
```

</div>

يفشل البناء عند الخروج <code dir="ltr">1</code>. يعيد المراجع
<code dir="ltr">{count}</code>، ويعيد الاستيراد، فيكون التشغيل التالي نظيفاً. وبدون الفحص، كان
العيب سيظهر فقط عندما يفتح مستخدم ألماني صفحة الدفع فيُعرض
<code dir="ltr">{count}</code> حرفياً أو يرمي منسّق ICU.

## مآزق

**مأزق:** التعامل مع خلية <code dir="ltr">fallback</code> كمترجمة. إنها مغطاة بثقافة أخرى، لا
بالثقافة المطلوبة.

**مأزق:** استخدام <code dir="ltr">--exclude</code> لإسكات نتيجة دون تسجيل السبب. قائمة الاستبعاد
جزء من عقد الجودة ويجب مراجعتها كالكود.

**مأزق:** افتراض أن الأداة ترى المفاتيح المركّبة ديناميكياً. إنها لا تراها. استخدم الثوابت أو وسّع
سطح الاستخراج.

**مأزق:** توقع SARIF. تكتب الأداة مخرجات طرفية وتقرير JSON مؤصَّلاً فقط.

**مأزق:** تشغيل الفحص من دليل عمل مختلف عن الدليل المستخدم لإنتاج التقرير. المسارات نسبية إلى دليل
العمل؛ ويجب أن يستخدم CI والتشغيل المحلي الجذر نفسه.

**مأزق:** ترك كتالوج غير قابل للقراءة يمر لأن الطرفية اقتُطعت. تخرج
<code dir="ltr">catalog-error</code> بالقيمة <code dir="ltr">1</code>؛ اقرأ تقرير JSON عندما
تكون مخرجات الطرفية طويلة.

## إلى أين بعد ذلك

- [مرجع الأدوات](../03-reference/05-tooling-reference.md) لخيارات CLI ومخطط التقرير والمولّد
  والمحلل.
- [المفاتيح المكتوبة بقوة](05-strongly-typed-keys.md) للثوابت التي يحصدها المستخرج.
- [سير عمل المترجمين](11-translator-workflow.md) لرحلة الذهاب والعودة التي تحميها هذه البوابة.

</div>
