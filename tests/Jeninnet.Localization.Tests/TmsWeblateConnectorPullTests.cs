//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.Tms;
using Jeninnet.Localization.Translation.Tms.Weblate;
using Jeninnet.Localization.Translation.Tms.Weblate.Extensions;
using Jeninnet.Localization.Translation.Tms.Weblate.Options;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Weblate pull tests. Every test drives the connector through a fake
/// <see cref="HttpMessageHandler"/>: no test requires a Weblate token or network
/// access. A live-key check would belong to an environment-gated suite and is
/// intentionally absent.
/// </summary>
[TestClass]
public class TmsWeblateConnectorPullTests {
    private static readonly CultureInfo _arabic = CultureInfo.GetCultureInfo("ar");

    /// <summary>
    /// Verifies that pull joins units with notes.
    /// </summary>
    [TestMethod]
    public async Task Pull_JoinsUnitsAndNotesAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            request.Method.Should().Be(HttpMethod.Get);
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Token");
            request.Headers.Authorization.Parameter.Should().Be("test-token");

            var path = request.RequestUri!.AbsolutePath;
            path.Should().Contain("/api/translations/demo/messages/ar/units/");

            return Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["Title"], ["العنوان"], "App.Title", "Shown in the header", 20),
                (2, ["Add to cart"], [], null, null, 0))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(_arabic, CancellationToken.None);

        result.Culture.Should().Be(_arabic);
        result.SourceLanguage.Should().Be("en");
        result.Templates.Should().HaveCount(2);
        result.Templates["App.Title"].Should().Be("العنوان");
        result.Templates["Add to cart"].Should().BeNull("the vendor carries the key but has no translation yet");
        result.Notes.Should().HaveCount(1);
        result.Notes["App.Title"].Should().Be("Shown in the header");
    }

    /// <summary>
    /// Verifies that pull pagination follows next cursor.
    /// </summary>
    [TestMethod]
    public async Task Pull_Pagination_FollowsNextCursorAsync() {
        (long Id, string[] Source, string[] Target, string? Context, string? Note, int State)[] first =
            [.. Enumerable.Range(1, 500).Select(static i => ((long)i, new[] { $"K{i}" }, new[] { $"T{i}" }, (string?)$"K{i}", (string?)null, 20))];
        var handler = new MockHttpMessageHandler((request, _) => {
            var query = request.RequestUri!.Query;
            return Task.FromResult(JsonResponse(query.Contains("page=1", StringComparison.Ordinal)
                ? UnitsPage("https://weblate.example/api/translations/demo/messages/ar/units/?page=2", first)
                : UnitsPage(null, (501, ["K501"], [], "K501", null, 0))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(_arabic, CancellationToken.None);

        result.Templates.Should().HaveCount(501);
        result.Templates["K501"].Should().BeNull();
        result.Templates["K1"].Should().Be("T1");
    }

    /// <summary>
    /// Verifies that pull skips plural units.
    /// </summary>
    [TestMethod]
    public async Task Pull_SkipsPluralUnitsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(UnitsPage(null,
                (1, ["Apple", "Apples"], ["Pomme", "Pommes"], "Apple", null, 20),
                (2, ["Welcome"], ["Bienvenue"], "Welcome", null, 20)))));

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(CultureInfo.GetCultureInfo("fr"), CancellationToken.None);

        result.Templates.Should().HaveCount(1, "plural units are outside the flat catalog subset");
        result.Templates["Welcome"].Should().Be("Bienvenue");
    }

    /// <summary>
    /// Verifies that pull empty component returns empty maps.
    /// </summary>
    [TestMethod]
    public async Task Pull_EmptyComponent_ReturnsEmptyMapsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(UnitsPage(null))));

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(_arabic, CancellationToken.None);

        result.Templates.Should().BeEmpty();
        result.Notes.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that pull server error throws tms connector exception.
    /// </summary>
    [TestMethod]
    public async Task Pull_ServerError_ThrowsTmsConnectorExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("weblate is down"),
        }));

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PullAsync(_arabic, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<TmsConnectorException>();
        exception.Which.Message.Should().Contain("500");
        exception.Which.Message.Should().Contain("weblate is down");
        exception.Which.Message.Should().NotContain("test-token", "authentication material never appears in diagnostics");
    }

    /// <summary>
    /// Verifies that pull null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task Pull_NullCulture_ThrowsArgumentNullExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PullAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that options validation rejects incomplete configuration.
    /// </summary>
    [TestMethod]
    public void OptionsValidation_RejectsIncompleteConfiguration() {
        Action emptyToken = () => CreateConnector(new HttpClient(new StubHandler()), apiToken: string.Empty);
        Action badEndpoint = () => CreateConnector(new HttpClient(new StubHandler()), endpoint: "not-a-uri");
        Action emptyProject = () => CreateConnector(new HttpClient(new StubHandler()), project: string.Empty);
        Action emptyComponent = () => CreateConnector(new HttpClient(new StubHandler()), component: "  ");

        emptyToken.Should().Throw<ArgumentException>("the setter rejects blank tokens eagerly");
        badEndpoint.Should().Throw<ArgumentException>("the setter rejects non-absolute endpoints eagerly");
        emptyProject.Should().Throw<InvalidOperationException>("construction validates the project slug");
        emptyComponent.Should().Throw<InvalidOperationException>("construction validates the component slug");
    }

    /// <summary>
    /// Verifies that options setters reject invalid values eagerly.
    /// </summary>
    [TestMethod]
    public void OptionsSetters_RejectInvalidValuesEagerly() {
        var options = new WeblateConnectorOptions();
        Action emptyToken = () => options.ApiToken = "  ";
        Action badEndpoint = () => options.Endpoint = "ftp://files.example";

        emptyToken.Should().Throw<ArgumentException>();
        badEndpoint.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Creates connector.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="apiToken">The API token.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="project">The project.</param>
    /// <param name="component">The component.</param>
    /// <returns>The connector result.</returns>
    private static WeblateConnector CreateConnector(
        HttpClient client,
        string apiToken = "test-token",
        string endpoint = "https://weblate.example",
        string project = "demo",
        string component = "messages") {
        var options = new WeblateConnectorOptions {
            ApiToken = apiToken,
            Endpoint = endpoint,
            Project = project,
            Component = component,
        };

        return new WeblateConnector(Options.Create(options), client);
    }

    /// <summary>
    /// Builds a Weblate units list page.
    /// </summary>
    /// <param name="next">The next page URL, or null on the last page.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The page JSON.</returns>
    internal static string UnitsPage(
        string? next,
        params (long Id, string[] Source, string[] Target, string? Context, string? Note, int State)[] rows) {
        var payload = new {
            count = rows.Length,
            next,
            previous = (string?)null,
            results = rows.Select(static row => new {
                id = row.Id,
                source = row.Source,
                target = row.Target,
                context = row.Context,
                note = row.Note,
                state = row.State,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Wraps JSON text in an OK response.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The response.</returns>
    internal static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// Handles stub.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new InvalidOperationException("must not be called"));
    }

    /// <summary>
    /// Handles mock http message.
    /// </summary>
    /// <param name="sendAsync">The send async.</param>
    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _sendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
