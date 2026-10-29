//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// Custom <see cref="MinLengthAttribute"/> that dynamically retrieves error messages from localization service.
/// </summary>
/// <remarks>
/// The localized message template may reference the configured minimum length as a composite format
/// argument <c>{0}</c>.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="LocalizedMinLengthAttribute"/> class.
/// </remarks>
/// <param name="length">The minimum allowable length.</param>
/// <param name="key">The localization key for the error message.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
[method: RequiresUnreferencedCode("Length validation reflects over the 'Count' property on collection types that do not implement ICollection; preserve those members explicitly when publishing trimmed.")]
public class LocalizedMinLengthAttribute(int length, string key) : MinLengthAttribute(length) {
    private readonly string _key = key;

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

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedMinLengthAttribute");

        return new ValidationResult(localizer[_key, Length]);
    }
}
