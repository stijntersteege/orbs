using System.Net;
using System.Text;
using System.Text.Json;
using MimeKit;
using Orbs.Application.Tools;
using Orbs.Infrastructure.Integrations;

namespace Orbs.Infrastructure.Tests;

public class IntegrationTests
{
    private static IntegrationConfig Config(Dictionary<string, string> settings, string? secret = "secret") =>
        new(Guid.NewGuid(), "Test", settings, secret, RestrictNetwork: false);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Web_search_reads_searxng_results()
    {
        var http = new FakeHttp(_ => Json("""{"results":[{"title":"Orbs","url":"https://orbs.example","content":"AI companions"},{"title":"Two","url":"https://two.example","content":""}]}"""));
        var search = new WebSearchIntegration(http);

        var text = await search.SearchAsync(Config(new() { ["provider"] = "searxng", ["url"] = "https://search.example" }), "orbs app", 5, CancellationToken.None);

        http.Requests.Single().RequestUri!.AbsoluteUri.ShouldBe("https://search.example/search?q=orbs%20app&format=json");
        text.ShouldStartWith("1. Orbs\nhttps://orbs.example\nAI companions".Replace("\n", Environment.NewLine, StringComparison.Ordinal));
        text.ShouldContain("2. Two");
    }

    [Fact]
    public async Task Brave_search_sends_the_key_as_a_header()
    {
        var http = new FakeHttp(_ => Json("""{"web":{"results":[{"title":"Hit","url":"https://hit.example","description":"Yes"}]}}"""));

        var text = await new WebSearchIntegration(http).SearchAsync(Config(new() { ["provider"] = "brave" }, "brave-key"), "q", 3, CancellationToken.None);

        http.Requests.Single().Headers.GetValues("X-Subscription-Token").ShouldBe(["brave-key"]);
        text.ShouldContain("Hit");
    }

    [Fact]
    public async Task Home_assistant_lists_entities_of_one_domain()
    {
        var http = new FakeHttp(_ => Json("""[{"entity_id":"light.kitchen","state":"on","attributes":{"friendly_name":"Kitchen"}},{"entity_id":"sensor.temp","state":"21.5","attributes":{}}]"""));
        var home = new HomeAssistantIntegration(http);

        var text = await home.ListAsync(Config(new() { ["url"] = "http://ha.local:8123" }, "token"), "light", CancellationToken.None);

        text.ShouldBe("light.kitchen: on (Kitchen)");
        http.Requests.Single().Headers.Authorization!.ToString().ShouldBe("Bearer token");
    }

    [Fact]
    public async Task Home_assistant_calls_a_service_with_the_entity()
    {
        var http = new FakeHttp(_ => Json("[]"));
        var home = new HomeAssistantIntegration(http);

        var text = await home.CallAsync(Config(new() { ["url"] = "http://ha.local:8123" }, "t"), Args("""{"domain":"light","service":"turn_on","entityId":"light.kitchen","data":{"brightness_pct":40}}"""), CancellationToken.None);

        text.ShouldBe("Called light.turn_on on light.kitchen.");
        var request = http.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/services/light/turn_on");
        http.Bodies.Single().ShouldContain("\"entity_id\":\"light.kitchen\"");
        http.Bodies.Single().ShouldContain("\"brightness_pct\":40");
    }

    [Fact]
    public async Task Home_assistant_refuses_odd_service_names()
    {
        var home = new HomeAssistantIntegration(new FakeHttp(_ => Json("[]")));

        await Should.ThrowAsync<ArgumentException>(() => home.CallAsync(Config(new() { ["url"] = "http://ha.local" }), Args("""{"domain":"../config","service":"x"}"""), CancellationToken.None));
    }

    [Fact]
    public async Task Caldav_lists_events_from_a_calendar_query()
    {
        const string ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:1\r\nDTSTART:20261201T090000Z\r\nDTEND:20261201T100000Z\r\nSUMMARY:Stand-up\\, team\r\nLOCATION:Room 1\r\nEND:VEVENT\r\nEND:VCALENDAR";
        var xml = $"""<?xml version="1.0"?><d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav"><d:response><d:propstat><d:prop><c:calendar-data>{ics}</c:calendar-data></d:prop></d:propstat></d:response></d:multistatus>""";
        var http = new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.MultiStatus) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") });
        var calendar = new CalDavIntegration(http, TimeProvider.System);

        var text = await calendar.ListAsync(Config(new() { ["url"] = "https://dav.example/cal/", ["username"] = "me" }), "2026-12-01T00:00:00Z", "2026-12-02T00:00:00Z", CancellationToken.None);

        http.Requests.Single().Method.Method.ShouldBe("REPORT");
        http.Bodies.Single().ShouldContain("time-range");
        http.Bodies.Single().ShouldContain("20261201T000000Z");
        text.ShouldBe("Tue 2026-12-01 09:00–10:00 UTC  Stand-up, team @ Room 1");
    }

    [Fact]
    public async Task Caldav_creates_an_event_with_a_put()
    {
        var http = new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var calendar = new CalDavIntegration(http, TimeProvider.System);

        var text = await calendar.CreateAsync(Config(new() { ["url"] = "https://dav.example/cal/", ["username"] = "me" }), "Dentist", "2026-12-03T14:00:00+01:00", "2026-12-03T15:00:00+01:00", "Main St", null, CancellationToken.None);

        text.ShouldBe("Added \"Dentist\" on 2026-12-03 13:00 UTC.");
        var request = http.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.ToString().ShouldEndWith(".ics");
        http.Bodies.Single().ShouldContain("DTSTART:20261203T130000Z");
        http.Bodies.Single().ShouldContain("SUMMARY:Dentist");
    }

    [Fact]
    public async Task Caldav_refuses_an_end_before_the_start()
    {
        var calendar = new CalDavIntegration(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.Created)), TimeProvider.System);

        await Should.ThrowAsync<ArgumentException>(() => calendar.CreateAsync(Config(new() { ["url"] = "https://dav.example/", ["username"] = "me" }), "x", "2026-12-03T15:00:00Z", "2026-12-03T14:00:00Z", null, null, CancellationToken.None));
    }

    [Fact]
    public void Email_messages_are_described_as_plain_text()
    {
        var message = new MimeMessage { Subject = "Hello" };
        message.From.Add(MailboxAddress.Parse("sam@example.com"));
        message.To.Add(MailboxAddress.Parse("me@example.com"));
        message.Body = new TextPart("html") { Text = "<p>Hi <b>there</b></p>" };

        var text = EmailIntegration.Describe(message);

        text.ShouldContain("From: sam@example.com");
        text.ShouldContain("Subject: Hello");
        text.ShouldContain("Hi there");
    }

    [Fact]
    public void Email_only_offers_sending_when_an_smtp_server_is_set()
    {
        var email = new EmailIntegration();

        email.CreateTools(Config(new() { ["imapHost"] = "imap.example.com" })).Select(t => t.Kind).ShouldBe(["email_list_recent", "email_read"]);
        email.CreateTools(Config(new() { ["imapHost"] = "i", ["smtpHost"] = "s" })).Select(t => t.Kind).ShouldContain("email_send");
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }
}
