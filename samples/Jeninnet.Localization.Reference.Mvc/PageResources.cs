//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Mvc;

/// <summary>
/// Anchors the strongly-typed <see cref="IStringLocalizer{T}"/> consumed by the MVC host's
/// controller and store-backed bridge code. Razor views resolve through the view-scoped
/// <c>IViewLocalizer</c> bridge instead. The resource-set name maps to the shared
/// reference content through the store, so no dedicated resource for this type exists.
/// </summary>
public sealed class PageResources {
    /// <summary>
    /// Initializes a new instance of the <see cref="PageResources"/> class.
    /// </summary>
    public PageResources() {
    }
}
