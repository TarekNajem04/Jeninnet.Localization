//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Selects how the Blazor integration scopes the active culture.
/// </summary>
public enum BlazorLocalizationMode {
    /// <summary>
    /// Per-circuit scope for Blazor Server / interactive Server components: one culture
    /// per circuit, seeded from the circuit's request culture. Ambient culture is never
    /// mutated, so concurrent circuits cannot bleed culture into each other; switching a
    /// culture persists through <see cref="IBlazorCultureStore"/> and reloads the page so
    /// the request pipeline applies the new culture to the server-rendered content.
    /// </summary>
    Server = 0,

    /// <summary>
    /// Static (single-user process) scope for client-side hosts (WebAssembly, Blazor
    /// Hybrid / WebView2): applying a culture sets the process ambient culture and raises
    /// the change event so components re-render. There is no cross-user risk in a
    /// single-user process.
    /// </summary>
    Wasm = 1,
}
