//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Integration-composition tests completing the localized DataAnnotations family with
/// <see cref="LocalizedFileExtensionsAttribute"/>, <see cref="LocalizedCreditCardAttribute"/>,
/// and <see cref="LocalizedCustomValidationAttribute"/>.
/// Every attribute is exercised through the real composed pipeline
/// (FakeLocalizationStore → CachedLocalizationStoreDecorator → CustomStringLocalizerFactory → IStringLocalizer&lt;T&gt;)
/// and most paths run through Validator.TryValidateObject.
/// </summary>
[TestClass]
public class LocalizedDataAnnotationsCompletionTests {
    /// <summary>
    /// Verifies that completion attributes invalid value without localizer throw configuration error.
    /// </summary>
    [TestMethod]
    public void CompletionAttributes_InvalidValueWithoutLocalizer_ThrowConfigurationError() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var invalidCases = new (ValidationAttribute Attribute, object Instance, object? Value, Type LocalizerType)[] {
            (new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" }, new FileExtensionsModel(), "file.gif", typeof(FileExtensionsModel)),
            (new LocalizedCreditCardAttribute("CreditCard.Error"), new CreditCardModel(), "not-a-card", typeof(CreditCardModel)),
            (new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Fail), "Custom.Error"), new CustomValidationModel(), "x", typeof(CustomValidationModel)),
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
    /// Verifies that completion attributes valid value without localizer is succeed.
    /// </summary>
    [TestMethod]
    public void CompletionAttributes_ValidValueWithoutLocalizer_Succeed() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var validCases = new (ValidationAttribute Attribute, object Instance, object? Value)[] {
            (new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" }, new FileExtensionsModel(), "a.PNG"),
            (new LocalizedCreditCardAttribute("CreditCard.Error"), new CreditCardModel(), "4012888888881881"),
            (new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), "Custom.Error"), new CustomValidationModel(), "x"),
        };

        foreach(var (attribute, instance, value) in validCases) {
            var context = new ValidationContext(instance, provider, null);
            var result = attribute.GetValidationResult(value, context);

            result.Should().Be(ValidationResult.Success,
                $"{attribute.GetType().Name} must succeed for a valid value without localizer services");
        }
    }

    /// <summary>
    /// Verifies that completion attributes missing key uses missing marker.
    /// </summary>
    [TestMethod]
    public void CompletionAttributes_MissingKeyUsesMissingMarker() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var missingCases = new (ValidationAttribute Attribute, object Instance, object? Value)[] {
            (new LocalizedFileExtensionsAttribute("Unknown.Key") { Extensions = "png,jpg" }, new FileExtensionsModel(), "file.gif"),
            (new LocalizedCreditCardAttribute("Unknown.Key"), new CreditCardModel(), "not-a-card"),
            (new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Fail), "Unknown.Key"), new CustomValidationModel(), "x"),
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
    /// Verifies that localized file extensions attribute valid extension succeeds.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_ValidExtension_Succeeds() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Only accept {0} files.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new FileExtensionsModel { FileName = "report.PNG" }, provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeTrue(
            "a configured extension must be accepted case-insensitively through the framework pipeline");
        results.Should().BeEmpty();
    });

    /// <summary>
    /// Verifies that localized file extensions attribute invalid extension localized message.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_InvalidExtension_LocalizedMessage() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Only accept {0} files.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new FileExtensionsModel { FileName = "file.gif" }, provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle();
        results[0].ErrorMessage.Should().Be("Only accept .png, .jpg files.",
            "the localized message must include the configured extension list as a composite format argument");
    });

    /// <summary>
    /// Verifies that localized file extensions attribute default extensions applied when unset.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_DefaultExtensionsAppliedWhenUnset() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Only accept {0} files.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedFileExtensionsAttribute("FileExt.Error");
        var context = new ValidationContext(new FileExtensionsModel(), provider, null);

        attribute.GetValidationResult("image.png", context).Should().Be(ValidationResult.Success,
            "the framework default list includes .png");

        var result = attribute.GetValidationResult("file.txt", context);
        Assert.IsNotNull(result);
        result.ErrorMessage.Should().Be("Only accept .png, .jpg, .jpeg, .gif files.",
            "the unset Extensions value must surface the framework default extension list");
    });

    /// <summary>
    /// Verifies that localized file extensions attribute null and empty values follow framework semantics.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_NullAndEmptyValues_FollowFrameworkSemantics() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Only accept {0} files.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" };
        var context = new ValidationContext(new FileExtensionsModel(), provider, null);

        attribute.GetValidationResult(null, context).Should().Be(ValidationResult.Success,
            "a null value is valid for FileExtensions");
        var empty = attribute.GetValidationResult("", context);
        Assert.IsNotNull(empty);
        empty.ErrorMessage.Should().Be("Only accept .png, .jpg files.",
            "an empty value is invalid for FileExtensions and must be localized");
    });

    /// <summary>
    /// Verifies that localized file extensions attribute formatted extensions matches framework display.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_FormattedExtensions_MatchesFrameworkDisplay() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Only accept {0} files.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" };
        var context = new ValidationContext(new FileExtensionsModel(), provider, null);

        var framework = new FileExtensionsAttribute { Extensions = "png,jpg" };
        var frameworkMessage = framework.FormatErrorMessage("X");
        var frameworkDisplay = frameworkMessage[(frameworkMessage.LastIndexOf(':') + 1)..].Trim();

        var result = attribute.GetValidationResult("file.gif", context);
        Assert.IsNotNull(result);
        result.ErrorMessage.Should().Be("Only accept " + frameworkDisplay + " files.",
            "the localized format argument must match the extension list displayed by the framework attribute");
    });

    /// <summary>
    /// Verifies that localized file extensions attribute respects each validation current culture.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_RespectsEachValidationCurrentCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("FileExtensionsModel", "en-US", "FileExt.Error", "Allow only {0} (US).");
        store.SetEntry("FileExtensionsModel", "fr", "FileExt.Error", "Autorisez uniquement {0} (FR).");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var attribute = new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" };
                var context = new ValidationContext(new FileExtensionsModel(), provider, null);
                var result = attribute.GetValidationResult("file.gif", context);
                Assert.IsNotNull(result);
                message = result.ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Allow only .png, .jpg (US).");
        ValidateUnder("fr").Should().Be("Autorisez uniquement .png, .jpg (FR).");
    }

    /// <summary>
    /// Verifies that localized file extensions attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedFileExtensionsAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedFileExtensionsAttribute("FileExt.Error") { Extensions = "png,jpg" };
        var context = new ValidationContext(new FileExtensionsModel(), provider, null);

        var act = () => attribute.GetValidationResult("file.gif", context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<FileExtensionsModel>");
    }

    /// <summary>
    /// Verifies that localized credit card attribute valid luhn number succeeds.
    /// </summary>
    [TestMethod]
    public void LocalizedCreditCardAttribute_ValidLuhnNumber_Succeeds() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CreditCardModel", "en-US", "CreditCard.Error", "Invalid card number.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new CreditCardModel { Card = "4012888888881881" }, provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeTrue(
            "a Luhn-valid card number must be accepted through the framework pipeline");
        results.Should().BeEmpty();
    });

    /// <summary>
    /// Verifies that localized credit card attribute invalid number localized message.
    /// </summary>
    [TestMethod]
    public void LocalizedCreditCardAttribute_InvalidNumber_LocalizedMessage() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CreditCardModel", "en-US", "CreditCard.Error", "Invalid card number.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new CreditCardModel { Card = "not-a-card" }, provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle();
        results[0].ErrorMessage.Should().Be("Invalid card number.");
    });

    /// <summary>
    /// Verifies that localized credit card attribute null and empty values follow framework semantics.
    /// </summary>
    [TestMethod]
    public void LocalizedCreditCardAttribute_NullAndEmptyValues_FollowFrameworkSemantics() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CreditCardModel", "en-US", "CreditCard.Error", "Invalid card number.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCreditCardAttribute("CreditCard.Error");
        var context = new ValidationContext(new CreditCardModel(), provider, null);

        attribute.GetValidationResult(null, context).Should().Be(ValidationResult.Success,
            "a null value is valid for CreditCard");
        attribute.GetValidationResult("", context).Should().Be(ValidationResult.Success,
            "an empty value is valid for CreditCard");
    });

    /// <summary>
    /// Verifies that localized credit card attribute respects each validation current culture.
    /// </summary>
    [TestMethod]
    public void LocalizedCreditCardAttribute_RespectsEachValidationCurrentCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("CreditCardModel", "en-US", "CreditCard.Error", "Invalid card number (US).");
        store.SetEntry("CreditCardModel", "fr", "CreditCard.Error", "Numéro de carte invalide (FR).");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var attribute = new LocalizedCreditCardAttribute("CreditCard.Error");
                var context = new ValidationContext(new CreditCardModel(), provider, null);
                var result = attribute.GetValidationResult("not-a-card", context);
                Assert.IsNotNull(result);
                message = result.ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Invalid card number (US).");
        ValidateUnder("fr").Should().Be("Numéro de carte invalide (FR).");
    }

    /// <summary>
    /// Verifies that localized credit card attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedCreditCardAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCreditCardAttribute("CreditCard.Error");
        var context = new ValidationContext(new CreditCardModel(), provider, null);

        var act = () => attribute.GetValidationResult("not-a-card", context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<CreditCardModel>");
    }

    /// <summary>
    /// Verifies that localized custom validation attribute successful validator succeeds.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_SuccessfulValidator_Succeeds() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("SuccessfulCustomValidationModel", "en-US", "Custom.Error", "Custom check failed.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new SuccessfulCustomValidationModel(), provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeTrue(
            "a custom validator returning ValidationResult.Success must succeed through the framework pipeline");
        results.Should().BeEmpty();
    });

    /// <summary>
    /// Verifies that localized custom validation attribute failed validator localized message.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_FailedValidator_LocalizedMessage() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CustomValidationModel", "en-US", "Custom.Error", "Custom check failed.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new CustomValidationModel(), provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle();
        results[0].ErrorMessage.Should().Be("Custom check failed.",
            "the custom validator failure must be replaced by the localized message");
    });

    /// <summary>
    /// Verifies that localized custom validation attribute failed validator preserves member names.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_FailedValidator_PreservesMemberNames() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("MemberNamesCustomValidationModel", "en-US", "Custom.Error", "Custom check failed.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new MemberNamesCustomValidationModel { Code = "needs-members" }, provider, null);
        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle();
        results[0].ErrorMessage.Should().Be("Custom check failed.");
        results[0].MemberNames.Should().ContainSubset(CustomValidationFixtures.ExpectedMemberNames,
            "member names reported by the custom validator must be preserved by the localized attribute");
    });

    /// <summary>
    /// Verifies that localized custom validation attribute validation context is passed to validator.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_ValidationContext_IsPassedToValidator() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("CustomValidationModel", "en-US", "Custom.Error", "Custom check failed.");
        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.NeedsContext), "Custom.Error");
        var context = new ValidationContext(new ContextAwareCustomValidationModel(), provider, null);
        var result = attribute.GetValidationResult("x", context);

        result.Should().Be(ValidationResult.Success,
            "the validator accepting a ValidationContext must receive the current validation context");
    });

    /// <summary>
    /// Verifies that localized custom validation attribute key constructor rejects null or whitespace.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_KeyConstructor_RejectsNullOrWhitespace() {
        var actKeyNull = () => new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), null!);
        var actKeyEmpty = () => new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), "");
        var actKeyWhiteSpace = () => new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), "   ");

        actKeyNull.Should().Throw<ArgumentException>();
        actKeyEmpty.Should().Throw<ArgumentException>();
        actKeyWhiteSpace.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that localized custom validation attribute exposes validator type and method.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_ExposesValidatorTypeAndMethod() {
        var attribute = new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), "Custom.Error");

        attribute.ValidatorType.Should().Be(typeof(CustomValidationFixtures));
        attribute.Method.Should().Be(nameof(CustomValidationFixtures.Pass));
    }

    /// <summary>
    /// Verifies that localized custom validation attribute validator exception propagates.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_ValidatorException_Propagates() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Throws), "Custom.Error");
        var context = new ValidationContext(new CustomValidationModel(), provider, null);

        var act = () => attribute.GetValidationResult("x", context);

        act.Should().Throw<FormatException>()
            .Exception.Message.Should().Be("fixture-exploded",
                "exceptions raised by the custom validator must propagate unchanged");
    }

    /// <summary>
    /// Verifies that localized custom validation attribute signature error propagates invalid operation exception.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_SignatureError_PropagatesInvalidOperationException() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), "MethodThatDoesNotExist", "Custom.Error");
        var context = new ValidationContext(new CustomValidationModel(), provider, null);

        var act = () => attribute.GetValidationResult("x", context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("does not exist",
                "framework custom-validation signature errors must propagate unchanged");
    }

    /// <summary>
    /// Verifies that localized custom validation attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new LocalizedCustomValidationAttribute(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Fail), "Custom.Error");
        var context = new ValidationContext(new CustomValidationModel(), provider, null);

        var act = () => attribute.GetValidationResult("x", context);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<CustomValidationModel>");
    }

    /// <summary>
    /// Verifies that localized custom validation attribute through framework validator uses localized message per culture.
    /// </summary>
    [TestMethod]
    public void LocalizedCustomValidationAttribute_ThroughFrameworkValidator_UsesLocalizedMessagePerCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("CustomValidationModel", "en-US", "Custom.Error", "Custom check failed (en).");
        store.SetEntry("CustomValidationModel", "fr", "Custom.Error", "Échec de la vérification personnalisée (fr).");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var results = new List<ValidationResult>();
                var context = new ValidationContext(new CustomValidationModel(), provider, null);
                Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
                results.Should().ContainSingle();
                message = results[0].ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Custom check failed (en).");
        ValidateUnder("fr").Should().Be("Échec de la vérification personnalisée (fr).");
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
    /// Represents a file extensions model.
    /// </summary>
    private sealed class FileExtensionsModel {
        /// <summary>
        /// Gets or sets the file name.
        /// </summary>
        [LocalizedFileExtensions("FileExt.Error", Extensions = "png,jpg")]
        public string? FileName { get; set; }
    }

    /// <summary>
    /// Represents a credit card model.
    /// </summary>
    private sealed class CreditCardModel {
        /// <summary>
        /// Gets or sets the card.
        /// </summary>
        [LocalizedCreditCard("CreditCard.Error")]
        public string? Card { get; set; }
    }

    /// <summary>
    /// Represents a custom validation model.
    /// </summary>
    private sealed class CustomValidationModel {
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        [LocalizedCustomValidation(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Fail), "Custom.Error")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Represents a successful custom validation model.
    /// </summary>
    private sealed class SuccessfulCustomValidationModel {
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        [LocalizedCustomValidation(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.Pass), "Custom.Error")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Represents a member names custom validation model.
    /// </summary>
    private sealed class MemberNamesCustomValidationModel {
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        [LocalizedCustomValidation(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.FailWithMembers), "Custom.Error")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Represents a context aware custom validation model.
    /// </summary>
    private sealed class ContextAwareCustomValidationModel {
        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        [LocalizedCustomValidation(typeof(CustomValidationFixtures), nameof(CustomValidationFixtures.NeedsContext), "Custom.Error")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Represents custom validation fixtures.
    /// </summary>
    [SuppressMessage("Design", "IDE0060:Remove unused parameter", Justification = "Framework-required parameter; the custom-validation contract mandates (object? value)")]
    public static class CustomValidationFixtures {
        /// <summary>
        /// Performs the pass operation.
        /// </summary>
        /// <param name="_">The _.</param>
        /// <returns>The pass result.</returns>
        public static ValidationResult? Pass(object? _) => ValidationResult.Success;

        /// <summary>
        /// Performs the fail operation.
        /// </summary>
        /// <param name="_">The _.</param>
        /// <returns>The fail result.</returns>
        public static ValidationResult? Fail(object? _) => new("fixture-fail");

        /// <summary>
        /// Performs the fail with members operation.
        /// </summary>
        /// <param name="_">The _.</param>
        /// <returns>The fail with members result.</returns>
        public static ValidationResult? FailWithMembers(object? _) => new("fixture-fail", ExpectedMemberNames);

        /// <summary>
        /// Determines whether the value needs context.
        /// </summary>
        /// <param name="_">The _.</param>
        /// <param name="context">The context.</param>
        /// <returns>The needs context result.</returns>
        public static ValidationResult? NeedsContext(object? _, ValidationContext context)
            => context is null ? new ValidationResult("no-context") : ValidationResult.Success;

        /// <summary>
        /// Performs the throws operation.
        /// </summary>
        /// <param name="_">The _.</param>
        /// <returns>The throws result.</returns>
        public static ValidationResult? Throws(object? _) => throw new FormatException("fixture-exploded");

        internal static readonly string[] ExpectedMemberNames = ["Name", "Other"];
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
}
