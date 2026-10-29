//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message Provider that raises events when it internally acquires message templates from an
/// external source. Optional capability; corresponds to <see cref="ProviderFeature.EventsListening"/>.
/// </summary>
/// <remarks>
/// Events are raised only for external acquisition (not for cache hits) and at most once per batched
/// acquisition. Exceptions thrown by event handlers propagate to the caller of the acquisition
/// operation.
/// </remarks>
public interface IProviderEventSource {
    /// <summary>
    /// Raised when the Provider successfully acquires one or more message templates from an external
    /// source for a target culture.
    /// </summary>
    event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <summary>
    /// Raised when the Provider fails to acquire message templates for a target culture.
    /// </summary>
    event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;
}
