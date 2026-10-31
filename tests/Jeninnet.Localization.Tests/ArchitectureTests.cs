//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the architecture tests.
/// </summary>
[TestClass]
public class ArchitectureTests {
    /// <summary>
    /// The verified, loadable library assemblies that must participate in the runtime
    /// dependency graph. This explicit inventory makes the assembly-identity assertions
    /// non-vacuous: every listed assembly must load and match its expected identity.
    /// The removed empty <c>Jeninnet.Localization.Resources</c> project and the test assembly
    /// itself are intentionally not part of the library graph.
    /// </summary>
    private static readonly string[] _expectedLibraryAssemblies = [
        "Jeninnet.Localization.Abstractions",
        "Jeninnet.Localization.Core",
        "Jeninnet.Localization.Storage",
        "Jeninnet.Localization.Providers.Database.Abstractions",
        "Jeninnet.Localization.Providers.Database.EntityFrameworkCore",
        "Jeninnet.Localization.Providers.Database.SqlServer",
        "Jeninnet.Localization.Providers.Database.MySql",
        "Jeninnet.Localization.Providers.Database.PostgreSQL",
        "Jeninnet.Localization.Providers.Database.SQLite",
        "Jeninnet.Localization.Providers.Json",
        "Jeninnet.Localization.Providers.Memory",
        "Jeninnet.Localization.Providers.Po",
        "Jeninnet.Localization.Providers.ResX",
        "Jeninnet.Localization.Providers.Satellite",
        "Jeninnet.Localization.Providers.Xml",
        "Jeninnet.Localization.Providers.Pseudo",
        "Jeninnet.Localization.Translation.Azure",
        "Jeninnet.Localization.Translation.Google",
        "Jeninnet.Localization.Integrations.Avalonia",
        "Jeninnet.Localization.Integrations.Blazor",
        "Jeninnet.Localization.Integrations.DataAnnotations",
        "Jeninnet.Localization.Integrations.Diagnostics",
        "Jeninnet.Localization.Integrations.JavaScript",
        "Jeninnet.Localization.Integrations.Mvc.Validation",
        "Jeninnet.Localization.Integrations.RequestLocalization",
        "Jeninnet.Localization.Interchange.Abstractions",
        "Jeninnet.Localization.Interchange.Csv",
        "Jeninnet.Localization.Interchange.Po",
        "Jeninnet.Localization.Interchange.ResX",
        "Jeninnet.Localization.Interchange.Xliff",
        "Jeninnet.Localization.Generators.Keys",
        "Jeninnet.Localization.Tools.Keys",
        "Jeninnet.Localization.Analyzers.Localizability",
        "Jeninnet.Localization.Analyzers.Localizability.CodeFixes"
    ];

    /// <summary>
    /// Platform-specific integrations are validated by the Windows host test project because their
    /// target frameworks cannot load in every library-test target.
    /// </summary>
    private static readonly string[] _platformLibraryAssemblies = [
        "Jeninnet.Localization.Integrations.Maui",
        "Jeninnet.Localization.Integrations.WinUI",
    ];
    private const string GENERATED_REGEX_NAMESPACE = "System.Text.RegularExpressions.Generated";

    /// <summary>
    /// The coverage collector's injected instrumentation namespace prefix (coverlet adds its
    /// tracker type to every instrumented assembly). Present only while coverage is being
    /// collected, never authored, and excluded from the identity invariant exactly like the
    /// compiler-generated namespaces.
    /// </summary>
    private const string COVERLET_INSTRUMENTATION_NAMESPACE_PREFIX = "Coverlet.";

    /// <summary>
    /// Compiler-embedded attribute namespaces: the netstandard2.0 generator
    /// carries Roslyn-embedded infrastructure (<c>Microsoft.CodeAnalysis.EmbeddedAttribute</c>
    /// plus the <c>System.Runtime.CompilerServices</c> nullable/<c>InternalsVisibleTo</c>
    /// attributes). Same category as <see cref="GENERATED_REGEX_NAMESPACE"/>: emitted by
    /// the compiler, never authored, excluded from the identity invariant.
    /// </summary>
    private static readonly string[] _compilerEmbeddedNamespaces = [
        "Microsoft.CodeAnalysis",
        "System.Runtime.CompilerServices",
    ];

    /// <summary>
    /// The sole Jeninnet reference a translation Provider may emit or declare
    /// (Architecture §18 Rule 8, §30). Hoisted to satisfy CA1861.
    /// </summary>
    private static readonly string[] _translationProviderAllowedReferences = ["Jeninnet.Localization.Abstractions"];

    /// <summary>
    /// The centralized architectural pairing between each <see cref="ProviderFeature"/> and its
    /// capability interface (Architecture §31.2). <see cref="ProviderFeature.None"/> and
    /// <see cref="ProviderFeature.Caching"/> are interface-less (guard / declarative-only). Tests
    /// reason from this map rather than from per-Provider assumptions.
    /// </summary>
    private static readonly Dictionary<ProviderFeature, Type?> _capabilityFeatureInterfaces = new() {
        [ProviderFeature.None] = null,
        [ProviderFeature.MultiKeys] = typeof(IMultiKeyMessageProvider),
        [ProviderFeature.EventsListening] = typeof(IProviderEventSource),
        [ProviderFeature.Preload] = typeof(IPreloadableMessageProvider),
        [ProviderFeature.Refresh] = typeof(IRefreshableMessageProvider),
        [ProviderFeature.Caching] = null,
    };

    /// <summary>
    /// Verifies that assembly boundary expected assemblies exist and are loadable.
    /// </summary>
    [TestMethod]
    public void AssemblyBoundary_ExpectedAssembliesExistAndAreLoadable() {
        foreach(var name in _expectedLibraryAssemblies) {
            var assembly = LoadAssembly(name);
            assembly.GetName().Name.Should().Be(name, $"expected assembly identity {name} to load with its declared simple name");
        }
    }

    /// <summary>
    /// Verifies that assembly discovery expected library set matches actual solution projects.
    /// </summary>
    [TestMethod]
    public void AssemblyDiscovery_ExpectedLibrarySetMatchesActualSolutionProjects() {
        var actualLibraryProjects = _csprojPaths.Value.Values
            .Where(static p => p.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                               p.Contains($"{Path.DirectorySeparatorChar}integrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(static p => Path.GetFileNameWithoutExtension(p))
            .ToArray();

        var expectedLibraryProjects = _expectedLibraryAssemblies.Concat(_platformLibraryAssemblies).ToArray();
        expectedLibraryProjects.Should().BeEquivalentTo(
            actualLibraryProjects,
            $"[Architecture] the expected library inventory must equal the projects actually declared under src/ and integrations/ (testing infrastructure moved to its standalone repository; missing: {string.Join(", ", expectedLibraryProjects.Except(actualLibraryProjects))}; unexpected: {string.Join(", ", actualLibraryProjects.Except(expectedLibraryProjects))})");
    }

    /// <summary>
    /// Verifies that the platform host integrations declare only the Abstractions project.
    /// </summary>
    [TestMethod]
    public void PlatformHostIntegrations_DeclareOnlyAbstractions() {
        foreach(var assemblyName in _platformLibraryAssemblies) {
            var declaredProjects = DeclaredProjectReferences(assemblyName);

            declaredProjects.Should().BeEquivalentTo(
                ["Jeninnet.Localization.Abstractions"],
                $"{assemblyName} must declare only the Abstractions ProjectReference");
        }
    }

    /// <summary>
    /// Verifies that providers must not have specific entities or dtos.
    /// </summary>
    [TestMethod]
    public void Providers_MustNotHaveSpecificEntitiesOrDtos() {
        var assemblies = ProviderAssemblies();

        // Non-vacuous guard: the scan must actually cover the database provider assemblies.
        assemblies.Select(static a => a.GetName().Name!).Should().Contain("Jeninnet.Localization.Providers.Database.EntityFrameworkCore");
        assemblies.Select(static a => a.GetName().Name!).Should().Contain("Jeninnet.Localization.Providers.Database.Abstractions");

        foreach(var assembly in assemblies) {
            foreach(var type in assembly.GetTypes()) {
                if(
                    (type.Name.EndsWith("Entity", StringComparison.OrdinalIgnoreCase) || type.Name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)) &&
                    type.Assembly != typeof(LocalizedMessageEntity).Assembly
                ) {
                    throw new ForbiddenProviderTypeException($"Forbidden provider-specific type found: {type.FullName}");
                }
            }
        }

        true.Should().BeTrue("No provider-specific entities or DTOs should be found in the provider assemblies.");
    }

    /// <summary>
    /// Verifies that purification removes obsolete runtime apis and scrutor metadata.
    /// </summary>
    [TestMethod]
    public void Purification_RemovesObsoleteRuntimeApisAndScrutorMetadata() {
        foreach(var assemblyName in _expectedLibraryAssemblies) {
            var assembly = LoadAssembly(assemblyName);
            assembly.GetCustomAttributes<ObsoleteAttribute>().Should().BeEmpty();

            foreach(var type in assembly.GetExportedTypes()) {
                type.GetCustomAttributes<ObsoleteAttribute>().Should().BeEmpty($"{type.FullName} must not be obsolete");
                foreach(var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
                    // Members declared by the framework (e.g. Enum.ToString is [Obsolete] in newer
                    // runtimes) are not our API and may carry framework-level Obsolete attributes.
                    if(member.DeclaringType?.Assembly == typeof(object).Assembly) {
                        continue;
                    }

                    var obsoleteAttributes = member.GetCustomAttributes<ObsoleteAttribute>()
                        .Where(
                            static attribute =>
                            attribute.Message?.StartsWith(
                                "Constructors of types with required members",
                                StringComparison.Ordinal
                            ) is false
                        )
                        .ToArray();
                    obsoleteAttributes.Should().BeEmpty($"{type.FullName}.{member.Name} must not be obsolete");
                }
            }
        }

        foreach(var csproj in Directory.EnumerateFiles(_repoRoot.Value, "*.csproj", SearchOption.AllDirectories)) {
            Assert.IsFalse(File.ReadAllText(csproj).Contains("Scrutor", StringComparison.Ordinal), $"{csproj} must not declare Scrutor");
        }

        Assert.IsFalse(
            File.ReadAllText(Path.Combine(_repoRoot.Value, "Directory.Packages.props"))
                .Contains("Scrutor", StringComparison.Ordinal),
            "Scrutor must not remain in central package metadata");
    }

    /// <summary>
    /// Verifies that storage must not contain fallback fields.
    /// </summary>
    [TestMethod]
    public void Storage_MustNotContainFallbackFields() {
        var entityType = typeof(LocalizedMessageEntity);
        var dtoType = typeof(LocalizedMessageEntry);

        entityType.GetProperty("ParentCulture").Should().BeNull();
        entityType.GetProperty("FallbackChain").Should().BeNull();
        dtoType.GetProperty("ParentCulture").Should().BeNull();
        dtoType.GetProperty("FallbackChain").Should().BeNull();
    }

    /// <summary>
    /// Verifies that providers all implement IInner message provider.
    /// </summary>
    [TestMethod]
    public void Providers_AllImplementIInnerMessageProvider() {
        // Concrete *MessageProvider classes are discovered across the real provider
        // assemblies. An earlier version filtered a nonexistent
        // "IValidationMessageProvider" interface and therefore scanned nothing.
        var providerTypes = ProviderAssemblies()
            .SelectMany(static a => a.GetTypes())
            .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                               t.Name.EndsWith("MessageProvider", StringComparison.Ordinal))
            .DistinctBy(static t => t.FullName);

        providerTypes.Should().NotBeEmpty("the provider assemblies must contain concrete *MessageProvider classes (non-vacuous scan)");

        foreach(var type in providerTypes) {
            var implementsInner = typeof(IInnerMessageProvider).IsAssignableFrom(type);
            implementsInner.Should().BeTrue($"Provider {type.Name} must implement IInnerMessageProvider");
        }
    }

