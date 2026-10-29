//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Localization;

/// <summary>
/// Provides an implementation of <see cref="IStringLocalizer"/> that retrieves localized strings
/// from an underlying <see cref="ILocalizationResourceSet"/>.
/// </summary>
public class CustomStringLocalizer : IStringLocalizer {
    /// <summary>
    /// Gets or sets the underlying localization resource set.
    /// </summary>
    /// <value>The resource set used for retrieving localized strings. Must be assigned before the localizer is used.</value>
    /// <exception cref="ArgumentNullException">Thrown when a null value is assigned.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the value is read before it has been assigned.</exception>
    protected ILocalizationResourceSet LocalizationResourceSet {
        get => field ?? throw new InvalidOperationException("The LocalizationResourceSet must be assigned before the localizer is used.");
        set {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomStringLocalizer"/> class.
    /// </summary>
    /// <param name="resourceSet">The resource set used for retrieving localized strings.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resourceSet"/> is null.</exception>
    public CustomStringLocalizer([DisallowNull] ILocalizationResourceSet resourceSet) => LocalizationResourceSet = resourceSet ?? throw new ArgumentNullException(nameof(resourceSet));

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomStringLocalizer"/> class for use by derived classes.
    /// </summary>
    /// <remarks>
    /// The derived class must assign a non-null <see cref="LocalizationResourceSet"/> before the localizer is used.
    /// </remarks>
    protected CustomStringLocalizer() { }

    /// <inheritdoc/>
    public LocalizedString this[string name] {
        get {
            if(LocalizationResourceSet.TryGetString(name, out var entry)) {
                return new LocalizedString(name, entry.Value, resourceNotFound: entry.ResourceNotFound);
            }

            entry = ExecuteAtSynchronousFrameworkBoundary(
                LocalizationResourceSet.GetStringAsync(name));
            return new LocalizedString(name, entry.Value, resourceNotFound: entry.ResourceNotFound);
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments] {
        get {
            if(!LocalizationResourceSet.TryGetString(name, out var entry)) {
                entry = ExecuteAtSynchronousFrameworkBoundary(
                    LocalizationResourceSet.GetStringAsync(name));
            }

            var formattedValue = string.Format(CultureInfo.CurrentCulture, entry.Value, arguments);

            return new LocalizedString(name, formattedValue, resourceNotFound: entry.ResourceNotFound);
        }
    }
    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) {
        var dict = ExecuteAtSynchronousFrameworkBoundary(
            LocalizationResourceSet.GetAllStringsAsync());

        return dict.Select(static kvp => new LocalizedString(kvp.Key, kvp.Value.Value, resourceNotFound: kvp.Value.ResourceNotFound));
    }

    /// <summary>
    /// Isolates the synchronous <see cref="IStringLocalizer"/> callbacks from the
    /// asynchronous resource-set contract. The framework exposes no asynchronous
    /// equivalent for these callbacks, so this is the single intentional bridge.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="operation">The asynchronous operation to execute synchronously.</param>
    /// <returns>The at synchronous framework boundary result.</returns>
    private static T ExecuteAtSynchronousFrameworkBoundary<T>(Task<T> operation) =>
        operation.ConfigureAwait(false).GetAwaiter().GetResult();
}
