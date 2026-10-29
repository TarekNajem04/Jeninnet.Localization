//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the mvc validation metadata tests.
/// </summary>
[TestClass]
public class MvcValidationMetadataTests {
    /// <summary>
    /// Verifies that display attributes localize through standard mvc metadata pipeline.
    /// </summary>
    [TestMethod]
    public void DisplayAttributes_LocalizeThroughStandardMvcMetadataPipeline() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(DisplayModel), "en-US", "UserName", "Username (en)");
        store.SetEntry(nameof(DisplayModel), "en-US", "UserDescription", "The username description");
        store.SetEntry(nameof(DisplayModel), "en-US", "UserPrompt", "Type the username");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));

        metadata.DisplayName.Should().Be("Username (en)");
        metadata.Description.Should().Be("The username description");
        metadata.Placeholder.Should().Be("Type the username");
    });

    /// <summary>
    /// Verifies that cached metadata evaluates current culture on every access.
    /// </summary>
    [TestMethod]
    public void CachedMetadata_EvaluatesCurrentCultureOnEveryAccess() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(DisplayModel), "en-US", "UserName", "Username (en)");
        store.SetEntry(nameof(DisplayModel), "ar-SY", "UserName", "Username (ar)");

        using var sp = CreateServices(store);
        var provider = sp.GetRequiredService<IModelMetadataProvider>();
        var metadata = provider.GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));

        metadata.DisplayName.Should().Be("Username (en)");

        RunWithCurrentUiCulture("ar-SY", () => metadata.DisplayName.Should().Be("Username (ar)"));
        RunWithCurrentUiCulture("en-US", () => metadata.DisplayName.Should().Be("Username (en)"));
    });

    /// <summary>
    /// Verifies that metadata instance is cached across resolutions.
    /// </summary>
    [TestMethod]
    public void MetadataInstance_IsCachedAcrossResolutions() {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(DisplayModel), "en-US", "UserName", "Username (en)");

        using var sp = CreateServices(store);
        var provider = sp.GetRequiredService<IModelMetadataProvider>();
        var first = provider.GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));
        var second = provider.GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));

        Assert.AreSame(first, second, "the metadata cache must return the same cached metadata instance");
    }
    /// <summary>
    /// Verifies that missing key surfaces canonical missing marker.
    /// </summary>
    [TestMethod]
    public void MissingKey_SurfacesCanonicalMissingMarker() => RunWithCurrentUiCulture("en-US", () => {
        using var sp = CreateServices(new FakeLocalizationStore());
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));

        metadata.DisplayName.Should().Be("[MISSING:UserName]");
        metadata.Description.Should().Be("[MISSING:UserDescription]");
        metadata.Placeholder.Should().Be("[MISSING:UserPrompt]");
    });

    /// <summary>
    /// Verifies that partial display only localizes populated properties.
    /// </summary>
    [TestMethod]
    public void PartialDisplay_OnlyLocalizesPopulatedProperties() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(PartialDisplayModel), "en-US", "OnlyDescription", "A description");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(PartialDisplayModel), nameof(PartialDisplayModel.Name));

        metadata.DisplayName.Should().BeNull();
        metadata.Description.Should().Be("A description");
        metadata.Placeholder.Should().BeNull();
    });

    /// <summary>
    /// Verifies that resource type display is left to the built in resource mechanism.
    /// </summary>
    [TestMethod]
    public void ResourceTypeDisplay_IsLeftToTheBuiltInResourceMechanism() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(ResourceTypeDisplayModel), "en-US", "Skipped", "Skipped-resource");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(ResourceTypeDisplayModel), nameof(ResourceTypeDisplayModel.Name));

        metadata.DisplayName.Should().Be("framework-resource-value",
            "a DisplayAttribute with ResourceType must be resolved by the built-in resource mechanism, not the store-backed localizer");
        Assert.AreNotEqual("Skipped-resource", metadata.DisplayName);
    });

    /// <summary>
    /// Verifies that short name has no standard mvc metadata destination.
    /// </summary>
    [TestMethod]
    public void ShortName_HasNoStandardMvcMetadataDestination() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(ShortNameModel), "en-US", "USN", "USN-resource");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(ShortNameModel), nameof(ShortNameModel.Name));

        metadata.DisplayName.Should().BeNull("ShortName has no DisplayMetadata destination");
        typeof(ModelMetadata).GetProperties().Should()
            .NotContain(static property => property.Name == "ShortName",
                "ModelMetadata exposes no ShortName slot on .NET 10");
    });

    /// <summary>
    /// Verifies that type level display localizes against the model type.
    /// </summary>
    [TestMethod]
    public void TypeLevelDisplay_LocalizesAgainstTheModelType() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(TypeLevelDisplayModel), "en-US", "TypeTitle", "Type title (en)");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForType(typeof(TypeLevelDisplayModel));

        metadata.DisplayName.Should().Be("Type title (en)");
    });

    /// <summary>
    /// Verifies that plain required error message is attached through mvc model validator.
    /// </summary>
    [TestMethod]
    public void PlainRequired_ErrorMessageIsAttachedThroughMvcModelValidator() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(RequiredValidationModel), "en-US", "City", "City is required (en)");

        using var sp = CreateServices(store);
        var validator = sp.GetRequiredService<IObjectModelValidator>();
        var modelState = Validate(validator, sp, new RequiredValidationModel());

        modelState["prefix.City"]!.Errors.Single().ErrorMessage.Should().Be("City is required (en)");
    });

    /// <summary>
    /// Verifies that model level metadata does not fault the container type lookup.
    /// </summary>
    [TestMethod]
    public void ModelLevelMetadata_DoesNotFaultTheContainerTypeLookup() {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(RequiredValidationModel), "en-US", "City", "City is required (en)");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForType(typeof(RequiredValidationModel));

        metadata.ModelType.Should().Be(typeof(RequiredValidationModel));
        metadata.ValidatorMetadata.OfType<RequiredAttribute>().Should().BeEmpty(
            "type-level metadata has no property name to localize and must not fault the ContainerType lookup");
    }

    /// <summary>
    /// Verifies that localized required family is not touched by mvc validation metadata provider.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredFamily_IsNotTouchedByMvcValidationMetadataProvider() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(FamilyRequiredModel), "en-US", "Title.Required", "Title must be set");
        store.SetEntry(nameof(FamilyRequiredModel), "en-US", "Title", "stale message from a competing property-name localization");

        using var sp = CreateServices(store);
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(FamilyRequiredModel), nameof(FamilyRequiredModel.Title));
        var attribute = metadata.ValidatorMetadata.OfType<LocalizedRequiredAttribute>().Single();

        attribute.ErrorMessage.Should().BeNull(
            "the localized family owns its own validation-time localization and must not receive a frozen ErrorMessage");
    });

    /// <summary>
    /// Verifies that localized required family localizes at validation time through request services.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredFamily_LocalizesAtValidationTimeThroughRequestServices() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(FamilyRequiredModel), "en-US", "Title.Required", "Title must be set");
        store.SetEntry(nameof(FamilyRequiredModel), "en-US", "Title", "stale message from a competing property-name localization");

        using var sp = CreateServices(store);
        var validator = sp.GetRequiredService<IObjectModelValidator>();
        var modelState = Validate(validator, sp, new FamilyRequiredModel());

        modelState["prefix.Title"]!.Errors.Single().ErrorMessage.Should().Be("Title must be set");
    });

    /// <summary>
    /// Verifies that registration wires both localized providers after framework metadata providers.
    /// </summary>
    [TestMethod]
    public void Registration_WiresBothLocalizedProvidersAfterFrameworkMetadataProviders() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(new FakeLocalizationStore());
        services.AddControllers();
        services.AddMvcLocalizedValidation();

        using var sp = services.BuildServiceProvider();
        var providers = sp.GetRequiredService<IOptions<MvcOptions>>().Value.ModelMetadataDetailsProviders;

        providers.Count(static provider => provider is LocalizedValidationMetadataProvider).Should().Be(1);
        providers.Count(static provider => provider is LocalizedDisplayMetadataProvider).Should().Be(1);

        var indexOf = providers.Select(static (provider, index) => (Type: provider.GetType().FullName!, Index: index)).ToArray();
        var frameworkDisplayIndex = indexOf.First(static entry => entry.Type == "Microsoft.AspNetCore.Mvc.DataAnnotations.DataAnnotationsMetadataProvider").Index;
        var localizedDisplayIndex = indexOf.First(static entry => entry.Type == typeof(LocalizedDisplayMetadataProvider).FullName).Index;
        var frameworkValidationIndex = indexOf.First(static entry => entry.Type == "Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.DefaultValidationMetadataProvider").Index;
        var localizedValidationIndex = indexOf.First(static entry => entry.Type == typeof(LocalizedValidationMetadataProvider).FullName).Index;

        Assert.IsLessThan(localizedDisplayIndex, frameworkDisplayIndex,
            "the localized display provider must run after the framework's DataAnnotations display mapping so its deferred resolution wins");
        Assert.IsLessThan(localizedValidationIndex, frameworkValidationIndex,
            "the localized validation provider must observe the framework-populated ValidatorMetadata");
    }

    /// <summary>
    /// Verifies that without registration framework literal display metadata remains.
    /// </summary>
    [TestMethod]
    public void WithoutRegistration_FrameworkLiteralDisplayMetadataRemains() {
        var services = new ServiceCollection();
        services.AddControllers();

        using var sp = services.BuildServiceProvider();
        var metadata = sp.GetRequiredService<IModelMetadataProvider>()
                         .GetMetadataForProperty(typeof(DisplayModel), nameof(DisplayModel.Name));

        metadata.DisplayName.Should().Be("UserName");
    }

    /// <summary>
    /// Creates services.
    /// </summary>
    /// <param name="store">The localization store.</param>
    /// <returns>The services result.</returns>
    private static ServiceProvider CreateServices(FakeLocalizationStore store) {
        var services = new ServiceCollection();

        // Register the framework generic localizers (IStringLocalizer<T>) needed by the localized family.
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);

        var cache = new MemoryCache(new MemoryCacheOptions());
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton<ILocalizationStore>(sp => new CachedLocalizationStoreDecorator(store, sp.GetRequiredService<IMemoryCache>()));
        services.AddControllers();
        services.AddMvcLocalizedValidation();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Performs the validate operation.
    /// </summary>
    /// <param name="validator">The validator.</param>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model.</param>
    /// <returns>The validate result.</returns>
    private static ModelStateDictionary Validate(IObjectModelValidator validator, IServiceProvider services, object model) {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        validator.Validate(actionContext, validationState: null, "prefix", model);
        return actionContext.ModelState;
    }

    /// <summary>
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCurrentUiCulture(string cultureName, Action action) {
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            action();
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

#pragma warning disable S3459, S1144 // Metadata-only model shapes: members exist to drive the MVC metadata pipeline through reflection
    /// <summary>
    /// Represents a display model.
    /// </summary>
    private sealed class DisplayModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Display(Name = "UserName", Description = "UserDescription", Prompt = "UserPrompt")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a partial display model.
    /// </summary>
    private sealed class PartialDisplayModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Display(Description = "OnlyDescription")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a resource type display model.
    /// </summary>
    public sealed class ResourceTypeDisplayModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Display(Name = nameof(Skipped), ResourceType = typeof(ResourceTypeDisplayModel))]
        public string? Name { get; set; }

        /// <summary>
        /// Gets the skipped.
        /// </summary>
        public static string Skipped => "framework-resource-value";
    }

    /// <summary>
    /// Represents a short name model.
    /// </summary>
    private sealed class ShortNameModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [Display(ShortName = "USN")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a type level display model.
    /// </summary>
    [Display(Name = "TypeTitle")]
    private sealed class TypeLevelDisplayModel {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public string? Value { get; set; }
    }
#pragma warning restore S3459, S1144

    /// <summary>
    /// Represents a required validation model.
    /// </summary>
    private sealed class RequiredValidationModel {
        /// <summary>
        /// Gets or sets the city.
        /// </summary>
        [Required]
        public string? City { get; set; }
    }

    /// <summary>
    /// Represents a family required model.
    /// </summary>
    private sealed class FamilyRequiredModel {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [LocalizedRequired("Title.Required")]
        public string? Title { get; set; }
    }

    /// <summary>
    /// Stores fake localization.
    /// </summary>
    private sealed class FakeLocalizationStore : ILocalizationStore {
        private readonly object _sync = new();
        private readonly Dictionary<(string Set, string Culture), Dictionary<string, string>> _data = [];
        private readonly Dictionary<(string Set, string Culture), FakeResourceSet> _sets = [];

        /// <summary>
        /// Sets entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        public void SetEntry(string resourceSet, string culture, string key, string value) {
            lock(_sync) {
                if(!_data.TryGetValue((resourceSet, culture), out var entries)) {
                    entries = [];
                    _data[(resourceSet, culture)] = entries;
                    _sets[(resourceSet, culture)] = new FakeResourceSet(resourceSet, culture, this);
                }

                entries[key] = value;
            }
        }

        /// <summary>
        /// Gets data.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The data result.</returns>
        public Dictionary<string, string>? GetData(string resourceSet, string culture) {
            lock(_sync) {
                return _data.TryGetValue((resourceSet, culture), out var entries) ? entries : null;
            }
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default)
            => Task.FromResult<ILocalizationResourceSet?>(GetSet(resourceSet, culture));

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            List<ILocalizationResourceSet> matches;
            lock(_sync) {
                matches = [.. _sets.Where(set => set.Key.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)).Select(static set => (ILocalizationResourceSet)set.Value)];
            }

            return Task.FromResult(matches);
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            localizationResourceSet = GetSet(resourceSet, culture);
            return localizationResourceSet is not null;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
            var set = GetSet(resourceSet, culture);
            return set is null ? Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name)) : set.GetStringAsync(name, cancellationToken);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var set = GetSet(resourceSet, culture);
            return set is null ? [] : await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();

            foreach(var set in GetSetsForCulture(culture)) {
                foreach(var entry in await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false)) {
                    result[entry.Key] = entry.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            var set = GetSet(resourceSet, culture);

            if(set is not null && set.TryGetString(name, out entry)) {
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            List<string> cultures;
            lock(_sync) {
                cultures = [.. _sets.Keys.Select(static key => key.Culture).Distinct(StringComparer.OrdinalIgnoreCase)];
            }

            return Task.FromResult(cultures);
        }

        /// <summary>
        /// Gets set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The set result.</returns>
        private FakeResourceSet? GetSet(string resourceSet, string culture) {
            lock(_sync) {
                return _sets.TryGetValue((resourceSet, culture), out var set) ? set : null;
            }
        }

        /// <summary>
        /// Gets sets for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <returns>The sets for culture sequence.</returns>
        private List<FakeResourceSet> GetSetsForCulture(string culture) {
            lock(_sync) {
                return [.. _sets.Where(set => set.Key.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)).Select(static set => set.Value)];
            }
        }
    }

    /// <summary>
    /// Test double for a resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="store">The localization store.</param>
    private sealed class FakeResourceSet(string resourceSet, string culture, FakeLocalizationStore store) : ILocalizationResourceSet {
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            var data = store.GetData(resourceSet, culture);

            return Task.FromResult(data is not null && data.TryGetValue(name, out var value)
                ? new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value)
                : LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            var data = store.GetData(resourceSet, culture);
            var result = new Dictionary<string, LocalizedStringEntry>();

            if(data is not null) {
                foreach(var entry in data) {
                    result[entry.Key] = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, entry.Key, entry.Value);
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            var data = store.GetData(resourceSet, culture);

            if(data is not null && data.TryGetValue(name, out var value)) {
                entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
