//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// Custom <see cref="StringLengthAttribute"/> that dynamically retrieves error messages from localization service.
/// </summary>
/// <remarks>
/// The localized message template may reference the configured minimum and maximum lengths as composite
/// format arguments <c>{0}</c> (minimum) and <c>{1}</c> (maximum).
/// Initializes a new instance of the <see cref="LocalizedStringLengthAttribute"/> class.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class LocalizedStringLengthAttribute : StringLengthAttribute {
    /// <summary>
    /// Gets the localization key for the error message.
    /// </summary>
    public string LocalizationKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedStringLengthAttribute"/> class with a maximum length.
    /// </summary>
    /// <param name="maximumLength">The maximum allowable length of the string.</param>
    /// <param name="key">The localization key for the error message.</param>
    public LocalizedStringLengthAttribute(int maximumLength, string key)
        : base(maximumLength) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        LocalizationKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedStringLengthAttribute"/> class with a minimum and maximum length.
    /// </summary>
    /// <param name="minimumLength">The minimum allowable length of the string.</param>
    /// <param name="maximumLength">The maximum allowable length of the string.</param>
    /// <param name="key">The localization key for the error message.</param>
    public LocalizedStringLengthAttribute(int minimumLength, int maximumLength, string key)
        : base(maximumLength) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        MinimumLength = minimumLength;
        LocalizationKey = key;
    }

    /// <summary>
    /// Validates the attribute value using the localization service.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationContext"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the validation context cannot resolve the localization composition
    /// (<see cref="IStringLocalizerFactory"/>) while the value is invalid. A missing localization
    /// composition is a configuration error and fails deterministically instead of emitting a
    /// hard-coded message.
    /// </exception>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        ArgumentNullException.ThrowIfNull(validationContext);

        var baseResult = base.IsValid(value, validationContext);
        if(baseResult is null || baseResult == ValidationResult.Success) {
            return ValidationResult.Success;
        }

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedStringLengthAttribute");

        return new ValidationResult(localizer[LocalizationKey, MinimumLength, MaximumLength]);
    }
}
