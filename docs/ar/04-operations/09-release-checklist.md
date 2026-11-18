<div dir="rtl">

# قائمة الإصدار (release checklist)

تضبط هذه القائمة كل إصدار معاينة. وهي تعكس معايير الخروج في ROADMAP وسير النشر
(<code dir="ltr">.github/workflows/nuget-publish.yml</code>)؛ ولا يُشحَن إصدار حتى تُفحَص كل خانة بدليل.

## الإصدار والوسم

- [ ] يساوي <code dir="ltr">&lt;Version&gt;</code> في <code dir="ltr">Directory.Build.props</code> إصدار الإصدار (مثل <code dir="ltr">1.0.0-preview.2</code>).
- [ ] الوسم <code dir="ltr">v&lt;Version&gt;</code> (مثل <code dir="ltr">v1.0.0-preview.2</code>) ويشير إلى التزام الإصدار.
- [ ] يحمل <code dir="ltr">CHANGELOG.md</code> بنداً للإصدار؛ وما يزال نطاق المعاينة يسمح بتعديل السطح.
- [ ] يوجد ملف ملاحظات في <code dir="ltr">.github/release-notes/v&lt;Version&gt;.md</code>؛ وإلا ولّد GitHub الملاحظات من نطاق الالتزامات.
- [ ] يُنشَأ إصدار GitHub مع <code dir="ltr">--prerelease</code> ما دام الإصدار يحمل لاحقة معاينة، فيوسم كل من nuget.org وGitHub بأنه معاينة.

## البناء والاختبار

- [ ] ينجح <code dir="ltr">dotnet build Jeninnet.Localization.slnx -c Release</code> بلا تحذيرات أو أخطاء.
- [ ] ينجح <code dir="ltr">dotnet build Jeninnet.Localization.Windows.slnx -c Release</code> على Windows.
- [ ] يجتاز <code dir="ltr">dotnet test Jeninnet.Localization.Tests -c Release -f net10.0</code> (مجموعة المكتبة، رجل net10).
- [ ] يجتاز <code dir="ltr">dotnet test Jeninnet.Localization.Tests -c Release -f net11.0</code> (مجموعة المكتبة، رجل net11).
- [ ] تجتاز المجموعات المرجعية: <code dir="ltr">Reference.Shared.Tests</code> و<code dir="ltr">Reference.Hosts.Tests</code>.
- [ ] تجتاز مجموعة المضيفات على Windows: <code dir="ltr">HostIntegrations.Tests</code> (MAUI/WinUI).
- [ ] <code dir="ltr">dotnet format --verify-no-changes</code> نظيف لحلّي المكتبة والمستهلكين.

## الواجهة والحزم

- [ ] يجتاز <code dir="ltr">PublicApiSurface_MatchesCommittedBaselines</code>؛ وأي تغيير مقصود في السطح جدّد خط الأساس <code dir="ltr">.Api.txt</code> في التغيير نفسه مع ملاحظة سجل.
- [ ] يجتاز فحص ApiCompat: وكل فرق سطح عن وسم الإصدار السابق مذكور في <code dir="ltr">.github/api-compat-acknowledged.txt</code>.
- [ ] ينجح <code dir="ltr">dotnet pack</code> مع <code dir="ltr">EnablePackageValidation=true</code> للحلّين.
- [ ] يساوي عدد الحزم المغلّفة عدد خطوط الأساس (44 قابلة للتغليف: 42 مشتركة بين المنصات مع MAUI وWinUI)؛ ويفحص سير العمل عدد خلاصة المستهلك.
- [ ] تحمل كل حزمة الأيقونة وREADME والرخصة وإشعارات الأطراف الثالثة والتزام SourceLink والوصف والوسوم (راجع <code dir="ltr">.nuspec</code> المغلّف).

## المستهلكون والدخان

- [ ] يستعيد حل المستهلكين ويبني ويختبر ويعمل من الخلاصة المحلية المغلّفة.
- [ ] دخان تثبيت نظيف: ثبّت حزمة الاكتشاف مع مزوّد واحد من الخلاصة في مضيف وحدة تحكم فارغ، وحُلّ مفتاحاً واحداً لكل ثقافة مخدومة مع غياب متعمَّد واحد.

## المستندات وAOT والأمن

- [ ] يُبنى DocFX؛ وتجتاز بوابات الروابط وادعاءات README والنص العربي وتغطية API.
- [ ] تغطي خريطة الحزم ومرجع المزوّدين وصفحة التوافق وملاحظات الإصدار هذا الإصدار.
- [ ] ينشر مستهلك الدخان NativeAOT ويعمل (<code dir="ltr">SMOKE OK</code>) مع تحليل تقليم نظيف.
- [ ] يجتاز <code dir="ltr">NuGetAudit</code>؛ ويغطي <code dir="ltr">THIRD-PARTY-NOTICES.md</code> كل تبعية مُعاد توزيعها.
- [ ] لا سر ولا بيانات شخصية في الكتالوجات أو السجلات أو التشخيص (راجع فصل الأمن).

## النشر

- [ ] عمل سير النشر على الوسم: التحقق من الإصدار، والاختبارات، والتغليف، وتحقق المستهلك، ورفع القطع (احتفاظ 30 يوماً)، والدفع بـ OIDC مع <code dir="ltr">--skip-duplicate</code>، وإصدار GitHub مع <code dir="ltr">.nupkg</code>/<code dir="ltr">.snupkg</code> المرفقة.
- [ ] اكتملت موافقة بيئة الإنتاج قبل خطوة الدفع.
- [ ] يسرد nuget.org الإصدار كمعاينة مع كل الحزم ظاهرة.

## أين تذهب بعد ذلك

- [التوافق والإصدارات](../03-reference/07-compatibility-and-versions.md) لموقف الدعم.
- [النشر](01-deployment.md) لترتيب الإصدار والتراجع المزدوج.
- [الأمن](02-security.md) لنموذج التهديد خلف فحوص الفحص والتنقيح.

</div>
