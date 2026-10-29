//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// Provides localized validation metadata for the application.
/// </summary>
/// <remarks>
/// Localizes the error message of plain framework <see cref="RequiredAttribute"/> validation metadata using the
/// property name as the localization key. Attributes that derive from <see cref="RequiredAttribute"/> (including
/// the localized DataAnnotations family) are left untouched: they own their own localization through their
/// validation logic, so this provider must not attach a competing, culture-frozen message.
/// <para>
/// The resolved message is attached when MVC creates the metadata and MVC caches the created metadata for the
/// lifetime of the application, so the ErrorMessage reflects the <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
/// active at first metadata creation. Applications that need culture-correct "required" messages across request
/// cultures should use the localized DataAnnotations family, which resolves its messages at validation time.
/// </para>
/// Initializes a new instance of the <see cref="LocalizedValidationMetadataProvider"/> class.
/// </remarks>
/// <param name="factory">The string localizer factory.</param>
public class LocalizedValidationMetadataProvider(IStringLocalizerFactory factory) : IValidationMetadataProvider {
    /// <summary>
    /// Creates the validation metadata.
    /// </summary>
    /// <param name="context">The validation metadata provider context.</param>
    public void CreateValidationMetadata(ValidationMetadataProviderContext context) {
        ArgumentNullException.ThrowIfNull(context);
        var localizer = factory.Create(context.Key.ContainerType ?? context.Key.ModelType);

        var requiredAttributes = context.ValidationMetadata
                                        .ValidatorMetadata
                                        .OfType<ValidationAttribute>()
                                        .Where(static attr => attr is RequiredAttribute
                                            && attr.GetType() == typeof(RequiredAttribute)
                                            && string.IsNullOrEmpty(attr.ErrorMessage));

        foreach(var attribute in requiredAttributes) {
            if(context.Key.Name is { } name) {
                attribute.ErrorMessage = localizer[name];
            }
        }
    }
}
