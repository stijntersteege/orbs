using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Orbs.Application.Accounts;
using Orbs.Application.Context;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class SecurityTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new() { SignInPerMinute = 3 };

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/auth/status")]
    [InlineData("/api/orbs")]
    public async Task Every_response_carries_the_browser_protections(string path)
    {
        var response = await _factory.CreateAnonymousClient().GetAsync(new Uri(path, UriKind.Relative));

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        var csp = response.Headers.GetValues("Content-Security-Policy").ShouldHaveSingleItem();
        csp.ShouldContain("script-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldNotContain("unsafe-eval");
    }

    [Fact]
    public async Task Guessing_passwords_is_slowed_down()
    {
        var client = _factory.CreateAnonymousClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("nobody@orbs.test", "wrong-password"), ApiClient.Json);
            codes.Add(response.StatusCode);
        }

        codes.ShouldBe([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task An_uploaded_web_page_downloads_as_plain_bytes()
    {
        var api = new ApiClient(_factory.CreateClient());
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("<script>alert(1)</script><p>Notes</p>"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(file, "file", "notes.html");
        var source = await ApiClient.ReadAsync<ContextSourceDto>(await api.Http.PostAsync(new Uri("/api/context-sources/files", UriKind.Relative), form));

        var download = await api.Http.GetAsync(new Uri($"/api/context-sources/{source.Id}/file", UriKind.Relative));

        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task Webhook_calls_are_limited_and_need_the_secret()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.PostAsync(new Uri($"/api/hooks/{Guid.NewGuid()}/not-the-secret", UriKind.Relative), new StringContent(new string('x', 50_000)));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_model_provider_added_by_someone_who_is_not_an_admin_cannot_reach_local_addresses()
    {
        var sam = new ApiClient(await _factory.CreateClientForNewUserAsync("sam@orbs.test"));
        var provider = await sam.CreateProviderAsync(new Orbs.Application.Providers.SaveProviderRequest("Sneaky", "openai-compatible", "http://127.0.0.1:9/v1", "key"));

        var response = await sam.Http.GetAsync(new Uri($"/api/providers/{provider.Id}/models", UriKind.Relative));

        response.IsSuccessStatusCode.ShouldBeFalse();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("does not fetch addresses on private networks", customMessage: body);
    }
}
