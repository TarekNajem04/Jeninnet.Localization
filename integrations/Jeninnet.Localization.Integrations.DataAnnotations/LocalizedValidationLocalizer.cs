//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.DataAnnotations;

/// <summary>
/// Shared resolution of the localized message source for the localized validation
/// attributes: resolves the canonical <see cref="IStringLocalizerFactory"/>
/// from the <see cref="ValidationContext"/> and creates the localizer for the
/// validated object type. This is exactly the resolution the framework's
/// <c>IStringLocalizer&lt;TObject&gt;</c> wrapper performs, without constructing the
/// closed generic over a runtime type via <see cref="Type.MakeGenericType(Type[])"/> —
/// a pattern native AOT (Ahead-of-Time) cannot guarantee.
/// </summary>
internal static class LocalizedValidationLocalizer {
    /// <summary>
    /// Resolves a localized string provider for the validated object type.
    /// </summary>
    /// <param name="validationContext">The validation context carrying the service provider.</param>
    /// <param name="attributeName">The attribute name used in the configuration-error message.</param>
    /// <returns>The store-backed localizer for the validated object type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the validation context cannot resolve an <see cref="IStringLocalizerFactory"/>.
    /// A missing localization composition is a configuration error and fails deterministically
    /// instead of emitting a hard-coded message.
    /// </exception>
    internal static IStringLocalizer Resolve(ValidationContext validationContext, string attributeName) {
        ArgumentNullException.ThrowIfNull(validationContext);
        ArgumentNullException.ThrowIfNull(attributeName);

        if(validationContext.GetService(typeof(IStringLocalizerFactory)) is IStringLocalizerFactory factory) {
            return factory.Create(validationContext.ObjectType);
        }

        throw new InvalidOperationException(
            attributeName + " could not resolve IStringLocalizer<" + validationContext.ObjectType.Name +
            "> from the validation context. Register the canonical localization pipeline (the store-backed IStringLocalizerFactory through AddLocalization(...)) before validating; validation messages must not fall back to hard-coded text.");
    }
}
