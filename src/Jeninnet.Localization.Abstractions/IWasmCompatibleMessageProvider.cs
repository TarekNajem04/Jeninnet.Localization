//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Marks a provider as safe to run in browser WebAssembly.
/// </summary>
/// <remarks>
/// A WASM-compatible provider resolves templates exclusively from in-memory or
/// otherwise already-loaded state: template resolution must not touch the file
/// system, a database, or the network unattended. The declaration is deliberate
/// and opt-in (a marker, not an inference), so environment-sensitive consumers —
/// for example the Blazor integration's WASM (WebAssembly) composition — can reject file/DB
/// providers with a deterministic, actionable error before any template is
/// served.
/// </remarks>
public interface IWasmCompatibleMessageProvider;
