//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Integration-composition tests for the full family of localized DataAnnotations attributes.
/// Every attribute is exercised through the real composed pipeline
/// (FakeLocalizationStore → CachedLocalizationStoreDecorator → CustomStringLocalizerFactory → IStringLocalizer&lt;T&gt;)
/// to prove end-to-end localized validation, per-culture resolution, missing-localizer failure,
/// and missing-key marker surfacing.
/// </summary>
[TestClass]
public class LocalizedDataAnnotationsIntegrationTests {
    /// <summary>
    /// Verifies that all attributes invalid value without localizer throw configuration error.
    /// </summary>
    [TestMethod]
    public void AllAttributes_InvalidValueWithoutLocalizer_ThrowConfigurationError() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var invalidCases = new (ValidationAttribute Attribute, object Instance, object? Value, Type LocalizerType)[] {
            (new LocalizedStringLengthAttribute(5, "StringLength.Error"), new StringLengthModel(), "toolongtext", typeof(StringLengthModel)),
            (new LocalizedStringLengthAttribute(2, 5, "StringLength.Error"), new TwoArgStringLengthModel(), "toolong", typeof(TwoArgStringLengthModel)),
            (new LocalizedMinLengthAttribute(3, "MinLength.Error"), new MinLengthModel(), "ab", typeof(MinLengthModel)),
            (new LocalizedMaxLengthAttribute(3, "MaxLength.Error"), new MaxLengthModel(), "toolong", typeof(MaxLengthModel)),
            (new LocalizedRangeAttribute(1, 10, "Range.Error"), new RangeModel(), 15, typeof(RangeModel)),
            (new LocalizedRangeAttribute(typeof(double), "1", "10", "Range.Error"), new DoubleRangeModel(), 15.0, typeof(DoubleRangeModel)),
            (new LocalizedRegularExpressionAttribute("^[A-Z]+$", "Regex.Error"), new RegexModel(), "abc123", typeof(RegexModel)),
            (new LocalizedCompareAttribute(nameof(CompareModel.Password), "Compare.Error"), new CompareModel { Password = "aaa" }, "bbb", typeof(CompareModel)),
            (new LocalizedEmailAddressAttribute("Email.Error"), new EmailModel(), "not-an-email", typeof(EmailModel)),
            (new LocalizedPhoneAttribute("Phone.Error"), new PhoneModel(), "!!!", typeof(PhoneModel)),
            (new LocalizedUrlAttribute("Url.Error"), new UrlModel(), "not a url", typeof(UrlModel)),
        };

