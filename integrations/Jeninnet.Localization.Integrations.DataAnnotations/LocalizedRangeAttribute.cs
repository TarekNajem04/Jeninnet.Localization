//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// Custom <see cref="RangeAttribute"/> that dynamically retrieves error messages from localization service.
/// </summary>
/// <remarks>
/// The localized message template may reference the configured minimum and maximum as composite
/// format arguments <c>{0}</c> (minimum) and <c>{1}</c> (maximum).
/// Initializes a new instance of the <see cref="LocalizedRangeAttribute"/> class.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class LocalizedRangeAttribute : RangeAttribute {
    /// <summary>
    /// Gets the localization key for the error message.
    /// </summary>
    public string LocalizationKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedRangeAttribute"/> class with integer bounds.
    /// </summary>
    /// <param name="minimum">The minimum allowable value.</param>
    /// <param name="maximum">The maximum allowable value.</param>
    /// <param name="key">The localization key for the error message.</param>
    public LocalizedRangeAttribute(int minimum, int maximum, string key)
        : base(minimum, maximum) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        LocalizationKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedRangeAttribute"/> class with operands of an arbitrary type.
    /// </summary>
    /// <param name="type">The type of the operands.</param>
    /// <param name="minimum">The minimum allowable value.</param>
    /// <param name="maximum">The maximum allowable value.</param>
    /// <param name="key">The localization key for the error message.</param>
    [RequiresUnreferencedCode("Arbitrary operand types are converted through TypeDescriptor, which trimming may break; prefer the integer-bounds constructor when publishing trimmed.")]
    public LocalizedRangeAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type, string minimum, string maximum, string key)
        : base(type, minimum, maximum) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
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

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedRangeAttribute");

        return new ValidationResult(localizer[LocalizationKey, Minimum, Maximum]);
    }
}
