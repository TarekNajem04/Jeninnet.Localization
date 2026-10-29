//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// DataAnnotations attribute that validates a URL and retrieves error messages from
/// localization service.
/// </summary>
/// <remarks>
/// The <see cref="System.ComponentModel.DataAnnotations.UrlAttribute"/> base type is sealed,
/// so this attribute derives from <see cref="System.ComponentModel.DataAnnotations.DataTypeAttribute"/>
/// (preserving the <see cref="DataType.Url"/> metadata) and delegates validation to an
/// internal <see cref="System.ComponentModel.DataAnnotations.UrlAttribute"/> instance.
/// Initializes a new instance of the <see cref="LocalizedUrlAttribute"/> class.
/// </remarks>
/// <param name="key">The localization key for the error message.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class LocalizedUrlAttribute(string key) : DataTypeAttribute(DataType.Url) {
    private readonly UrlAttribute _validator = new();

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

        if(_validator.GetValidationResult(value, validationContext) == ValidationResult.Success) {
            return ValidationResult.Success;
        }

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedUrlAttribute");

        return new ValidationResult(localizer[key]);
    }
}
