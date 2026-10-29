# AOT and Trimming Compatibility

This document is the compatibility contract for Native AOT (Ahead-of-Time), trimming, and
single-file deployment. It records what the repository enforces, what is
deliberately excluded, and how the smoke consumer proves it.

## What is enforced

Every packable library project that targets a supported runtime TFM (`net8.0`, `net9.0`, or `net10.0`) builds with
`IsAotCompatible=true` (set in `Directory.Build.props`), which enables the trim
and AOT analyzers (`IL2026`/`IL2070`+/`IL3050` families) and stamps the
`IsTrimmable` assembly metadata. The repository treats warnings as errors, so a
trim-unsafe pattern cannot land unnoticed.

Reworked under  (analyzers proved each site):

- **Source-generated JSON everywhere a serializer is used** — the Azure and
  Google translation payloads, the JSON catalog provider, the Blazor JS-interop
  string encoding, the versioned parity report, and the distributed
  cache payloads all serialize/deserialize through `JsonSerializerContext`
  source generation. No reflection-based `JsonSerializer` call remains in
  `src/` or `integrations/`.
- **Missing-key call-site capture** uses `DiagnosticMethodInfo.Create` (the
  analyzer-recommended, AOT-safe replacement for `StackFrame.GetMethod`).
- **The JavaScript resource endpoint** maps a `RequestDelegate` instead of a
  minimal-API parameter-bound delegate, so route handling carries no
  reflection-based binding.
- **Localized validation attributes** resolve their message source through
  `IStringLocalizerFactory` (exactly what the framework's
  `IStringLocalizer<T>` wrapper does) instead of constructing
  `IStringLocalizer<TObject>` over the runtime validated type via
  `MakeGenericType`.

## Documented exclusions (intentionally not AOT-safe)

1. **`Jeninnet.Localization.Providers.Database.EntityFrameworkCore`** —
   excluded from the AOT contract (`IsAotCompatible=false`). EF Core itself
   states that it is not fully compatible with trimming or Native AOT
   (`DbContext` construction is annotated `RequiresUnreferencedCode`/
   `RequiresDynamicCode` by EF Core). Consumers running fully trimmed/AOT
   deployments should use the non-EF database providers. The exclusion is
   declared in the project file and restated in the provider README.
2. **`Jeninnet.Localization.Generators.Keys` and
   `Jeninnet.Localization.Analyzers.Localizability*`** — `netstandard2.0`
   build-time tooling assemblies; they never execute in the published
   application, so the runtime AOT contract does not apply to them.
3. **Annotated localizer-resolution surface of the validation attributes** —
   when trimming is used, `LocalizedCompareAttribute`,
   `LocalizedMaxLengthAttribute`, `LocalizedMinLengthAttribute`,
   `LocalizedRangeAttribute(Type, ...)` and
   `LocalizedCustomValidationAttribute(Type, ...)` are annotated with
   `RequiresUnreferencedCode`/`DynamicallyAccessedMembers` because the DataAnnotations
   base types resolve members by reflection (`Compare` by property name,
   `MaxLength`/`MinLength` by `Count` reflection, `Range` through
   `TypeDescriptor`, custom validators by method name). The annotations are
   honest propagation of the BCL (Base Class Library)'s own contracts: applying the attributes as
   source-level annotations stays warning-free, while dynamic construction over
   trimmed code paths reports the trim hazard. The integer/string-based
   constructors remain fully trim-safe.
4. **Attribute-based code-first resource discovery (`LocalizedResourceDiscovery`,
)** — `FromAssembly`/`FromType` reflect over `[LocalizedResource]` metadata
   that trimming may remove and are `RequiresUnreferencedCode`-annotated, so callers
   in trimmed/AOT deployments get the IL2026 propagation. The governed synchronizer
   itself (`ResourceSynchronizer`) carries no reflection; trimmed consumers pass
   explicit `ResourceDeclaration` values instead.

## Package posture matrix

`Compatible` means `IsAotCompatible=true` and the trim/AOT analyzers are enforced. `Compatible with
annotations` means the assembly is compatible except for the documented reflection-based
constructors, which propagate `RequiresUnreferencedCode`. `Excluded` means the project sets
`IsAotCompatible=false`. `Build-time` means the package runs in the compiler/tool host, not in the
published application.

| Package | Posture |
|---|---|
| [`Jeninnet.Localization.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md) | Compatible |
| [`Jeninnet.Localization.Core`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) | Compatible |
| [`Jeninnet.Localization.Storage`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Memory`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Json`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Xml`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.ResX`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Satellite`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Po`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Pseudo`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.SqlServer`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SqlServer/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.MySql`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.MySql/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.PostgreSQL`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.SQLite`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SQLite/README.md) | Compatible |
| [`Jeninnet.Localization.Providers.Database.EntityFrameworkCore`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | Excluded |
| [`Jeninnet.Localization.Translation.Azure`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md) | Compatible |
| [`Jeninnet.Localization.Translation.Google`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Google/README.md) | Compatible |
| [`Jeninnet.Localization.Integrations.DataAnnotations`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md) | Compatible with annotations |
| [`Jeninnet.Localization.Integrations.Mvc.Validation`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Mvc.Validation/README.md) | Compatible |
| [`Jeninnet.Localization.Integrations.Blazor`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md) | Compatible |
| [`Jeninnet.Localization.Integrations.JavaScript`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.JavaScript/README.md) | Compatible |
| [`Jeninnet.Localization.Integrations.Diagnostics`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md) | Compatible |
| [`Jeninnet.Localization.Integrations.RequestLocalization`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.RequestLocalization/README.md) | Compatible |
| [`Jeninnet.Localization.Interchange.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md) | Compatible |
| [`Jeninnet.Localization.Interchange.Csv`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md) | Compatible |
| [`Jeninnet.Localization.Interchange.Po`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md) | Compatible |
| [`Jeninnet.Localization.Interchange.ResX`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md) | Compatible |
| [`Jeninnet.Localization.Interchange.Xliff`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md) | Compatible |
| [`Jeninnet.Localization.Generators.Keys`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) | Build-time only |
| [`Jeninnet.Localization.Analyzers.Localizability`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) | Build-time only |
| [`Jeninnet.Localization.Tools.Keys`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md) | Build-time tool |

`Jeninnet.Localization.Analyzers.Localizability.CodeFixes` is not packable; it is a build-time
companion that ships inside the analyzer package and follows the same build-time-only posture. See
[its README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability.CodeFixes/README.md).

## Interpreted-only features under AOT

Per the  decision, the CLDR (Common Locale Data Repository) plural-rules engine is table-driven and
compiles no rules at runtime, so pluralization participates in AOT with no
exclusions. No feature of this library requires the JIT (Just-in-Time); the only
interpreted-only concerns are the member-reflection contracts listed above.

## Smoke consumer (CI shape)

`samples/Jeninnet.Localization.Smoke.Aot` publishes NativeAOT and exercises
localize/format/cache/translate (translation with a fake HTTP transport so the
source-generated JSON path runs). Command:

```powershell
dotnet publish samples/Jeninnet.Localization.Smoke.Aot -c Release -r <rid>
```

Toolchain gate: Native AOT requires MSVC on Windows or `clang` + `zlib1g-dev`
on Linux; CI runs the publish and execution on `ubuntu-latest` (the test-matrix
OS) after provisioning those packages. On a machine without the toolchain the
publish fails at the native link stage — that is the gate, not a silent
downgrade. The smoke binary itself is checked by running it (exit code `0`,
`SMOKE OK`).
