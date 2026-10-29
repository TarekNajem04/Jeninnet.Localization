//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Seeds each Server circuit's culture from the request culture.
/// </summary>
/// <remarks>
/// The circuit's request carries the culture resolved by
/// <c>RequestLocalizationMiddleware</c> from the culture cookie (the mechanism that keeps
/// working across the WebSocket upgrade). The handler applies that culture to the
/// circuit-scoped <see cref="BlazorCultureService"/> when the circuit opens. It never
/// mutates ambient culture, so concurrent circuits stay isolated, and the culture of a
/// circuit is fixed for the circuit's lifetime (a culture switch reloads the page and
/// produces a new circuit, per).
/// </remarks>
public sealed class BlazorCircuitCultureHandler : CircuitHandler {
    private readonly BlazorCultureService _cultureService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorCircuitCultureHandler"/> class.
    /// </summary>
    /// <param name="cultureService">The circuit-scoped culture service.</param>
    /// <param name="httpContextAccessor">The accessor used to read the connection's request culture feature.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultureService"/> or <paramref name="httpContextAccessor"/> is null.</exception>
    public BlazorCircuitCultureHandler(BlazorCultureService cultureService, IHttpContextAccessor httpContextAccessor) {
        ArgumentNullException.ThrowIfNull(cultureService);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _cultureService = cultureService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken) {
        _cultureService.ApplyRequestCulture(_httpContextAccessor.HttpContext?.Features.Get<IRequestCultureFeature>());
        return Task.CompletedTask;
    }
}
