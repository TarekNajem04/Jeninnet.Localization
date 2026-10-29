//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// DataAnnotations attribute that runs a custom static validation method and retrieves error messages
/// from localization service.
/// </summary>
/// <remarks>
/// The <see cref="System.ComponentModel.DataAnnotations.CustomValidationAttribute"/> base type is sealed,
/// so this attribute derives from <see cref="ValidationAttribute"/> and delegates validation to an
/// internal <see cref="System.ComponentModel.DataAnnotations.CustomValidationAttribute"/> instance.
/// The framework semantics are preserved unchanged: the validator type and method must resolve to a
/// public static method returning <see cref="ValidationResult"/> with an optional
/// <see cref="ValidationContext"/> parameter; signature errors and exceptions raised by the custom
/// method propagate unchanged; member names reported by the custom method are preserved. Only the
/// failure message is replaced by the localized message.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = true)]
public class LocalizedCustomValidationAttribute : ValidationAttribute {
    private readonly CustomValidationAttribute _validator;
    private readonly string _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedCustomValidationAttribute"/> class.
    /// </summary>
    /// <param name="validatorType">The type that contains the static validation method.</param>
    /// <param name="method">The name of the public static validation method.</param>
    /// <param name="key">The localization key for the error message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or white-space.</exception>
    public LocalizedCustomValidationAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type validatorType, string method, string key) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
        _validator = new CustomValidationAttribute(validatorType, method);
    }

    /// <summary>
    /// Gets the type that contains the static validation method.
    /// </summary>
    public Type ValidatorType => _validator.ValidatorType;

    /// <summary>
    /// Gets the name of the public static validation method.
    /// </summary>
    public string Method => _validator.Method;

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
    /// hard-coded message. Signature errors raised by the delegated
    /// <see cref="System.ComponentModel.DataAnnotations.CustomValidationAttribute"/> and exceptions
    /// thrown by the custom method propagate unchanged.
    /// </exception>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        ArgumentNullException.ThrowIfNull(validationContext);

        var baseResult = _validator.GetValidationResult(value, validationContext);
        if(baseResult is null || baseResult == ValidationResult.Success) {
            return ValidationResult.Success;
        }

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedCustomValidationAttribute");

        return new ValidationResult(localizer[_key], baseResult.MemberNames);
    }
}
