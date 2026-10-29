//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// DataAnnotations attribute that validates a file extension and retrieves error messages from
/// localization service.
/// </summary>
/// <remarks>
/// The <see cref="System.ComponentModel.DataAnnotations.FileExtensionsAttribute"/> base type is sealed,
/// so this attribute derives from <see cref="System.ComponentModel.DataAnnotations.DataTypeAttribute"/>
/// (preserving the <see cref="DataType.Upload"/> metadata) and delegates validation to an internal
/// <see cref="System.ComponentModel.DataAnnotations.FileExtensionsAttribute"/> instance. The configured
/// extensions and the validation behavior therefore match the framework attribute exactly. The localized
/// message template may reference the effective extension list as composite format argument <c>{0}</c>.
/// </remarks>
/// <param name="key">The localization key for the error message.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class LocalizedFileExtensionsAttribute(string key) : DataTypeAttribute(DataType.Upload) {
    private readonly FileExtensionsAttribute _validator = new();

    /// <summary>
    /// Gets or sets the allowed file extensions. A null, empty, or white-space value uses the framework
    /// default list (<c>.png, .jpg, .jpeg, .gif</c>). Extensions are matched case-insensitively.
    /// </summary>
    public string Extensions {
        get => _validator.Extensions;
        set => _validator.Extensions = value;
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

        var baseResult = _validator.GetValidationResult(value, validationContext);
        if(baseResult is null || baseResult == ValidationResult.Success) {
            return ValidationResult.Success;
        }

        var localizer = LocalizedValidationLocalizer.Resolve(validationContext, "LocalizedFileExtensionsAttribute");

        return new ValidationResult(localizer[key, FormatExtensions(_validator.Extensions)]);
    }

    /// <summary>
    /// Formats extensions.
    /// </summary>
    /// <param name="extensions">The extensions.</param>
    /// <returns>The extensions text.</returns>
    private static string FormatExtensions(string extensions) {
        if(string.IsNullOrWhiteSpace(extensions)) {
            return ".png, .jpg, .jpeg, .gif";
        }

        return string.Join(", ", extensions.Split(',').Select(static part => {
            var trimmed = new string([.. part.Where(static c => !char.IsWhiteSpace(c))]).ToLowerInvariant();
            return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
        }));
    }
}