    /// <summary>
    /// Verifies that localization provider selector is sealed.
    /// </summary>
    [TestMethod]
    public void LocalizationProviderSelector_IsSealed() => typeof(LocalizationProviderSelector).IsSealed.Should().BeTrue();

    /// <summary>
    /// Verifies that localization contracts are interfaces only in abstractions.
    /// </summary>
    [TestMethod]
    public void LocalizationContracts_AreInterfacesOnlyInAbstractions() {
        var abstractionsAssembly = typeof(IMessageProvider).Assembly;
        var coreTypeNames = LoadAssembly("Jeninnet.Localization.Core").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var publicInterfaces = abstractionsAssembly.GetTypes()
                                                   .Where(static t => t is { IsInterface: true, IsPublic: true });

        foreach(var contract in new[] { typeof(IMessageProvider), typeof(IInnerMessageProvider), typeof(IMessageLocalizer), typeof(IMessageFormatter) }) {
            publicInterfaces.Should().ContainSingle(i => i == contract);
            coreTypeNames.Contains(contract.Name).Should().BeFalse(
                $"[Architecture] the {contract.Name} contract must be OWNED by Abstractions, not duplicated in Core");
        }
    }

    /// <summary>
    /// Verifies that core no static localization classes.
    /// </summary>
    [TestMethod]
    public void Core_NoStaticLocalizationClasses() {
        var localizationAssemblies = _expectedLibraryAssemblies
            .Select(LoadAssembly)
            .Where(static a => a.FullName?.StartsWith("Jeninnet.Localization.Core", StringComparison.Ordinal) == true &&
                               !a.FullName.Contains("Abstractions", StringComparison.Ordinal) &&
                               !a.FullName.Contains("Tests", StringComparison.Ordinal));

        // Non-vacuous guard: Core itself must be inspected.
        localizationAssemblies.Select(static a => a.GetName().Name!).Should().Contain("Jeninnet.Localization.Core");

        var allowedStaticTypes = new HashSet<string> {
            "ServiceCollectionExtensions",
            "ApplicationBuilderExtensions",
            "JeninnetLocalizationExtensions"
        };

        foreach(var assembly in localizationAssemblies) {
            var staticClasses = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true } &&
                            !allowedStaticTypes.Contains(t.Name));

