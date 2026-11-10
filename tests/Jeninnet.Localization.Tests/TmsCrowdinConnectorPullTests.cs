//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.Tms;
using Jeninnet.Localization.Translation.Tms.Crowdin;
using Jeninnet.Localization.Translation.Tms.Crowdin.Extensions;
using Jeninnet.Localization.Translation.Tms.Crowdin.Options;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Crowdin pull tests. Every test drives the connector through a fake
/// <see cref="HttpMessageHandler"/>: no test requires a Crowdin token or network
/// access. A live-key check would belong to an environment-gated suite and is
/// intentionally absent.
/// </summary>
[TestClass]
public class TmsCrowdinConnectorPullTests {
    private static readonly CultureInfo _arabic = CultureInfo.GetCultureInfo("ar");

    /// <summary>
    /// Verifies that pull joins strings and translations with notes.
    /// </summary>
    [TestMethod]
    public async Task Pull_JoinsStringsAndTranslations_WithNotesAsync() {
        var handler = new MockHttpMessageHandler((request, cancellationToken) => {
            request.Method.Should().Be(HttpMethod.Get);
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("test-token");

            var path = request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/strings", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(StringsPage(
                    (1, "App.Title", "Title", "Shown in the header"),
                    (2, null, "Add to cart", null))));
            }

            path.Should().Contain("/languages/ar/translations");
            return Task.FromResult(JsonResponse(TranslationsPage(
                (1, "العنوان", null))));
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
    /// Verifies that pull pagination follows cursor.
    /// </summary>
    [TestMethod]
    public async Task Pull_Pagination_FollowsCursorAsync() {
        (long Id, string? Identifier, string Text, string? Context)[] first =
            Enumerable.Range(1, 500).Select(static i => ((long)i, (string?)$"K{i}", $"Text {i}", (string?)null)).ToArray();
        (long StringId, string? Content, string? PluralForm)[] firstTranslations =
            Enumerable.Range(1, 500).Select(static i => ((long)i, (string?)$"T{i}", (string?)null)).ToArray();
        var handler = new MockHttpMessageHandler((request, _) => {
            var query = request.RequestUri!.Query;
            if(request.RequestUri.AbsolutePath.EndsWith("/strings", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(query.Contains("offset=500", StringComparison.Ordinal)
                    ? StringsPage((501, "K501", "Text 501", null))
                    : StringsPage(first)));
            }

            return Task.FromResult(JsonResponse(query.Contains("offset=500", StringComparison.Ordinal)
                ? TranslationsPage()
                : TranslationsPage(firstTranslations)));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(_arabic, CancellationToken.None);

        result.Templates.Should().HaveCount(501);
        result.Templates["K501"].Should().BeNull();
        result.Templates["K1"].Should().Be("T1");
    }

    /// <summary>
    /// Verifies that pull skips plural form rows.
    /// </summary>
    [TestMethod]
    public async Task Pull_SkipsPluralFormRowsAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            if(request.RequestUri!.AbsolutePath.EndsWith("/strings", StringComparison.Ordinal)) {
                return Task.FromResult(JsonResponse(StringsPage(
                    (1, "Apple", "Apple", null),
                    (2, "Welcome", "Welcome", null))));
            }

            return Task.FromResult(JsonResponse(TranslationsPage(
                (1, "Pommes", "one"),
                (1, "Pommes-pluriel", "other"),
                (2, "Bienvenue", null))));
        });

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);

        var result = await connector.PullAsync(CultureInfo.GetCultureInfo("fr"), CancellationToken.None);

        result.Templates.Should().HaveCount(1, "plural-form strings are outside the flat catalog subset");
        result.Templates["Welcome"].Should().Be("Bienvenue");
    }

    /// <summary>
    /// Verifies that pull empty project returns empty maps.
    /// </summary>
    [TestMethod]
    public async Task Pull_EmptyProject_ReturnsEmptyMapsAsync() {
        var handler = new MockHttpMessageHandler((request, _) => Task.FromResult(JsonResponse(
            request.RequestUri!.AbsolutePath.EndsWith("/strings", StringComparison.Ordinal) ? StringsPage() : TranslationsPage())));

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
            Content = new StringContent("crowdin is down"),
        }));

        using var client = new HttpClient(handler);
        var connector = CreateConnector(client);
        Func<Task> act = async () => await connector.PullAsync(_arabic, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<TmsConnectorException>();
        exception.Which.Message.Should().Contain("500");
        exception.Which.Message.Should().Contain("crowdin is down");
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
        Action badProject = () => CreateConnector(new HttpClient(new StubHandler()), projectId: 0);

        emptyToken.Should().Throw<ArgumentException>("the setter rejects blank tokens eagerly");
        badEndpoint.Should().Throw<ArgumentException>("the setter rejects non-absolute endpoints eagerly");
        badProject.Should().Throw<InvalidOperationException>("construction validates the project id");
    }

    /// <summary>
    /// Verifies that options setters reject invalid values eagerly.
    /// </summary>
    [TestMethod]
    public void OptionsSetters_RejectInvalidValuesEagerly() {
        var options = new CrowdinConnectorOptions();
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
    /// <param name="projectId">The project id.</param>
    /// <returns>The connector result.</returns>
    private static CrowdinConnector CreateConnector(
        HttpClient client,
        string apiToken = "test-token",
        string endpoint = "https://api.crowdin.com",
        long projectId = 42) {
        var options = new CrowdinConnectorOptions {
            ApiToken = apiToken,
            Endpoint = endpoint,
            ProjectId = projectId,
            FileId = 7,
        };

        return new CrowdinConnector(Options.Create(options), client);
    }

    /// <summary>
    /// Builds a Crowdin strings list page.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The page JSON.</returns>
    internal static string StringsPage(params (long Id, string? Identifier, string Text, string? Context)[] rows) {
        var payload = new {
            data = rows.Select(static row => new {
                data = new {
                    id = row.Id,
                    identifier = row.Identifier,
                    text = row.Text,
                    context = row.Context,
                },
            }).ToArray(),
            pagination = new { offset = 0, limit = 500 },
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Builds a Crowdin translations list page.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The page JSON.</returns>
    internal static string TranslationsPage(params (long StringId, string? Content, string? PluralForm)[] rows) {
        var payload = new {
            data = rows.Select(static row => new {
                data = new {
                    stringId = row.StringId,
                    content = row.Content,
                    pluralFormName = row.PluralForm,
                },
            }).ToArray(),
            pagination = new { offset = 0, limit = 500 },
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Wraps JSON text in an OK response.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The response.</returns>
    internal static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) {
        Content = JsonContent.Create(JsonDocument.Parse(json).RootElement),
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