        foreach(var (attribute, instance, value, localizerType) in invalidCases) {
            var context = new ValidationContext(instance, provider, null);
            var act = () => attribute.GetValidationResult(value, context);

            act.Should().Throw<InvalidOperationException>()
                .Exception.Message.Should().Contain("IStringLocalizer<" + localizerType.Name + ">",
                    $"{attribute.GetType().Name} must throw when the localizer is missing and the value is invalid");
        }
    }

    /// <summary>
    /// Verifies that all attributes valid value without localizer is succeed.
    /// </summary>
    [TestMethod]
    public void AllAttributes_ValidValueWithoutLocalizer_Succeed() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var validCases = new (ValidationAttribute Attribute, object Instance, object? Value)[] {
            (new LocalizedStringLengthAttribute(5, "StringLength.Error"), new StringLengthModel(), "ok"),
            (new LocalizedStringLengthAttribute(2, 5, "StringLength.Error"), new TwoArgStringLengthModel(), "ok"),
            (new LocalizedMinLengthAttribute(3, "MinLength.Error"), new MinLengthModel(), "abc"),
            (new LocalizedMaxLengthAttribute(3, "MaxLength.Error"), new MaxLengthModel(), "ab"),
            (new LocalizedRangeAttribute(1, 10, "Range.Error"), new RangeModel(), 5),
            (new LocalizedRangeAttribute(typeof(double), "1", "10", "Range.Error"), new DoubleRangeModel(), 5.0),
            (new LocalizedRegularExpressionAttribute("^[A-Z]+$", "Regex.Error"), new RegexModel(), "ABC"),
            (new LocalizedCompareAttribute(nameof(CompareModel.Password), "Compare.Error"), new CompareModel { Password = "aaa" }, "aaa"),
            (new LocalizedEmailAddressAttribute("Email.Error"), new EmailModel(), "user@example.com"),
            (new LocalizedPhoneAttribute("Phone.Error"), new PhoneModel(), "+1 (234) 567-890"),
            (new LocalizedUrlAttribute("Url.Error"), new UrlModel(), "https://example.com"),
        };

        foreach(var (attribute, instance, value) in validCases) {
            var context = new ValidationContext(instance, provider, null);
            var result = attribute.GetValidationResult(value, context);

            result.Should().Be(ValidationResult.Success,
                $"{attribute.GetType().Name} must succeed for a valid value without localizer services");
        }
    }

    /// <summary>
    /// Verifies that all attributes missing key uses missing marker.
    /// </summary>
    [TestMethod]
    public void AllAttributes_MissingKeyUsesMissingMarker() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var missingCases = new (ValidationAttribute Attribute, object Instance, object? Value)[] {
            (new LocalizedStringLengthAttribute(5, "Unknown.Key"), new StringLengthModel(), "toolongtext"),
            (new LocalizedStringLengthAttribute(2, 5, "Unknown.Key"), new TwoArgStringLengthModel(), "toolong"),
            (new LocalizedMinLengthAttribute(3, "Unknown.Key"), new MinLengthModel(), "ab"),
            (new LocalizedMaxLengthAttribute(3, "Unknown.Key"), new MaxLengthModel(), "toolong"),
            (new LocalizedRangeAttribute(1, 10, "Unknown.Key"), new RangeModel(), 15),
            (new LocalizedRangeAttribute(typeof(double), "1", "10", "Unknown.Key"), new DoubleRangeModel(), 15.0),
            (new LocalizedRegularExpressionAttribute("^[A-Z]+$", "Unknown.Key"), new RegexModel(), "abc123"),
            (new LocalizedCompareAttribute(nameof(CompareModel.Password), "Unknown.Key"), new CompareModel { Password = "aaa" }, "bbb"),
            (new LocalizedEmailAddressAttribute("Unknown.Key"), new EmailModel(), "not-an-email"),
            (new LocalizedPhoneAttribute("Unknown.Key"), new PhoneModel(), "!!!"),
            (new LocalizedUrlAttribute("Unknown.Key"), new UrlModel(), "not a url"),
        };

        foreach(var (attribute, instance, value) in missingCases) {
            var context = new ValidationContext(instance, provider, null);
            var result = attribute.GetValidationResult(value, context);

            result.Should().NotBeNull($"{attribute.GetType().Name} must fail when the key is missing");
            Assert.IsNotNull(result);
            result.ErrorMessage.Should().Be("[MISSING:Unknown.Key]",
                $"{attribute.GetType().Name} must surface the missing-key marker");
        }
    });

    /// <summary>
    /// Verifies that localized string length attribute formats minimum and maximum.
    /// </summary>
    [TestMethod]
    public void LocalizedStringLengthAttribute_FormatsMinimumAndMaximum() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("StringLengthModel", "en-US", "StringLength.Error", "Length must be between {0} and {1}.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedStringLengthAttribute(2, 5, "StringLength.Error");
        var context = new ValidationContext(new StringLengthModel(), provider, null);

        var result = attribute.GetValidationResult("toolongtext", context);

        result!.ErrorMessage.Should().Be("Length must be between 2 and 5.",
            "the localized message must include the configured minimum and maximum as composite format arguments");
    });

    /// <summary>
    /// Verifies that localized min length attribute formats length argument.
    /// </summary>
    [TestMethod]
    public void LocalizedMinLengthAttribute_FormatsLengthArgument() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("MinLengthModel", "en-US", "MinLength.Error", "Must be at least {0} characters.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedMinLengthAttribute(3, "MinLength.Error");
        var context = new ValidationContext(new MinLengthModel(), provider, null);

        var result = attribute.GetValidationResult("ab", context);

        result!.ErrorMessage.Should().Be("Must be at least 3 characters.",
            "the localized message must include the configured minimum length");
    });

    /// <summary>
    /// Verifies that localized max length attribute formats length argument.
    /// </summary>
    [TestMethod]
    public void LocalizedMaxLengthAttribute_FormatsLengthArgument() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("MaxLengthModel", "en-US", "MaxLength.Error", "Must not exceed {0} characters.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedMaxLengthAttribute(3, "MaxLength.Error");
        var context = new ValidationContext(new MaxLengthModel(), provider, null);

        var result = attribute.GetValidationResult("toolong", context);

        result!.ErrorMessage.Should().Be("Must not exceed 3 characters.",
            "the localized message must include the configured maximum length");
    });

    /// <summary>
    /// Verifies that localized range attribute formats minimum and maximum.
    /// </summary>
    [TestMethod]
    public void LocalizedRangeAttribute_FormatsMinimumAndMaximum() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("RangeModel", "en-US", "Range.Error", "Value must be between {0} and {1}.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedRangeAttribute(1, 10, "Range.Error");
        var context = new ValidationContext(new RangeModel(), provider, null);

        var result = attribute.GetValidationResult(15, context);

        result!.ErrorMessage.Should().Be("Value must be between 1 and 10.",
            "the localized message must include the configured integer bounds");
    });

    /// <summary>
    /// Verifies that localized range attribute type based constructor formats minimum and maximum.
    /// </summary>
    [TestMethod]
    public void LocalizedRangeAttribute_TypeBasedConstructor_FormatsMinimumAndMaximum() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("DoubleRangeModel", "en-US", "Range.Error", "Score must be between {0} and {1}.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedRangeAttribute(typeof(double), "1", "10", "Range.Error");
        var context = new ValidationContext(new DoubleRangeModel(), provider, null);

        var result = attribute.GetValidationResult(15.0, context);

        result!.ErrorMessage.Should().Be("Score must be between 1 and 10.",
            "the type-based RangeAttribute constructor must pass the configured string bounds");
    });

    /// <summary>
    /// Verifies that localized regular expression attribute formats pattern argument.
    /// </summary>
    [TestMethod]
    public void LocalizedRegularExpressionAttribute_FormatsPatternArgument() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("RegexModel", "en-US", "Regex.Error", "Must match the pattern {0}.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedRegularExpressionAttribute("^[A-Z]+$", "Regex.Error");
        var context = new ValidationContext(new RegexModel(), provider, null);

        var result = attribute.GetValidationResult("abc123", context);

        result!.ErrorMessage.Should().Be("Must match the pattern ^[A-Z]+$.",
            "the localized message must include the configured pattern as a composite format argument");
    });

    /// <summary>
    /// Verifies that localized compare attribute uses localized message.
    /// </summary>
    [TestMethod]
    public void LocalizedCompareAttribute_UsesLocalizedMessage() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CompareModel", "en-US", "Compare.Error", "Fields do not match.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCompareAttribute(nameof(CompareModel.Password), "Compare.Error");
        var model = new CompareModel { Password = "aaa" };
        var context = new ValidationContext(model, provider, null);

        var result = attribute.GetValidationResult("bbb", context);

        result!.ErrorMessage.Should().Be("Fields do not match.");
    });

    /// <summary>
    /// Verifies that localized compare attribute equal values returns success.
    /// </summary>
    [TestMethod]
    public void LocalizedCompareAttribute_EqualValues_ReturnsSuccess() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCompareAttribute(nameof(CompareModel.Password), "Compare.Error");
        var model = new CompareModel { Password = "aaa" };
        var context = new ValidationContext(model, provider, null);

        var result = attribute.GetValidationResult("aaa", context);

        result.Should().Be(ValidationResult.Success);
    });

    /// <summary>
    /// Verifies that localized email address attribute respects each validation current culture.
    /// </summary>
    [TestMethod]
    public void LocalizedEmailAddressAttribute_RespectsEachValidationCurrentCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("EmailModel", "en-US", "Email.Error", "Enter a valid email address.");
        store.SetEntry("EmailModel", "fr", "Email.Error", "Saisissez une adresse e-mail valide.");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var attribute = new LocalizedEmailAddressAttribute("Email.Error");
                var context = new ValidationContext(new EmailModel(), provider, null);
                var result = attribute.GetValidationResult("not-an-email", context);
                Assert.IsNotNull(result);
                message = result.ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Enter a valid email address.");
        ValidateUnder("fr").Should().Be("Saisissez une adresse e-mail valide.");
    }

    /// <summary>
    /// Verifies that localized range attribute respects each validation current culture.
    /// </summary>
    [TestMethod]
    public void LocalizedRangeAttribute_RespectsEachValidationCurrentCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("RangeModel", "en-US", "Range.Error", "Value must be between {0} and {1} (US).");
        store.SetEntry("RangeModel", "fr", "Range.Error", "La valeur doit être entre {0} et {1} (FR).");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var attribute = new LocalizedRangeAttribute(1, 10, "Range.Error");
                var context = new ValidationContext(new RangeModel(), provider, null);
                var result = attribute.GetValidationResult(15, context);
                Assert.IsNotNull(result);
                message = result.ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Value must be between 1 and 10 (US).");
        ValidateUnder("fr").Should().Be("La valeur doit être entre 1 et 10 (FR).");
    }

    /// <summary>
    /// Verifies that localized string length attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedStringLengthAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedStringLengthAttribute(5, "StringLength.Error");
        var context = new ValidationContext(new StringLengthModel(), provider, null);

        var act = () => attribute.GetValidationResult("toolongtext", context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<StringLengthModel>");
    }

    /// <summary>
    /// Verifies that localized range attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedRangeAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedRangeAttribute(1, 10, "Range.Error");
        var context = new ValidationContext(new RangeModel(), provider, null);

        var act = () => attribute.GetValidationResult(15, context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<RangeModel>");
    }

    /// <summary>
    /// Verifies that localized email address attribute through framework validator uses localized message per culture.
    /// </summary>
    [TestMethod]
    public void LocalizedEmailAddressAttribute_ThroughFrameworkValidator_UsesLocalizedMessagePerCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("EmailModel", "en-US", "Email.Error", "Invalid email address (en).");
        store.SetEntry("EmailModel", "fr", "Email.Error", "Adresse e-mail invalide (fr).");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var results = new List<ValidationResult>();
                var context = new ValidationContext(new EmailModel { Email = "not-an-email" }, provider, null);
                Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
                results.Should().ContainSingle();
                message = results[0].ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Invalid email address (en).");
        ValidateUnder("fr").Should().Be("Adresse e-mail invalide (fr).");
    }

    /// <summary>
    /// Registers cached store.
    /// </summary>
    /// <param name="store">The localization store.</param>
    /// <param name="services">The service collection.</param>
    /// <returns>The register cached store result.</returns>
    private static MemoryCache RegisterCachedStore(FakeLocalizationStore store, IServiceCollection services) {
        var cache = new MemoryCache(new MemoryCacheOptions());
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton<ILocalizationStore>(sp => new CachedLocalizationStoreDecorator(store, sp.GetRequiredService<IMemoryCache>()));
        services.AddMvcLocalizedValidation();
        return cache;
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

    /// <summary>
    /// Represents a string length model.
    /// </summary>
    private sealed class StringLengthModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedStringLength(5, "StringLength.Error")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a two arg string length model.
    /// </summary>
    private sealed class TwoArgStringLengthModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedStringLength(2, 5, "StringLength.Error")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a min length model.
    /// </summary>
    private sealed class MinLengthModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedMinLength(3, "MinLength.Error")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a max length model.
    /// </summary>
    private sealed class MaxLengthModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedMaxLength(3, "MaxLength.Error")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents a range model.
    /// </summary>
    private sealed class RangeModel {
        /// <summary>
        /// Gets or sets the age.
        /// </summary>
        [LocalizedRange(1, 10, "Range.Error")]
        public int Age { get; set; }
    }

    /// <summary>
    /// Represents a double range model.
    /// </summary>
    private sealed class DoubleRangeModel {
        /// <summary>
        /// Gets or sets the score.
        /// </summary>
        [LocalizedRange(typeof(double), "1", "10", "Range.Error")]
        public double Score { get; set; }
    }

    /// <summary>
    /// Represents a regex model.
    /// </summary>
    private sealed class RegexModel {
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        [LocalizedRegularExpression("^[A-Z]+$", "Regex.Error")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Represents a compare model.
    /// </summary>
    private sealed class CompareModel {
        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the confirm.
        /// </summary>
        [LocalizedCompare(nameof(Password), "Compare.Error")]
        public string? Confirm { get; set; }
    }

    /// <summary>
    /// Represents an email model.
    /// </summary>
    private sealed class EmailModel {
        /// <summary>
        /// Gets or sets the email.
        /// </summary>
        [LocalizedEmailAddress("Email.Error")]
        public string? Email { get; set; }
    }

    /// <summary>
    /// Represents a phone model.
    /// </summary>
    private sealed class PhoneModel {
        /// <summary>
        /// Gets or sets the phone.
        /// </summary>
        [LocalizedPhone("Phone.Error")]
        public string? Phone { get; set; }
    }

    /// <summary>
    /// Represents an url model.
    /// </summary>
    private sealed class UrlModel {
        /// <summary>
        /// Gets or sets the website.
        /// </summary>
        [LocalizedUrl("Url.Error")]
        public string? Website { get; set; }
    }

    /// <summary>
    /// Stores fake localization.
    /// </summary>
    private sealed class FakeLocalizationStore : ILocalizationStore {
        private readonly Dictionary<(string Set, string Culture), Dictionary<string, string>> _data = [];

        /// <summary>
        /// Sets entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        public void SetEntry(string resourceSet, string culture, string key, string value) {
            if(!_data.TryGetValue((resourceSet, culture), out var entries)) {
                entries = [];
                _data[(resourceSet, culture)] = entries;
            }

            entries[key] = value;
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            if(_data.TryGetValue((resourceSet, culture), out var entries)) {
                localizationResourceSet = new FakeResourceSet(resourceSet, culture, entries);
                return true;
            }

            localizationResourceSet = null;
            return false;
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            _data.TryGetValue((resourceSet, culture), out var entries);
            return Task.FromResult<ILocalizationResourceSet?>(
                entries is not null ? new FakeResourceSet(resourceSet, culture, entries) : null);
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
            if(_data.TryGetValue((resourceSet, culture), out var entries) && entries.TryGetValue(name, out var value)) {
                entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(
                _data.TryGetValue((resourceSet, culture), out var entries) && entries.TryGetValue(name, out var value)
                    ? new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value)
                    : LocalizedStringEntry.Missing(resourceSet, culture, name));

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            List<ILocalizationResourceSet> result = [];
            foreach(var ((set, cul), entries) in _data) {
                if(cul.Equals(culture, StringComparison.OrdinalIgnoreCase)) {
                    result.Add(new FakeResourceSet(set, cul, entries));
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();
            if(_data.TryGetValue((resourceSet, culture), out var entries)) {
                foreach(var entry in entries) {
                    result[entry.Key] = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, entry.Key, entry.Value);
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();
            foreach(var ((set, cul), entries) in _data) {
                if(cul.Equals(culture, StringComparison.OrdinalIgnoreCase)) {
                    foreach(var entry in entries) {
                        result[entry.Key] = new LocalizedStringEntry(Guid.NewGuid(), set, cul, entry.Key, entry.Value);
                    }
                }
            }

            return Task.FromResult(result);
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
            var result = _data.Keys.Select(static key => key.Culture).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Test double for a resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The localized entries.</param>
    private sealed class FakeResourceSet(string resourceSet, string culture, Dictionary<string, string> entries) : ILocalizationResourceSet {
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
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(
                entries.TryGetValue(name, out var value)
                    ? new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value)
                    : LocalizedStringEntry.Missing(resourceSet, culture, name));

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();
            foreach(var entry in entries) {
                result[entry.Key] = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, entry.Key, entry.Value);
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
            if(entries.TryGetValue(name, out var value)) {
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

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