            foreach(var cls in staticClasses) {
                if(cls.GetMethods(BindingFlags.Public | BindingFlags.Static)
                       .Any(m => m.Name.Contains("Localization", StringComparison.OrdinalIgnoreCase) ||
                                 m.Name.Contains("Message", StringComparison.OrdinalIgnoreCase))) {
                    throw new ForbiddenProviderTypeException(
                        $"Static class with localization-related methods found: {cls.FullName}");
                }
            }
        }

        true.Should().BeTrue("No static APIs for localization found in provider assemblies.");
    }

    // =====================================================================
    // Architecture §18 dependency rules, exposed as architecture tests.
    // Two classifications are used for each test:
    //   PASS: this checkpoint already satisfies the final rule.
    //   EXPECTED FAILURE: this checkpoint violates the rule; the correction is
    //                      intentionally deferred.
    // =====================================================================

    /// <summary>
    /// Verifies that rule01 abstractions must not depend on implementations.
    /// </summary>
    [TestMethod]
    public void Rule01_Abstractions_MustNotDependOnImplementations() {
        var emitted = ReferencedNames(LoadAssembly("Jeninnet.Localization.Abstractions"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Abstractions");
        var declaredPackages = DeclaredPackageReferences("Jeninnet.Localization.Abstractions");

        emitted.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal)).Should().BeEmpty(
            $"[Architecture] Abstractions must emit references to no implementation assembly (found: {string.Join(", ", emitted)})");
        declaredProjects.Should().BeEmpty(
            "[Architecture] Abstractions must declare no ProjectReferences to implementation assemblies (only BCL + Microsoft.Extensions.* is allowed)");

        // The architecture boundary is about the whole dependency surface, not just
        // the emitted graph: an unauthorized declared package (EF, MVC, Scrutor, an
        // implementation package, or any unrelated third-party implementation) must
        // not silently pass. The declared package surface must stay framework-only.
        var forbiddenPackageKinds = new[] { "EntityFrameworkCore", "AspNetCore.Mvc", "Scrutor" };
        var declaredForbiddenPackages = declaredPackages.Where(p =>
            p.StartsWith("Jeninnet.", StringComparison.Ordinal) ||
            forbiddenPackageKinds.Any(kind => p.StartsWith("Microsoft." + kind, StringComparison.Ordinal)));
        declaredForbiddenPackages.Should().BeEmpty(
            $"[Architecture] Abstractions must not DECLARE implementation/framework packages (found: {string.Join(", ", declaredForbiddenPackages)}); only BCL + Microsoft.Extensions.* abstractions are permitted");

        var declaredUnrelatedPackages = declaredPackages.Where(static p =>
            !p.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) &&
            !p.StartsWith("System.", StringComparison.Ordinal) &&
            !p.StartsWith("Jeninnet.", StringComparison.Ordinal));
        declaredUnrelatedPackages.Should().BeEmpty(
            $"[Architecture] Abstractions must not DECLARE unrelated third-party implementation packages (found: {string.Join(", ", declaredUnrelatedPackages)})");
    }

    /// <summary>
    /// Verifies that rule02 core must not depend on providers integrations translation storage or EF.
    /// </summary>
    [TestMethod]
    public void Rule02_Core_MustNotDependOnProvidersIntegrationsTranslationStorageOrEF() {
        var emitted = ReferencedNames(LoadAssembly("Jeninnet.Localization.Core"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Core");
        var declaredPackages = DeclaredPackageReferences("Jeninnet.Localization.Core");

        static bool IsForbiddenLocal(string name) =>
            name == "Jeninnet.Localization.Storage" ||
            name.StartsWith("Jeninnet.Localization.Providers.", StringComparison.Ordinal) ||
            name.StartsWith("Jeninnet.Localization.Translation.", StringComparison.Ordinal) ||
            name.StartsWith("Jeninnet.Localization.Integrations.", StringComparison.Ordinal);

        var declaredLocalOffenders = declaredProjects.Where(IsForbiddenLocal);
        var emittedLocalOffenders = emitted.Where(IsForbiddenLocal);

        declaredLocalOffenders.Should().BeEmpty(
            $"[Architecture] Core must not DECLARE upward ProjectReferences (found: {string.Join(", ", declaredLocalOffenders)})");
        emittedLocalOffenders.Should().BeEmpty(
            $"[Architecture] Core must not EMIT upward references (found: {string.Join(", ", emittedLocalOffenders)})");

        var forbiddenFrameworks = new[] {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.SqlServer",
            "Microsoft.AspNetCore.Mvc.Core",
            "Scrutor"
        };

        var declaredFrameworkOffenders = declaredPackages.Where(forbiddenFrameworks.Contains);
        var emittedFrameworkOffenders = emitted.Where(forbiddenFrameworks.Contains);

        declaredFrameworkOffenders.Should().BeEmpty(
            $"[Architecture] Core must not DECLARE EF / Mvc.Core / Scrutor packages (found: {string.Join(", ", declaredFrameworkOffenders)})");
        emittedFrameworkOffenders.Should().BeEmpty(
            $"[Architecture] Core must not EMIT EF / Mvc.Core / Scrutor references (found: {string.Join(", ", emittedFrameworkOffenders)})");
    }

    /// <summary>
    /// Verifies that rule03 storage must be BCLOnly.
    /// </summary>
    [TestMethod]
    public void Rule03_Storage_MustBeBCLOnly() {
        var emitted = ReferencedNames(LoadAssembly("Jeninnet.Localization.Storage"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Storage");
        var declaredPackages = DeclaredPackageReferences("Jeninnet.Localization.Storage");

        declaredProjects.Should().BeEmpty(
            $"[Architecture] Storage must not DECLARE ProjectReferences (found: {string.Join(", ", declaredProjects)})");
        declaredPackages.Should().BeEmpty(
            $"[Architecture] Storage must not DECLARE PackageReferences (found: {string.Join(", ", declaredPackages)})");

        var emittedForbidden = emitted.Where(static n =>
            n.StartsWith("Jeninnet.", StringComparison.Ordinal) ||
            n.StartsWith("Microsoft.", StringComparison.Ordinal));
        emittedForbidden.Should().BeEmpty(
            $"[Architecture] Storage must emit only BCL references (found: {string.Join(", ", emittedForbidden)})");
    }

    /// <summary>
    /// Verifies that rule04 providers must not depend on core.
    /// </summary>
    [TestMethod]
    public void Rule04_Providers_MustNotDependOnCore() {
        var violations = FindProviderToCoreViolations(LocalReferenceGraph(_expectedLibraryAssemblies));

        violations.Should().BeEmpty(
            $"[Architecture] Providers → Core is FORBIDDEN and must remain absent (violating: {string.Join(", ", violations)})");
    }

    /// <summary>
    /// Verifies that rule05 database abstractions must not reference core or vendor adapters.
    /// </summary>
    [TestMethod]
    public void Rule05_DatabaseAbstractions_MustNotReferenceCoreOrVendorAdapters() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Providers.Database.Abstractions"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Providers.Database.Abstractions");

        declaredProjects.Contains("Jeninnet.Localization.Core").Should().BeFalse(
            "[Architecture] Database.Abstractions must not DECLARE a Core ProjectReference; the contract belongs in Abstractions");

        var forbidden = referenced.Where(static n =>
            n == "Jeninnet.Localization.Core" ||
            n.StartsWith("Jeninnet.Localization.Providers.Database.", StringComparison.Ordinal));
        forbidden.Should().BeEmpty($"[Architecture] Database.Abstractions must not depend on Core or on database vendor adapters (found: {string.Join(", ", forbidden)})");
    }

    /// <summary>
    /// Verifies that rule06 data annotations integration must not reference core or abstractions.
    /// </summary>
    [TestMethod]
    public void Rule06_DataAnnotationsIntegration_MustNotReferenceCoreOrAbstractions() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.DataAnnotations"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.DataAnnotations");

        declaredProjects.Contains("Jeninnet.Localization.Core").Should().BeFalse(
            "[Architecture] the DataAnnotations integration must DECLARE no Core ProjectReference");
        declaredProjects.Should().BeEmpty(
            "[Architecture] the DataAnnotations integration must DECLARE no Jeninnet ProjectReference at all (fully decoupled)");
        referenced.Contains("Jeninnet.Localization.Core").Should().BeFalse(
            "[Architecture] the DataAnnotations integration must not emit a Core reference");
        referenced.Contains("Jeninnet.Localization.Abstractions").Should().BeFalse("[Architecture] must not reference Jeninnet.Localization.Abstractions");
        referenced.Contains("Microsoft.Extensions.Localization.Abstractions").Should().BeTrue("[Architecture] must depend on IStringLocalizer from Microsoft.Extensions.Localization.Abstractions");
    }

    /// <summary>
    /// Verifies that rule07 mvc validation is the documented core dependency exception.
    /// </summary>
    [TestMethod]
    public void Rule07_MvcValidation_IsTheDocumentedCoreDependencyException() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.Mvc.Validation"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.Mvc.Validation");

        referenced.Should().Contain("Jeninnet.Localization.Core", "[Architecture] Mvc.Validation may depend on Core (CustomStringLocalizerFactory bridge)");
        referenced.Should().Contain("Microsoft.AspNetCore.Mvc.Core", "[Architecture] Mvc.Validation is coupled to Microsoft.AspNetCore.Mvc.Core");

        declaredProjects.Should().Contain("Jeninnet.Localization.Core",
            "[Architecture] Mvc.Validation must DECLARE its Core ProjectReference (CustomStringLocalizerFactory bridge)");
        declaredProjects.Should().HaveCount(1,
            "[Architecture] Mvc.Validation's only Jeninnet ProjectReference is Core");

        var forbidden = referenced.Where(static n =>
            n.StartsWith("Jeninnet.Localization.Providers.", StringComparison.Ordinal) ||
            n.StartsWith("Jeninnet.Localization.Translation.", StringComparison.Ordinal) ||
            n.StartsWith("Jeninnet.Localization.Integrations.", StringComparison.Ordinal) ||
            n == "Jeninnet.Localization.Storage");
        forbidden.Should().BeEmpty($"[Architecture] Mvc.Validation must not reference providers/translation/integrations/storage (found: {string.Join(", ", forbidden)})");
    }

    /// <summary>
    /// Verifies that translation abstractions is removed from active architecture.
    /// </summary>
    [TestMethod]
    public void TranslationAbstractions_IsRemovedFromActiveArchitecture() {
        const string obsoleteProjectName = "Jeninnet.Localization.Translation.Abstractions";

        _csprojPaths.Value.ContainsKey(obsoleteProjectName).Should().BeFalse(
            $"[Architecture] the obsolete {obsoleteProjectName} project must be removed from the repository");

        var slnxText = File.ReadAllText(Path.Combine(_repoRoot.Value, SLN_FILE_NAME));
        slnxText.Contains(obsoleteProjectName, StringComparison.Ordinal).Should().BeFalse(
            $"[Architecture] the obsolete {obsoleteProjectName} project must not be listed in {SLN_FILE_NAME}");
    }

    /// <summary>
    /// Verifies that rule07 integration exception is modeled explicitly.
    /// </summary>
    [TestMethod]
    public void Rule07_IntegrationException_IsModeledExplicitly() {
        var references = LocalReferenceGraph(_expectedLibraryAssemblies);
        var integrationNames = references.Keys
            .Where(static name => name.StartsWith("Jeninnet.Localization.Integrations.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        integrationNames.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Integrations.Avalonia", "Jeninnet.Localization.Integrations.Blazor", "Jeninnet.Localization.Integrations.DataAnnotations", "Jeninnet.Localization.Integrations.Diagnostics", "Jeninnet.Localization.Integrations.JavaScript", "Jeninnet.Localization.Integrations.Mvc.Validation", "Jeninnet.Localization.Integrations.RequestLocalization"],
            "[Architecture] all documented integration assemblies must be inspected (non-vacuous discovery)");

        references["Jeninnet.Localization.Integrations.Mvc.Validation"].Should().Contain("Jeninnet.Localization.Core",
            "[Architecture] Mvc.Validation is the ONLY integration allowed to reference Core (documented exception)");

        var nonMvcViolations = FindNonMvcIntegrationCoreReferences(references);
        nonMvcViolations.Should().BeEmpty(
            $"[Architecture] only Mvc.Validation may reference Core (violating: {string.Join(", ", nonMvcViolations)})");
    }

    /// <summary>
    /// Verifies that blazor integration depends only on abstractions and never on core.
    /// </summary>
    [TestMethod]
    public void BlazorIntegration_DependsOnlyOnAbstractions_AndNeverOnCore() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.Blazor"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.Blazor");

        declaredProjects.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            "the Blazor integration's only Jeninnet ProjectReference is Abstractions (WASM-compatible provider marker)");

        var emittedLocal = referenced.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal));
        emittedLocal.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            $"the Blazor integration must emit no other Jeninnet reference (found: {string.Join(", ", emittedLocal)})");

        referenced.Should().NotContain("Jeninnet.Localization.Core",
            "[Architecture] only Mvc.Validation may depend on Core");
        referenced.Should().Contain("Microsoft.AspNetCore.Components.Server",
            "the circuit culture handler is coupled to the ASP.NET Core shared framework (FrameworkReference)");
        referenced.Should().Contain("Microsoft.JSInterop",
            "the culture stores use IJSRuntime");
    }

    /// <summary>
    /// Verifies that java script integration depends only on abstractions and never on core.
    /// </summary>
    [TestMethod]
    public void JavaScriptIntegration_DependsOnlyOnAbstractions_AndNeverOnCore() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.JavaScript"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.JavaScript");

        declaredProjects.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            "the JavaScript integration's only Jeninnet ProjectReference is Abstractions (IMessageProvider)");

        var emittedLocal = referenced.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal));
        emittedLocal.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            $"the JavaScript integration must emit no other Jeninnet reference (found: {string.Join(", ", emittedLocal)})");

        referenced.Should().NotContain("Jeninnet.Localization.Core",
            "[Architecture] only Mvc.Validation may depend on Core");
        referenced.Should().Contain("Microsoft.AspNetCore.Routing",
            "the endpoint mapping is coupled to the ASP.NET Core shared framework (FrameworkReference)");
        referenced.Should().Contain("Microsoft.AspNetCore.Http.Results",
            "the endpoint returns minimal-API results");
    }

    /// <summary>
    /// Verifies that diagnostics integration depends only on abstractions and is not framework is coupled.
    /// </summary>
    [TestMethod]
    public void DiagnosticsIntegration_DependsOnlyOnAbstractions_AndIsNotFrameworkCoupled() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.Diagnostics"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.Diagnostics");

        declaredProjects.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            "the Diagnostics integration's only Jeninnet ProjectReference is Abstractions");

        var emittedLocal = referenced.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal));
        emittedLocal.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            $"the Diagnostics integration must emit no other Jeninnet reference (found: {string.Join(", ", emittedLocal)})");

        referenced.Should().NotContain("Jeninnet.Localization.Core",
            "[Architecture] only Mvc.Validation may depend on Core");
        referenced.Should().Contain("Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions",
            "health checks use the Microsoft.Extensions package surface");
        referenced.Should().NotContain("Microsoft.AspNetCore.Http.Abstractions",
            "the Diagnostics integration deliberately has NO ASP.NET Core shared-framework coupling");
    }

    /// <summary>
    /// Verifies that request localization integration depends only on abstractions and never on core.
    /// </summary>
    [TestMethod]
    public void RequestLocalizationIntegration_DependsOnlyOnAbstractions_AndNeverOnCore() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Integrations.RequestLocalization"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Integrations.RequestLocalization");

        declaredProjects.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            "the request-localization bridge's only Jeninnet ProjectReference is Abstractions");

        var emittedLocal = referenced.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal));
        emittedLocal.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            $"the request-localization bridge must emit no other Jeninnet reference (found: {string.Join(", ", emittedLocal)})");

        referenced.Should().NotContain("Jeninnet.Localization.Core",
            "[Architecture] only Mvc.Validation may depend on Core");
        referenced.Should().Contain("Microsoft.AspNetCore.Localization",
            "the bridge is coupled to the RequestLocalizationMiddleware surface (FrameworkReference)");
        referenced.Should().Contain("Microsoft.AspNetCore.Http.Abstractions",
            "the culture providers read HttpContext request data");
    }

    /// <summary>
    /// Verifies that fi04 open telemetry stays consumer side no project references APackage.
    /// </summary>
    [TestMethod]
    public void Fi04_OpenTelemetryStaysConsumerSide_NoProjectReferencesAPackage() {
        var violations = new List<string>();
        foreach(var (projectName, csprojPath) in _csprojPaths.Value) {
            if(File.ReadAllText(csprojPath).Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase)) {
                violations.Add(projectName);
            }
        }

        violations.Should().BeEmpty(
            $"OpenTelemetry glue stays consumer-side: no project may reference an OpenTelemetry package (found: {string.Join(", ", violations)})");

        var centralPackages = File.ReadAllText(Path.Combine(_repoRoot.Value, "Directory.Packages.props"));
        centralPackages.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase).Should().BeFalse(
            "the central package versions must not declare an OpenTelemetry package");
    }

    /// <summary>
    /// Verifies that rule08 translation providers reference only abstractions.
    /// </summary>
    [TestMethod]
    public void Rule08_TranslationProviders_ReferenceOnlyAbstractions() {
        foreach(var name in new[] { "Jeninnet.Localization.Translation.Azure", "Jeninnet.Localization.Translation.Google" }) {
            var emitted = ReferencedNames(LoadAssembly(name));
            var declaredProjects = DeclaredProjectReferences(name);

            var emittedLocal = emitted.Where(static n => n.StartsWith("Jeninnet.", StringComparison.Ordinal));
            emittedLocal.Should().BeEquivalentTo(_translationProviderAllowedReferences,
                $"[Architecture] {name} must emit references to ONLY Jeninnet.Localization.Abstractions among Jeninnet assemblies (found: {string.Join(", ", emittedLocal)})");

            declaredProjects.Should().BeEquivalentTo(_translationProviderAllowedReferences,
                $"[Architecture] {name} must DECLARE no ProjectReference other than Jeninnet.Localization.Abstractions (found: {string.Join(", ", declaredProjects)})");
        }
    }

    /// <summary>
    /// Verifies that translation providers implement IInner message provider.
    /// </summary>
    [TestMethod]
    public void TranslationProviders_ImplementIInnerMessageProvider() {
        foreach(var name in new[] { "Jeninnet.Localization.Translation.Azure", "Jeninnet.Localization.Translation.Google" }) {
            var providers = LoadAssembly(name).GetTypes()
                .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                                   typeof(IInnerMessageProvider).IsAssignableFrom(t))
                .ToArray();

            providers.Should().NotBeEmpty($"[Architecture] {name} must contain a concrete Provider implementing IInnerMessageProvider");

            foreach(var provider in providers) {
                var interfaces = provider.GetInterfaces().Select(static @interface => @interface.Name);
                interfaces.Should().NotContain("ITranslationProvider",
                    $"[Architecture] {provider.Name} must not implement the obsolete ITranslationProvider contract");

                var hasPublicTranslateAsync = provider.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(static m => !m.IsSpecialName && m.Name == "TranslateAsync");
                hasPublicTranslateAsync.Should().BeFalse(
                    $"[Architecture] {provider.Name} must not expose a public TranslateAsync method (obsolete translation surface)");
            }
        }
    }

    /// <summary>
    /// Verifies that capability model lives in abstractions.
    /// </summary>
    [TestMethod]
    public void CapabilityModel_LivesInAbstractions() {
        var abstractions = LoadAssembly("Jeninnet.Localization.Abstractions");
        var capabilityTypes = new[] {
            typeof(ProviderFeature),
            typeof(IProviderCapabilities),
            typeof(IMultiKeyMessageProvider),
            typeof(IPreloadableMessageProvider),
            typeof(IRefreshableMessageProvider),
            typeof(IProviderEventSource),
            typeof(ProviderAcquisitionEventArgs),
            typeof(ProviderAcquisitionFailedEventArgs),
        };

        foreach(var type in capabilityTypes) {
            ((object)type.Assembly).Should().BeSameAs(abstractions,
                $"[Architecture] {type.Name} must live in Jeninnet.Localization.Abstractions so both translation projects share the provider-neutral capability model");
        }
    }

    /// <summary>
    /// Verifies that capability architecture feature interface mapping is complete and is centralized.
    /// </summary>
    [TestMethod]
    public void CapabilityArchitecture_FeatureInterfaceMapping_IsCompleteAndCentralized() {
        _capabilityFeatureInterfaces.Keys.Should().BeEquivalentTo(
            Enum.GetValues<ProviderFeature>(),
            "[Architecture] the classification map must cover exactly the ProviderFeature members");

        foreach(var (feature, capabilityInterface) in _capabilityFeatureInterfaces) {
            if(feature is ProviderFeature.Caching or ProviderFeature.None) {
                capabilityInterface.Should().BeNull(
                    $"[Architecture] ProviderFeature.{feature} must remain interface-less");
            } else {
                capabilityInterface.Should().NotBeNull(
                    $"[Architecture] ProviderFeature.{feature} must be operational (paired with a capability interface)");
            }
        }

        LoadAssembly("Jeninnet.Localization.Abstractions").GetTypes()
            .Any(static t => t.Name == "ICachingMessageProvider")
            .Should().BeFalse("[Architecture] no ICachingMessageProvider may exist; Caching is declarative-only");
    }

    /// <summary>
    /// Verifies that provider feature none is the explicit non capability default.
    /// </summary>
    [TestMethod]
    public void ProviderFeature_None_IsTheExplicitNonCapabilityDefault() {
        default(ProviderFeature).Should().Be(ProviderFeature.None,
            "[Architecture] an unset ProviderFeature value must never silently represent a real capability");

        var provider = new AzureTranslationProvider(
            Options.Create(new AzureTranslationOptions { ApiKey = "architecture-test" }),
            new HttpClient());
        provider.Supports(default).Should().BeFalse(
            "[Architecture] an unset ProviderFeature value must not report any operational capability");
    }

    /// <summary>
    /// Verifies that capability architecture reported capabilities require declared capability interface.
    /// </summary>
    [TestMethod]
    public void CapabilityArchitecture_ReportedCapabilities_RequireDeclaredCapabilityInterface() {
        var providers = DiscoverCapabilityProviders().ToArray();

        providers.Select(static provider => provider.GetType().Assembly.GetName().Name!)
            .Should().Contain("Jeninnet.Localization.Translation.Azure",
                "[Architecture] the Azure translation Provider must participate in the capability model");
        providers.Select(static provider => provider.GetType().Assembly.GetName().Name!)
            .Should().Contain("Jeninnet.Localization.Translation.Google",
                "[Architecture] the Google translation Provider must participate in the capability model");

        foreach(var provider in providers) {
            foreach(var (feature, capabilityInterface) in _capabilityFeatureInterfaces) {
                if(capabilityInterface is null || !provider.Supports(feature)) {
                    continue;
                }

                capabilityInterface.IsInstanceOfType(provider).Should().BeTrue(
                    $"[Architecture] {provider.GetType().Name} reports ProviderFeature.{feature} but does not implement {capabilityInterface.Name}");
            }
        }
    }

    /// <summary>
    /// Verifies that google translation provider reports no optional capabilities.
    /// </summary>
    [TestMethod]
    public void GoogleTranslationProvider_ReportsNoOptionalCapabilities() {
        using var client = new HttpClient();
        var provider = new GoogleTranslationProvider(
            Options.Create(new GoogleTranslationOptions { ApiKey = "architecture-test" }),
            client);

        foreach(var feature in Enum.GetValues<ProviderFeature>()) {
            provider.Supports(feature).Should().BeFalse(
                $"[Architecture] Google must honestly report unsupported feature {feature}");
        }
    }

    /// <summary>
    /// Verifies that translation providers do not expose http client publicly.
    /// </summary>
    [TestMethod]
    public void TranslationProviders_DoNotExposeHttpClientPublicly() {
        foreach(var providerType in new[] { typeof(AzureTranslationProvider), typeof(GoogleTranslationProvider) }) {
            var httpMembers = providerType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(static member => member switch {
                    MethodInfo method => method.ReturnType,
                    PropertyInfo property => property.PropertyType,
                    EventInfo => null,
                    FieldInfo field => field.FieldType,
                    _ => null,
                })
                .Where(static returnType => returnType == typeof(HttpClient))
                .ToArray();

            httpMembers.Should().BeEmpty(
                $"[Architecture] {providerType.Name} must not publicly expose its HttpClient (HTTP client injection is constructor-only)");
        }
    }

    /// <summary>
    /// Verifies that rule09 ef core store provider must not reference core.
    /// </summary>
    [TestMethod]
    public void Rule09_EfCoreStoreProvider_MustNotReferenceCore() {
        var referenced = ReferencedNames(LoadAssembly("Jeninnet.Localization.Providers.Database.EntityFrameworkCore"));
        var declaredProjects = DeclaredProjectReferences("Jeninnet.Localization.Providers.Database.EntityFrameworkCore");

        declaredProjects.Contains("Jeninnet.Localization.Core").Should().BeFalse(
            "[Architecture] the EF Core store provider must not DECLARE a Core ProjectReference");
        declaredProjects.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Abstractions"],
            "[Architecture] the EF Core store provider's only Jeninnet ProjectReference is Abstractions");

        referenced.Contains("Jeninnet.Localization.Core").Should().BeFalse(
            "[Architecture] the EF Core store provider must not reference Core");
        referenced.Should().Contain("Jeninnet.Localization.Abstractions", "[Architecture] the store provider must depend on Abstractions (store contracts)");
        referenced.Should().Contain("Microsoft.EntityFrameworkCore", "[Architecture] the store provider must use EF Core");
        var declaredPackageNames = XDocument.Load(_csprojPaths.Value["Jeninnet.Localization.Providers.Database.EntityFrameworkCore"])
            .Descendants("PackageReference")
            .Select(static e => e.Attribute("Include")?.Value)
            .Where(static v => v is not null)
            .Cast<string>();
        declaredPackageNames.Should().Contain(
            "Microsoft.EntityFrameworkCore.SqlServer",
            "[Architecture] the store provider declares the SQL Server EF Core package");
    }

    /// <summary>
    /// Verifies that rule11 no dependency cycles.
    /// </summary>
    [TestMethod]
    public void Rule11_NoDependencyCycles() {
        var cycle = FindFirstDependencyCycle(LocalReferenceGraph(_expectedLibraryAssemblies));

        var cycleDescription = cycle is null ? "none" : string.Join(" → ", cycle);
        Assert.IsNull(cycle, $"[Architecture] dependency cycle detected: {cycleDescription}");
    }

    // =====================================================================
    // §17 mutation / actionability proofs — the critical rule predicates are
    // fed deliberately-violating synthetic graphs to prove they are not vacuous.
    // =====================================================================

    /// <summary>
    /// Verifies that mutation proof provider to core forbidden edge is detected.
    /// </summary>
    [TestMethod]
    public void MutationProof_ProviderToCore_ForbiddenEdgeIsDetected() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Abstractions"] = ["System.Runtime"],
            ["Jeninnet.Localization.Core"] = ["Jeninnet.Localization.Abstractions"],
            ["Jeninnet.Localization.Providers.Json"] = ["Jeninnet.Localization.Abstractions", "Jeninnet.Localization.Core"]
        };

        var violations = FindProviderToCoreViolations(graph);

        violations.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Providers.Json"],
            "[Architecture] a deliberate Providers → Core edge must be detected");
    }

    /// <summary>
    /// Verifies that mutation proof provider to core compliant graph yields no violations.
    /// </summary>
    [TestMethod]
    public void MutationProof_ProviderToCore_CompliantGraphYieldsNoViolations() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Abstractions"] = ["System.Runtime"],
            ["Jeninnet.Localization.Core"] = ["Jeninnet.Localization.Abstractions"],
            ["Jeninnet.Localization.Providers.Json"] = ["Jeninnet.Localization.Abstractions"]
        };

        var violations = FindProviderToCoreViolations(graph);

        violations.Should().BeEmpty(
            "[Architecture] a compliant graph must yield no Providers → Core violations");
    }

    /// <summary>
    /// Verifies that mutation proof dependency cycle is detected.
    /// </summary>
    [TestMethod]
    public void MutationProof_DependencyCycle_IsDetected() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Providers.Json"] = ["Jeninnet.Localization.Providers.Xml"],
            ["Jeninnet.Localization.Providers.Xml"] = ["Jeninnet.Localization.Providers.Json"]
        };

        var cycle = FindFirstDependencyCycle(graph);

        Assert.IsNotNull(cycle, "[Architecture] a deliberate two-node cycle must be detected");
        cycle.Length.Should().Be(3, "[Architecture] the detected cycle must span Json → Xml → Json");
    }

    /// <summary>
    /// Verifies that mutation proof dependency cycle compliant graph yields none.
    /// </summary>
    [TestMethod]
    public void MutationProof_DependencyCycle_CompliantGraphYieldsNone() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Abstractions"] = ["System.Runtime"],
            ["Jeninnet.Localization.Providers.Json"] = ["Jeninnet.Localization.Abstractions"]
        };

        var cycle = FindFirstDependencyCycle(graph);

        Assert.IsNull(cycle, "[Architecture] a DAG must not report a cycle");
    }

    /// <summary>
    /// Verifies that mutation proof integration exception invalid core edge is detected.
    /// </summary>
    [TestMethod]
    public void MutationProof_IntegrationException_InvalidCoreEdgeIsDetected() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Integrations.DataAnnotations"] = ["Microsoft.Extensions.Localization.Abstractions", "Jeninnet.Localization.Core"],
            ["Jeninnet.Localization.Integrations.Mvc.Validation"] = ["Jeninnet.Localization.Core"]
        };

        var violations = FindNonMvcIntegrationCoreReferences(graph);

        violations.Should().BeEquivalentTo(
            ["Jeninnet.Localization.Integrations.DataAnnotations"],
            "[Architecture] a deliberate non-Mvc integration → Core edge must be detected; the Mvc.Validation exception must not be reported");
    }

    /// <summary>
    /// Verifies that mutation proof self reference is detected as cycle.
    /// </summary>
    [TestMethod]
    public void MutationProof_SelfReference_IsDetectedAsCycle() {
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal) {
            ["Jeninnet.Localization.Providers.Json"] = ["Jeninnet.Localization.Providers.Json"]
        };

        var cycle = FindFirstDependencyCycle(graph);

        Assert.IsNotNull(cycle, "[Architecture] a self-reference is a cycle and must be detected (self-reference was previously guarded separately)");
    }

    // =====================================================================
    // Architecture boundary invariants (§8, §9.2, §15, §17, §18, §19).
    // =====================================================================

    /// <summary>
    /// Verifies that the root global usings file is removed.
    /// </summary>
    [TestMethod]
    public void RootGlobalUsingsCs_IsRemoved() =>
        File.Exists(Path.Combine(_repoRoot.Value, "GlobalUsings.cs"))
            .Should()
            .BeFalse("[Architecture] the obsolete root GlobalUsings.cs design artifact must be removed");

    /// <summary>
    /// Verifies that the empty Resources project is removed.
    /// </summary>
    [TestMethod]
    public void ResourcesProject_IsRemoved() =>
        _csprojPaths.Value
                    .ContainsKey("Jeninnet.Localization.Resources")
                    .Should().BeFalse("[Architecture] the empty Jeninnet.Localization.Resources project must be removed");

    /// <summary>
    /// Verifies that assertions is consumed as an external package.
    /// </summary>
    [TestMethod]
    public void Assertions_IsConsumedAsExternalPackage() {
        _csprojPaths.Value.ContainsKey("Jeninnet.Assertions").Should().BeFalse(
            "[Architecture] Assertions lives in its own standalone repository and must not be a local project here");

        _csprojPaths.Value.ContainsKey("Jeninnet.Assertions.Tests").Should().BeFalse(
            "[Architecture] the Assertions dedicated test project moved with it to its standalone repository");

        var localizationsTests = _csprojPaths.Value["Jeninnet.Localization.Tests"];
        var testCsproj = XDocument.Load(localizationsTests);
        testCsproj.Descendants("ProjectReference")
                  .Attributes("Include")
                  .Select(static a => a.Value)
                  .Should().NotContain(static value => value.Contains("Jeninnet.Assertions", StringComparison.Ordinal),
                      "[Architecture] Jeninnet.Localization.Tests must consume Assertions as an external NuGet package, not a project reference");
        testCsproj.Descendants("PackageReference")
                  .Attributes("Include")
                  .Select(static a => a.Value)
                  .Should().Contain("Jeninnet.Assertions",
                      "[Architecture] Jeninnet.Localization.Tests must reference the Assertions NuGet package");
    }

    /// <summary>
    /// Verifies that no production project references test assemblies.
    /// </summary>
    [TestMethod]
    public void ProductionProjects_DoNotReferenceTestAssemblies() {
        var testProjectNames = new[] { "Jeninnet.Localization.Tests" };

        foreach(var csproj in Directory.EnumerateFiles(_repoRoot.Value, "*.csproj", SearchOption.AllDirectories)) {
            if(testProjectNames.Contains(Path.GetFileNameWithoutExtension(csproj), StringComparer.Ordinal)) {
                continue;
            }

            foreach(var testName in testProjectNames) {
                File.ReadAllText(csproj).Contains($"{testName}.csproj", StringComparison.Ordinal).Should().BeFalse(
                    $"[Architecture] production project {csproj} must not reference the test/assertion project {testName}");
            }
        }
    }

    /// <summary>
    /// Verifies that EF Core dependencies are confined to the EF provider boundary.
    /// </summary>
    [TestMethod]
    public void EfCoreDependencies_AreConfinedToEfProviderBoundary() {
        const string efProvider = "Jeninnet.Localization.Providers.Database.EntityFrameworkCore";
        var nonEfAssemblies = _expectedLibraryAssemblies
            .Where(static name => name != efProvider)
            .ToArray();

        nonEfAssemblies.Should().HaveCount(_expectedLibraryAssemblies.Length - 1,
            "the EF confinement scan must cover every library assembly except the EF store provider");

        foreach(var name in nonEfAssemblies) {
            var referenced = ReferencedNames(LoadAssembly(name));
            referenced.Should().NotContain("Microsoft.EntityFrameworkCore",
                $"[Architecture] EF Core must be confined to {efProvider}; {name} must not reference Microsoft.EntityFrameworkCore");
            referenced.Should().NotContain("Microsoft.EntityFrameworkCore.SqlServer",
                $"[Architecture] EF Core must be confined to {efProvider}; {name} must not reference Microsoft.EntityFrameworkCore.SqlServer");
        }
    }

    /// <summary>
    /// Verifies that the solution graph has the final boundary paths.
    /// </summary>
    [TestMethod]
    public void SolutionGraph_HasFinalBoundaryPaths() {
        var slnx = File.ReadAllText(Path.Combine(_repoRoot.Value, SLN_FILE_NAME));

        slnx.Contains("implementations").Should().BeFalse("[Architecture] the solution must contain no implementations/ paths");
        slnx.Contains("Localization.Resources").Should().BeFalse("[Architecture] the solution must not reference the removed Resources project");
        slnx.Contains("Jeninnet.Localization.Providers.Database.csproj").Should().BeFalse("[Architecture] the solution must not reference the former Providers.Database project path");
        slnx.Contains("Providers.Database/Jeninnet.Localization.Providers.Database.csproj").Should().BeFalse("[Architecture] no stale EF store path remains");

        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.DataAnnotations/", "[Architecture] DataAnnotations integration in the solution");
        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.Mvc.Validation/", "[Architecture] Mvc.Validation integration in the solution");
        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.Blazor/", "Blazor integration in the solution");
        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.Diagnostics/", "Diagnostics integration in the solution");
        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.JavaScript/", "JavaScript integration in the solution");
        slnx.Should().Contain("integrations/Jeninnet.Localization.Integrations.RequestLocalization/", "request-localization bridge in the solution");
        slnx.Should().Contain("src/interchange/Jeninnet.Localization.Interchange.Abstractions/", "the interchange family contract home in the solution");
        slnx.Should().Contain("src/interchange/Jeninnet.Localization.Interchange.Csv/", "the CSV interchange in the solution");
        slnx.Should().Contain("src/interchange/Jeninnet.Localization.Interchange.ResX/", "the ResX interchange in the solution");
        slnx.Should().Contain("src/interchange/Jeninnet.Localization.Interchange.Po/", "the PO interchange in the solution");
        slnx.Should().Contain("Jeninnet.Localization.Providers.Database.EntityFrameworkCore.csproj", "[Architecture] EF Core store provider in the solution");
        slnx.Contains("src/testing/", StringComparison.Ordinal).Should().BeFalse("[Architecture] the solution must not list Assertions projects (they moved to the standalone repository)");
    }

    // =====================================================================
    // Architecture §10 relocations (prompt §14.3) — enforceable, deferred.
    // =====================================================================

    /// <summary>
    /// Verifies that storage contracts must live in abstractions.
    /// </summary>
    [TestMethod]
    public void StorageContracts_MustLiveInAbstractions() {
        var abstractionsTypeNames = LoadAssembly("Jeninnet.Localization.Abstractions").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);
        var coreTypeNames = LoadAssembly("Jeninnet.Localization.Core").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach(var expected in new[] { "ILocalizationStore", "ILocalizationResourceSet", "LocalizedStringEntry", "LocalizedStringEntryExtensions" }) {
            abstractionsTypeNames.Contains(expected).Should().BeTrue(
                $"[Architecture] {expected} must be declared in Abstractions");
            coreTypeNames.Contains(expected).Should().BeFalse(
                $"[Architecture] {expected} must not remain in Core");
        }
    }

    /// <summary>
    /// Verifies that provider utilities must live in abstractions.
    /// </summary>
    [TestMethod]
    public void ProviderUtilities_MustLiveInAbstractions() {
        var coreTypeNames = LoadAssembly("Jeninnet.Localization.Core").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);
        var abstractionsTypeNames = LoadAssembly("Jeninnet.Localization.Abstractions").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach(var expected in new[] { "MessageProviderBase", "CultureAwareMessageProvider" }) {
            abstractionsTypeNames.Contains(expected).Should().BeTrue(
                $"[Architecture] {expected} must be declared in Abstractions");
            coreTypeNames.Contains(expected).Should().BeFalse(
                $"[Architecture] {expected} must not remain in Core");
        }
    }

    /// <summary>
    /// Verifies that core must not retain relocation type forwarders.
    /// </summary>
    [TestMethod]
    public void Core_MustNotRetainRelocationTypeForwarders() {
        var forwardedTypes = LoadAssembly("Jeninnet.Localization.Core")
            .GetForwardedTypes()
            .ToHashSet();

        foreach(var relocated in new[] { typeof(MessageProviderBase), typeof(CultureAwareMessageProvider) }) {
            forwardedTypes.Contains(relocated).Should().BeFalse(
                $"[Architecture] Core must not retain a type-forwarder to Abstractions for {relocated.Name}; the canonical-only policy forbids type-forwarders");
        }
    }

    /// <summary>
    /// Verifies that core does not define data annotations or mvc metadata types.
    /// </summary>
    [TestMethod]
    public void Core_DoesNotDefineDataAnnotationsOrMvcMetadataTypes() {
        var coreTypeNames = LoadAssembly("Jeninnet.Localization.Core").GetTypes()
            .Select(static t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        coreTypeNames.Contains("LocalizedRequiredAttribute").Should().BeFalse(
            "[Architecture] the DataAnnotations attribute was extracted to integrations/…Integrations.DataAnnotations");
        coreTypeNames.Contains("LocalizedValidationMetadataProvider").Should().BeFalse(
            "[Architecture] the MVC metadata provider was extracted to integrations/…Integrations.Mvc.Validation");
    }

    // =====================================================================
    // Architecture §20 namespace/assembly/package identity.
    /// <summary>
    /// Verifies that assembly identity root namespace matches assembly name.
    /// </summary>
    [TestMethod]
    public void AssemblyIdentity_RootNamespaceMatchesAssemblyName() {
        foreach(var name in _expectedLibraryAssemblies) {
            var assembly = LoadAssembly(name);

            var offenders = assembly.GetTypes()
                .Where(static t => !string.IsNullOrEmpty(t.Namespace))
                .Select(static t => t.Namespace!)
                .Distinct()
                .Where(ns => ns != GENERATED_REGEX_NAMESPACE &&
                              !ns.StartsWith(COVERLET_INSTRUMENTATION_NAMESPACE_PREFIX, StringComparison.Ordinal) &&
                              !_compilerEmbeddedNamespaces.Contains(ns, StringComparer.Ordinal) &&
                              ns != name &&
                              !ns.StartsWith(name + ".", StringComparison.Ordinal));

            offenders.Should().BeEmpty(
                $"[Architecture] every namespace in {name} must be {name} or a sub-namespace of it (found: {string.Join(", ", offenders)}; the {GENERATED_REGEX_NAMESPACE}, compiler-embedded attribute, and coverage-instrumentation namespaces are excluded)");
        }
    }

    /// <summary>
    /// Verifies that library identity project assembly root package are is unified.
    /// </summary>
    [TestMethod]
    public void LibraryIdentity_ProjectAssemblyRootPackageAreUnified() {
        var buildProps = File.ReadAllText(Path.Combine(_repoRoot.Value, "Directory.Build.props"));
        buildProps.Should().Contain("<PackageId>$(MSBuildProjectName)</PackageId>",
            "[Architecture] Directory.Build.props ties package identity to the project name");
        buildProps.Should().Contain("<Version>1.0.0-preview.1</Version>",
            "[Architecture] the single solution-wide version is centralized in Directory.Build.props");

        foreach(var name in _expectedLibraryAssemblies) {
            var csprojPath = _csprojPaths.Value[name];
            Path.GetFileNameWithoutExtension(csprojPath).Should().Be(name,
                $"[Architecture] {name} must be produced by a project named after the assembly");

            var doc = XDocument.Load(csprojPath);
            doc.Descendants("Version").Select(static e => e.Value.Trim()).Should().BeEmpty(
                $"[Architecture] {name} must not declare a per-project Version (the solution-wide version lives in Directory.Build.props)");

            foreach(var identityProperty in new[] { "AssemblyName", "RootNamespace", "PackageId" }) {
                var declared = doc.Descendants(identityProperty).Select(static e => e.Value.Trim()).FirstOrDefault();
                if(declared?.Length > 0) {
                    declared.Should().Be(name,
                        $"[Architecture] {name} must not override {identityProperty} away from its identity (project name == assembly name == root namespace == package id)");
                }
            }
        }
    }

    /// <summary>
    /// Verifies that provider options must not expose default culture or inherit core fallback options.
    /// </summary>
    [TestMethod]
    public void ProviderOptions_MustNotExposeDefaultCultureOrInheritCoreFallbackOptions() {
        var providerOptionsTypes = new[] {
            typeof(MemoryLocalizationOptions),
            typeof(JsonLocalizationOptions),
            typeof(XmlLocalizationOptions),
            typeof(ResXLocalizationOptions),
            typeof(DatabaseLocalizationOptions),
            typeof(MySqlLocalizationOptions),
            typeof(PostgreSqlLocalizationOptions),
            typeof(SqliteLocalizationOptions),
            typeof(SqlServerLocalizationOptions),
            typeof(AzureTranslationOptions),
            typeof(GoogleTranslationOptions),
            typeof(PseudoLocalizationOptions),
        };

        foreach(var type in providerOptionsTypes) {
            type.GetProperty("DefaultCulture").Should().BeNull(
                $"provider options {type.Name} must not expose DefaultCulture: fallback-culture ownership belongs exclusively to the localizer (LocalizationOptions)");
            typeof(LocalizationOptions).IsAssignableFrom(type).Should().BeFalse(
                $"provider options {type.Name} must not derive from LocalizationOptions: provider options must not imply fallback ownership");
        }

        typeof(LocalizationOptions).GetProperty("DefaultCulture").Should().NotBeNull(
            "LocalizationOptions.DefaultCulture remains the single authoritative fallback-culture configuration");
    }

    /// <summary>
    /// Verifies that r8 canonical contracts carry optional final cancellation token.
    /// </summary>
    [TestMethod]
    public void R8_CanonicalContracts_CarryOptionalFinalCancellationToken() {
        var providerMethod = typeof(IMessageProvider).GetMethod("GetMessageTemplateAsync");
        providerMethod.Should().NotBeNull();
        var providerParameters = providerMethod!.GetParameters();
        providerParameters.Should().HaveCount(3, "the canonical provider lookup carries the optional cancellation token");
        providerParameters[2].ParameterType.Should().Be(typeof(CancellationToken));
        providerParameters[2].HasDefaultValue.Should().BeTrue();
        typeof(IMessageProvider).GetMethods().Count(static method => method.Name == "GetMessageTemplateAsync")
            .Should().Be(1, "no compatibility overload may be introduced for the canonical provider lookup");

        var localizerMethods = typeof(IMessageLocalizer).GetMethods()
            .Where(static method => method.Name == "LocalizeAsync")
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();
        localizerMethods.Should().HaveCount(2, "exactly the culture-aware and culture-less canonical overloads exist");
        foreach(var method in localizerMethods) {
            var parameters = method.GetParameters();
            parameters[^1].ParameterType.Should().Be(typeof(CancellationToken));
            parameters[^1].HasDefaultValue.Should().BeTrue();
        }
    }

    /// <summary>
    /// Verifies that r8 canonical provider and localizer implementations use uniform final optional token.
    /// </summary>
    [TestMethod]
    public void R8_CanonicalProviderAndLocalizerImplementations_UseUniformFinalOptionalToken() {
        var providerTypes = _expectedLibraryAssemblies
            .SelectMany(assemblyName => LoadAssembly(assemblyName).GetTypes())
            .Where(static type => type is { IsClass: true, IsAbstract: false } && typeof(IMessageProvider).IsAssignableFrom(type))
            .ToArray();
        providerTypes.Should().NotBeEmpty("the canonical provider implementations must be discoverable");

        foreach(var type in providerTypes) {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(static method => method.Name == "GetMessageTemplateAsync" && !method.IsSpecialName)
                .ToArray();
            methods.Should().ContainSingle(static _ => true, $"[R8] provider {type.Name} must implement the single canonical lookup signature");
            var parameters = methods[0].GetParameters();
            parameters[^1].ParameterType.Should().Be(typeof(CancellationToken), $"[R8] provider {type.Name} must accept the canonical cancellation token");
            parameters[^1].HasDefaultValue.Should().BeTrue($"[R8] the token is optional to callers for provider {type.Name}");
        }

        var localizerType = typeof(IMessageLocalizer).Assembly.GetType("Jeninnet.Localization.Core.DefaultMessageLocalizer")
            ?? LoadAssembly("Jeninnet.Localization.Core").GetType("Jeninnet.Localization.Core.DefaultMessageLocalizer");
        localizerType.Should().NotBeNull();
        var localizerMethods = localizerType!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(static method => method.Name == "LocalizeAsync")
            .ToArray();
        localizerMethods.Should().HaveCount(2, "[R8] the default localizer implements exactly the two canonical overloads");
        foreach(var method in localizerMethods) {
            method.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken), $"[R8] localizer method {method} must accept the canonical token");
            method.GetParameters()[^1].HasDefaultValue.Should().BeTrue("[R8] the token is optional to callers");
        }
    }

    /// <summary>
    /// Verifies that r8 azure shared work state is provider owned and not part of the public surface.
    /// </summary>
    [TestMethod]
    public void R8_AzureSharedWorkState_IsProviderOwnedAndNotPartOfThePublicSurface() {
        //  Model C: shared-work bookkeeping is provider-owned; no caller-visible state exists.
        var declaredPublic = typeof(AzureTranslationProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Event)
            .Select(static member => member.Name)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new HashSet<string>(StringComparer.Ordinal) {
            "GetMessageTemplateAsync",
            "GetMessageTemplatesAsync",
            "PreloadAsync",
            "RefreshAsync",
            "Supports",
            "TranslationAcquired",
            "AcquisitionFailed",
            "get_TranslationAcquired",
            "add_TranslationAcquired",
            "remove_TranslationAcquired",
            "get_AcquisitionFailed",
            "add_AcquisitionFailed",
            "remove_AcquisitionFailed",
        };

        declaredPublic.Where(name => !expected.Contains(name)).Should().BeEmpty(
            "Azure shared-work, cache, and generation state must remain provider-owned internals, never public surface");
    }

    /// <summary>
    /// Verifies that r8 synchronous framework bridges remain tokenless.
    /// </summary>
    [TestMethod]
    public void R8_SynchronousFrameworkBridges_RemainTokenless() {
        var tokenlessMembers = new (Type Type, string Member, BindingFlags Flags)[] {
            (typeof(CustomStringLocalizer), "GetAllStrings", BindingFlags.Public | BindingFlags.Instance),
            (typeof(CustomStringLocalizerFactory), "Create", BindingFlags.Public | BindingFlags.Instance),
            (typeof(DefaultMessageFormatter), "Format", BindingFlags.Public | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedStringLengthAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedMinLengthAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedMaxLengthAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRangeAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRegularExpressionAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedCompareAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedEmailAddressAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedPhoneAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedUrlAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedFileExtensionsAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedCreditCardAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
            (typeof(Jeninnet.Localization.Integrations.DataAnnotations.LocalizedCustomValidationAttribute), "IsValid", BindingFlags.NonPublic | BindingFlags.Instance),
        };

        foreach(var (type, member, flags) in tokenlessMembers) {
            foreach(var method in type.GetMethods(flags).Where(method => method.Name == member)) {
                method.GetParameters().Should().NotContain(
                    static parameter => parameter.ParameterType == typeof(CancellationToken),
                    $"[R8] the framework-imposed synchronous member {type.Name}.{method.Name} must remain tokenless");
            }
        }

        // The synchronous IStringLocalizer indexers are the primary bridge and must not pretend to carry cancellation.
        foreach(var indexer in typeof(CustomStringLocalizer).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(static property => property.GetIndexParameters().Length > 0)) {
            indexer.GetIndexParameters().Should().NotContain(static parameter => parameter.ParameterType == typeof(CancellationToken));
        }
    }

    /// <summary>
    /// Verifies that r8 token bearing contracts are not modified by cancellation work.
    /// </summary>
    [TestMethod]
    public void R8_TokenBearingContracts_AreNotModifiedByCancellationWork() {
        var batchMethod = typeof(IMultiKeyMessageProvider).GetMethod("GetMessageTemplatesAsync");
        batchMethod.Should().NotBeNull();
        batchMethod!.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken), "[R8] the batch contract keeps its existing token");
        batchMethod.GetParameters()[^1].HasDefaultValue.Should().BeTrue();

        typeof(IPreloadableMessageProvider).GetMethod("PreloadAsync")!.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken));
        typeof(IRefreshableMessageProvider).GetMethod("RefreshAsync")!.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken));

        var storeMethod = typeof(ILocalizationStore).GetMethod("GetStringAsync");
        storeMethod.Should().NotBeNull();
        storeMethod!.GetParameters()[^1].ParameterType.Should().Be(typeof(CancellationToken), "[R8] the store contract keeps its existing token");
    }

    /// <summary>
    /// Verifies that WS19 providers do not retain live mutable options.
    /// </summary>
    [TestMethod]
    public void WS19_Providers_DoNotRetainLiveMutableOptions() {
        var optionsTypes = new[] {
            typeof(MemoryLocalizationOptions),
            typeof(JsonLocalizationOptions),
            typeof(XmlLocalizationOptions),
            typeof(ResXLocalizationOptions),
            typeof(DatabaseLocalizationOptions),
            typeof(AzureTranslationOptions),
            typeof(GoogleTranslationOptions),
        };

        foreach(var type in CapabilityProviderTypes()) {
            for(var current = type; current is not null && current != typeof(object); current = current.BaseType) {
                foreach(var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                    optionsTypes.Should().NotContain(
                        optionsType => optionsType.IsAssignableFrom(field.FieldType),
                        $"provider {type.Name} must not retain live options (field {current.Name}.{field.Name})");
                }

                foreach(var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                    optionsTypes.Should().NotContain(
                        optionsType => optionsType.IsAssignableFrom(property.PropertyType),
                        $"provider {type.Name} must not retain live options (property {current.Name}.{property.Name})");
                }
            }
        }
    }

    /// <summary>
    /// Verifies that WS19 providers do not implement disposal.
    /// </summary>
    [TestMethod]
    public void WS19_Providers_DoNotImplementDisposal() {
        foreach(var type in CapabilityProviderTypes()) {
            typeof(IDisposable).IsAssignableFrom(type).Should().BeFalse($"provider {type.Name} must not implement IDisposable (no disposal lifecycle exists)");
            typeof(IAsyncDisposable).IsAssignableFrom(type).Should().BeFalse($"provider {type.Name} must not implement IAsyncDisposable (no disposal lifecycle exists)");
        }
    }

    /// <summary>
    /// Performs the capability provider types operation.
    /// </summary>
    /// <returns>The capability provider types sequence.</returns>
    private static IEnumerable<Type> CapabilityProviderTypes() =>
        _expectedLibraryAssemblies
            .SelectMany(assemblyName => LoadAssembly(assemblyName).GetTypes())
            .Where(static type => type is { IsClass: true, IsAbstract: false } && typeof(IMessageProvider).IsAssignableFrom(type));

    // =====================================================================
    // Provider/file-provider behavior tests (unchanged — retained verbatim).
    // =====================================================================

    /// <summary>
    /// Verifies that json provider canonical resource matches file content.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_CanonicalResourceMatchesFileContentAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"REQUIRED":"The field is required.","EMAIL":"Email is invalid."}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var options = new JsonLocalizationOptions { FilePath = tempFile };
            var provider = new JsonMessageProvider(options);

            _ = await provider.GetMessageTemplateAsync("REQUIRED", CultureInfo.InvariantCulture, CancellationToken.None);

            provider.Resource.Should().NotBeNull();
            provider.Resource!.Entries.Should().HaveCount(2);
            provider.Resource.Entries.Should().Contain(static e => e.MessageKey == "REQUIRED" && e.Template == "The field is required.");
            provider.Resource.Entries.Should().Contain(static e => e.MessageKey == "EMAIL" && e.Template == "Email is invalid.");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider canonical resource matches file content.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_CanonicalResourceMatchesFileContentAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(
                new XElement("messages",
                    new XElement("REQUIRED", "The field is required."),
                    new XElement("EMAIL", "Email is invalid.")
                )
            );
            doc.Save(tempFile);

            var options = new XmlLocalizationOptions { FilePath = tempFile };
            var provider = new XmlMessageProvider(options);

            _ = await provider.GetMessageTemplateAsync("REQUIRED", CultureInfo.InvariantCulture, CancellationToken.None);

            provider.Resource.Should().NotBeNull();
            provider.Resource!.Entries.Should().HaveCount(2);
            provider.Resource.Entries.Should().Contain(static e => e.MessageKey == "REQUIRED" && e.Template == "The field is required.");
            provider.Resource.Entries.Should().Contain(static e => e.MessageKey == "EMAIL" && e.Template == "Email is invalid.");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider cache matches canonical resource.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_CacheMatchesCanonicalResourceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"A":"1","B":"2"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            var resultA = await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);
            var resultB = await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None);

            resultA.Should().Be(provider.Resource!.Entries.First(static e => e.MessageKey == "A").Template);
            resultB.Should().Be(provider.Resource.Entries.First(static e => e.MessageKey == "B").Template);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider cache matches canonical resource.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_CacheMatchesCanonicalResourceAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(
                new XElement("messages",
                    new XElement("A", "1"),
                    new XElement("B", "2")
                )
            );
            doc.Save(tempFile);

            var provider = new XmlMessageProvider(tempFile);

            var resultA = await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None);
            var resultB = await provider.GetMessageTemplateAsync("B", CultureInfo.InvariantCulture, CancellationToken.None);

            resultA.Should().Be(provider.Resource!.Entries.First(static e => e.MessageKey == "A").Template);
            resultB.Should().Be(provider.Resource.Entries.First(static e => e.MessageKey == "B").Template);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that json provider resource includes all keys.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_ResourceIncludesAllKeysAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            const string json = """{"K1":"V1","K2":"V2","K3":"V3"}""";
            await File.WriteAllTextAsync(tempFile, json, TestContext?.CancellationToken ?? CancellationToken.None);

            var provider = new JsonMessageProvider(tempFile);

            _ = await provider.GetMessageTemplateAsync("K1", CultureInfo.InvariantCulture, CancellationToken.None);

            provider.Resource!.Entries.Select(static e => e.MessageKey).Should().BeEquivalentTo("K1", "K2", "K3");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that xml provider resource includes all keys.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_ResourceIncludesAllKeysAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            var doc = new XDocument(
                new XElement("messages",
                    new XElement("K1", "V1"),
                    new XElement("K2", "V2"),
                    new XElement("K3", "V3")
                )
            );
            doc.Save(tempFile);

            var provider = new XmlMessageProvider(tempFile);

            _ = await provider.GetMessageTemplateAsync("K1", CultureInfo.InvariantCulture, CancellationToken.None);

            provider.Resource!.Entries.Select(static e => e.MessageKey).Should().BeEquivalentTo("K1", "K2", "K3");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that sqlite provider uses storage types directly.
    /// </summary>
    [TestMethod]
    public void SqliteProvider_UsesStorageTypesDirectly() {
        var sqliteAssembly = typeof(SqliteMessageProvider).Assembly;
        var customEntities = sqliteAssembly.GetTypes()
            .Where(static t => t is { IsClass: true, IsPublic: true } &&
                        (t.Name.EndsWith("Entity", StringComparison.Ordinal) ||
                         t.Name.EndsWith("Dto", StringComparison.Ordinal) ||
                         t.Name.EndsWith("Entry", StringComparison.Ordinal)));

        customEntities.Should().BeEmpty("Sqlite provider must not define custom entity/DTO types — uses Storage types directly");
    }

    /// <summary>
    /// Verifies that database providers must use parameterized queries.
    /// </summary>
    [TestMethod]
    public void DatabaseProviders_MustUseParameterizedQueries() {
        // Outcome A: the ADO.NET database providers are guaranteed parameterized by
        // construction — each concrete *MessageProvider inherits the single
        // shared parameterized lookup in DatabaseMessageProviderBase<T> and
        // overrides only EscapeIdentifier. The test proves that shared base exists and
        // that the concrete providers resolve their lookup through it. Actual
        // parameterization (@MessageKey / Parameters.AddWithValue) is verified at unit
        // level by DatabaseExactLookupTests.Provider_UsesParameterizedQueries.
        foreach(var name in _expectedLibraryAssemblies.Where(static n =>
            n.StartsWith("Jeninnet.Localization.Providers.Database.", StringComparison.Ordinal) &&
            !n.EndsWith(".Abstractions", StringComparison.Ordinal) &&
            !n.Contains("Abstractions.", StringComparison.Ordinal))) {

            var providerTypes = LoadAssembly(name).GetTypes()
                .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                                   t.Name.EndsWith("MessageProvider", StringComparison.Ordinal));

            foreach(var providerType in providerTypes) {
                var lookup = providerType.GetMethod("GetMessageTemplateAsync", BindingFlags.Instance | BindingFlags.Public);
                lookup.Should().NotBeNull($"Provider {providerType.Name} must expose GetMessageTemplateAsync");

                var declaringType = lookup!.DeclaringType;
                declaringType.Should().NotBeNull($"The GetMessageTemplateAsync implementation for {providerType.Name} must exist");

                var declaringAssembly = declaringType!.Assembly.GetName().Name;
                declaringAssembly.Should().Be(
"Jeninnet.Localization.Providers.Database.Abstractions",
                    $"Provider {providerType.Name} must resolve GetMessageTemplateAsync through the shared parameterized base DatabaseMessageProviderBase<T>; {providerType.Name} overrides only EscapeIdentifier");
            }
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    // =====================================================================
    // Helpers.
    // =====================================================================

    /// <summary>
    /// Performs the provider assemblies operation.
    /// </summary>
    /// <returns>The provider assemblies sequence.</returns>
    private static IEnumerable<Assembly> ProviderAssemblies() {
        foreach(var name in _expectedLibraryAssemblies.Where(static n => n.StartsWith("Jeninnet.Localization.Providers.", StringComparison.Ordinal))) {
            yield return LoadAssembly(name);
        }
    }

    /// <summary>
    /// Performs the discover capability providers operation.
    /// </summary>
    /// <returns>The discover capability providers sequence.</returns>
    private static IEnumerable<IProviderCapabilities> DiscoverCapabilityProviders() {
        foreach(var assemblyName in new[] { "Jeninnet.Localization.Translation.Azure", "Jeninnet.Localization.Translation.Google" }) {
            foreach(var type in LoadAssembly(assemblyName).GetTypes()
                .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                                   typeof(IProviderCapabilities).IsAssignableFrom(t))) {
                yield return CreateCapabilityProvider(type);
            }
        }
    }

    /// <summary>
    /// Creates capability provider.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The capability provider result.</returns>
    private static IProviderCapabilities CreateCapabilityProvider(Type type) {
        if(type == typeof(AzureTranslationProvider)) {
            using var client = new HttpClient();
            return new AzureTranslationProvider(
                Options.Create(new AzureTranslationOptions { ApiKey = "architecture-test" }),
                client);
        }

        if(type == typeof(GoogleTranslationProvider)) {
            using var client = new HttpClient();
            return new GoogleTranslationProvider(
                Options.Create(new GoogleTranslationOptions { ApiKey = "architecture-test" }),
                client);
        }

        throw new InvalidOperationException($"No test-harness construction is defined for capability Provider {type.FullName}.");
    }

    /// <summary>
    /// Loads assembly.
    /// </summary>
    /// <param name="simpleName">The simple name.</param>
    /// <returns>The assembly result.</returns>
    private static Assembly LoadAssembly(string simpleName) {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
                              .FirstOrDefault(a => a.GetName().Name == simpleName);
        return loaded ?? Assembly.Load(new AssemblyName(simpleName));
    }

    /// <summary>
    /// Performs the referenced names operation.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The referenced names sequence.</returns>
    private static string[] ReferencedNames(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(static r => r.Name ?? string.Empty)];

    /// <summary>
    /// Performs the declared project references operation.
    /// </summary>
    /// <param name="assemblySimpleName">The assembly simple name.</param>
    /// <returns>The declared project references sequence.</returns>
    private static string[] DeclaredProjectReferences(string assemblySimpleName) =>
        DeclaredReferenceItems(assemblySimpleName, "ProjectReference");

    /// <summary>
    /// Performs the declared package references operation.
    /// </summary>
    /// <param name="assemblySimpleName">The assembly simple name.</param>
    /// <returns>The declared package references sequence.</returns>
    private static string[] DeclaredPackageReferences(string assemblySimpleName) =>
        DeclaredReferenceItems(assemblySimpleName, "PackageReference");

    private const string SLN_FILE_NAME = "Jeninnet.Localization.slnx";

    private static readonly Lazy<string> _repoRoot = new(() => {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null) {
            if(File.Exists(Path.Combine(dir.FullName, SLN_FILE_NAME))) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"cannot locate repository root (no {SLN_FILE_NAME} found above {AppContext.BaseDirectory})");
    });

    private static readonly Lazy<Dictionary<string, string>> _csprojPaths = new(() => {
        var root = _repoRoot.Value;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)) {
            var stem = Path.GetFileNameWithoutExtension(csproj);
            map[stem] = csproj;
        }

        return map;
    });

    /// <summary>
    /// Performs the declared reference items operation.
    /// </summary>
    /// <param name="assemblySimpleName">The assembly simple name.</param>
    /// <param name="itemName">The item name.</param>
    /// <returns>The declared reference items sequence.</returns>
    private static string[] DeclaredReferenceItems(string assemblySimpleName, string itemName) {
        if(!_csprojPaths.Value.TryGetValue(assemblySimpleName, out var csprojPath)) {
            throw new InvalidOperationException($"no .csproj found declaring assembly {assemblySimpleName}");
        }

        var doc = XDocument.Load(csprojPath);
        return [.. doc.Descendants(itemName)
                  .Attributes("Include")
                  .Select(static a => a.Value)
                  .Where(static v => !string.IsNullOrWhiteSpace(v))
                  .Select(static v => Path.GetFileNameWithoutExtension(v.Replace('\\', '/')))];
    }

    /// <summary>
    /// Builds the emitted local reference graph as assemblyName → referenced assembly names,
    /// restricted to the library assemblies under test (framework/BCL (Base Class Library) edges are excluded).
    /// </summary>
    /// <param name="assemblyNames">The library assembly simple names that form the graph nodes.</param>
    /// <returns>The local reference graph result.</returns>
    private static Dictionary<string, string[]> LocalReferenceGraph(IEnumerable<string> assemblyNames) {
        var names = assemblyNames.ToArray();
        var nodeSet = names.ToHashSet(StringComparer.Ordinal);

        return names.ToDictionary(
            static name => name,
            name => ReferencedNames(LoadAssembly(name)).Where(nodeSet.Contains).ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The <c>Rule04_Providers_MustNotDependOnCore</c> predicate: returns every provider
    /// assembly that emits a Core reference. Shared with the §17 mutation proofs so the
    /// tested predicate is the production-rule predicate.
    /// </summary>
    /// <param name="graph">The local reference graph of assembly names.</param>
    /// <returns>The provider to core violations sequence.</returns>
    private static string[] FindProviderToCoreViolations(IReadOnlyDictionary<string, string[]> graph) => [.. graph
        .Where(static kv =>
            kv.Key.StartsWith("Jeninnet.Localization.Providers.", StringComparison.Ordinal) &&
            kv.Value.Contains("Jeninnet.Localization.Core"))
        .Select(static kv => kv.Key)];

    /// <summary>
    /// The <c>Rule07_IntegrationException_IsModeledExplicitly</c> predicate: returns every integration
    /// assembly other than Mvc.Validation that emits a Core reference. Shared with the §17 mutation
    /// proofs so the exception modeling is tested by the production-rule predicate.
    /// </summary>
    /// <param name="graph">The local reference graph of assembly names.</param>
    /// <returns>The non mvc integration core references sequence.</returns>
    private static string[] FindNonMvcIntegrationCoreReferences(IReadOnlyDictionary<string, string[]> graph) => [.. graph
        .Where(static kv =>
            kv.Key.StartsWith("Jeninnet.Localization.Integrations.", StringComparison.Ordinal) &&
            kv.Key != "Jeninnet.Localization.Integrations.Mvc.Validation" &&
            kv.Value.Contains("Jeninnet.Localization.Core"))
        .Select(static kv => kv.Key)];

    /// <summary>
    /// The <c>Rule11_NoDependencyCycles</c> predicate: returns the first dependency cycle found
    /// (path of assembly names) or <see langword="null"/> when the local graph is acyclic. A
    /// self-reference is a cycle whose path repeats the node. Shared with the §17 mutation proofs.
    /// </summary>
    /// <param name="graph">The local reference graph of assembly names.</param>
    /// <returns>The first dependency cycle result.</returns>
    private static string[]? FindFirstDependencyCycle(IReadOnlyDictionary<string, string[]> graph) {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();

        foreach(var root in graph.Keys) {
            if(state.TryGetValue(root, out var rootState) && rootState != 0) {
                continue;
            }

            var cycle = TryWalkDependencyGraph(root, graph, state, path);
            if(cycle is not null) {
                return cycle;
            }
        }

        return null;
    }

    /// <summary>
    /// Depth-first walk from <paramref name="node"/> over the dependency graph. Nodes on the
    /// current path are marked 1, fully explored nodes are marked 2, and any back-edge to a
    /// marked-1 node closes a cycle whose path is returned. Returns <see langword="null"/> when
    /// the walk completes without finding a cycle.
    /// </summary>
    /// <param name="node">The node to start the walk from.</param>
    /// <param name="graph">The local reference graph of assembly names.</param>
    /// <param name="state">Per-node walk state shared across the recursive walk.</param>
    /// <param name="path">The current walk path shared across the recursive walk.</param>
    /// <returns>The walk dependency graph result.</returns>
    private static string[]? TryWalkDependencyGraph(
        string node,
        IReadOnlyDictionary<string, string[]> graph,
        Dictionary<string, int> state,
        List<string> path) {
        state[node] = 1;
        path.Add(node);

        foreach(var next in graph[node]) {
            if(state.TryGetValue(next, out var nextState)) {
                if(nextState == 1) {
                    return CompleteCycleFrom(path, next);
                }

                continue;
            }

            if(!graph.ContainsKey(next)) {
                continue;
            }

            var cycle = TryWalkDependencyGraph(next, graph, state, path);
            if(cycle is not null) {
                return cycle;
            }
        }

        state[node] = 2;
        path.RemoveAt(path.Count - 1);
        return null;
    }

    /// <summary>
    /// Builds the cycle path for a back-edge onto <paramref name="next"/>: the current path
    /// slice from the first occurrence of <paramref name="next"/> plus the repeated node.
    /// </summary>
    /// <param name="path">The current walk path.</param>
    /// <param name="next">The back-edge target that closes the cycle.</param>
    /// <returns>The complete cycle from sequence.</returns>
    private static string[] CompleteCycleFrom(List<string> path, string next) {
        var cycleStart = path.IndexOf(next);
        return [.. path.Skip(cycleStart), next];
    }
}
