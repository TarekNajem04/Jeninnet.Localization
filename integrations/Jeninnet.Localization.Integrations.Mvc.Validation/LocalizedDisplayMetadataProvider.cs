//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Mvc.Validation;

/// <summary>
/// Provides localized display metadata for <see cref="DisplayAttribute"/> through the standard
/// ASP.NET Core MVC metadata pipeline.
/// </summary>
/// <remarks>
/// Localizes <see cref="DisplayAttribute.Name"/> into display metadata, <see cref="DisplayAttribute.Description"/>
/// into description metadata, and <see cref="DisplayAttribute.Prompt"/> into placeholder metadata. Each literal
/// attribute value is the localization key, resolved through an <see cref="IStringLocalizer"/> created by the
/// supplied <see cref="IStringLocalizerFactory"/>.
/// <para>
/// On .NET 10 the MVC display metadata model exposes no destination for <see cref="DisplayAttribute.ShortName"/>,
/// so short names cannot be localized through the standard pipeline; this is a framework constraint, not a
/// configuration gap.
/// </para>
/// <para>
/// Localization is deferred inside the metadata delegates: each evaluation creates a fresh localizer, so the
/// value reflects <see cref="System.Globalization.CultureInfo.CurrentUICulture"/> at metadata-consumption time. Cached MVC metadata
/// therefore stays culture-correct when the current culture changes between requests. Missing keys surface the
/// canonical <c>[MISSING:key]</c> marker. A missing localization composition is a configuration error and
/// surfaces deterministically the first time the metadata is consumed.
/// </para>
/// <para>
/// When <see cref="DisplayAttribute.ResourceType"/> is set, the application has explicitly selected the built-in
/// resource-manager mechanism; this provider then leaves that metadata untouched.
/// </para>
/// Initializes a new instance of the <see cref="LocalizedDisplayMetadataProvider"/> class.
/// </remarks>
/// <param name="factory">The string localizer factory.</param>
public class LocalizedDisplayMetadataProvider(IStringLocalizerFactory factory) : IDisplayMetadataProvider {
    /// <summary>
    /// Creates the display metadata.
    /// </summary>
    /// <param name="context">The display metadata provider context.</param>
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context) {
        ArgumentNullException.ThrowIfNull(context);

        var displayAttribute = context.Attributes.OfType<DisplayAttribute>().FirstOrDefault();
        if(displayAttribute is null || displayAttribute.ResourceType is not null) {
            return;
        }

        var containerType = context.Key.ContainerType ?? context.Key.ModelType;

        if(!string.IsNullOrEmpty(displayAttribute.Name)) {
            context.DisplayMetadata.DisplayName = () => factory.Create(containerType)[displayAttribute.Name];
        }

        if(!string.IsNullOrEmpty(displayAttribute.Description)) {
            context.DisplayMetadata.Description = () => factory.Create(containerType)[displayAttribute.Description];
        }

        if(!string.IsNullOrEmpty(displayAttribute.Prompt)) {
            context.DisplayMetadata.Placeholder = () => factory.Create(containerType)[displayAttribute.Prompt];
        }
    }
}
