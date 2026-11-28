using System.Net;
using System.Net.Http.Json;
using Orbs.Application.Conversations;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Domain.Conversations;
using Orbs.Testing;

namespace Orbs.Api.IntegrationTests;

public sealed class ChatApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ChatApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_reply_streams_as_server_sent_events_and_is_saved()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello orb");

        result.Response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        result.Events[0].Type.ShouldBe(ChatEvent.StartType);
        result.Events[^1].Type.ShouldBe(ChatEvent.DoneType);
        result.Reply.ShouldBe("You said: Hello orb");

        var saved = await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}");
        saved.Title.ShouldBe("Hello orb");
        saved.Messages.Select(m => m.Role).ShouldBe([MessageRole.User, MessageRole.Assistant]);
    }

    [Fact]
    public async Task Without_a_model_the_api_answers_409()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello");

        result.Response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_provider_failure_mid_reply_arrives_as_an_error_event()
    {
        await _api.CreateProviderAsync(new SaveProviderRequest("Flaky", FailingModelAdapter.KindName, DefaultModel: "x"));
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        var result = await _api.SendAsync(chat.Id, "Hello");

        result.Events[^1].Type.ShouldBe(ChatEvent.ErrorType);
        result.Events[^1].Text!.ShouldContain("provider exploded");
    }

    [Fact]
    public async Task Conversations_are_listed_per_orb()
    {
        await _api.ConnectEchoAsync();
        var pip = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var byte_ = await _api.CreateOrbAsync(new SaveOrbRequest("Byte"));
        await _api.StartConversationAsync(pip.Id);
        await _api.StartConversationAsync(byte_.Id);

        var list = await _api.GetAsync<List<ConversationSummaryDto>>($"/api/conversations?orbId={pip.Id}");

        list.ShouldHaveSingleItem().OrbId.ShouldBe(pip.Id);
    }

    [Fact]
    public async Task Deleting_an_orb_deletes_its_conversations()
    {
        var orb = await _api.CreateOrbAsync(new SaveOrbRequest("Pip"));
        var chat = await _api.StartConversationAsync(orb.Id);

        await _api.Http.DeleteAsync(new Uri($"/api/orbs/{orb.Id}", UriKind.Relative));

        (await _api.Http.GetAsync(new Uri($"/api/conversations/{chat.Id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

public sealed class MessagePagingTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Long_conversations_load_the_newest_messages_and_page_back()
    {
        var api = new ApiClient(_factory.CreateClient());
        await api.ConnectEchoAsync();
        var orb = await api.CreateOrbAsync(new Orbs.Application.Orbs.SaveOrbRequest("Pip"));
        var chat = await api.StartConversationAsync(orb.Id);
        for (var i = 1; i <= 3; i++)
        {
            await api.SendAsync(chat.Id, $"message {i}");
        }

        var latest = await api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}?limit=2");
        latest.Messages.Select(m => m.Content).ShouldBe(["message 3", "You said: message 3"]);
        latest.HasMoreMessages.ShouldBeTrue();

        var before = Uri.EscapeDataString(latest.Messages[0].CreatedAt.ToString("O"));
        var older = await api.GetAsync<MessagePageDto>($"/api/conversations/{chat.Id}/messages?before={before}&limit=3");
        older.Messages.Select(m => m.Content).ShouldBe(["You said: message 1", "message 2", "You said: message 2"]);
        older.HasMore.ShouldBeTrue();

        before = Uri.EscapeDataString(older.Messages[0].CreatedAt.ToString("O"));
        var first = await api.GetAsync<MessagePageDto>($"/api/conversations/{chat.Id}/messages?before={before}");
        first.Messages.Select(m => m.Content).ShouldBe(["message 1"]);
        first.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task Another_users_messages_cannot_be_paged()
    {
        var owner = new ApiClient(_factory.CreateClient());
        var orb = await owner.CreateOrbAsync(new Orbs.Application.Orbs.SaveOrbRequest("Pip"));
        var chat = await owner.StartConversationAsync(orb.Id);
        var sam = await _factory.CreateClientForNewUserAsync("sam@orbs.test");

        (await sam.GetAsync(new Uri($"/api/conversations/{chat.Id}/messages", UriKind.Relative))).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}

public sealed class ToolApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_built_in_tools_are_listed()
    {
        var api = new ApiClient(_factory.CreateClient());

        var tools = await api.GetAsync<List<Orbs.Application.Tools.ToolDto>>("/api/tools");

        tools.Select(t => t.Name).ShouldBe(["calculator", "current_time"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_reply_that_uses_a_tool_streams_the_call_and_keeps_an_audit_trail()
    {
        var api = new ApiClient(_factory.CreateClient());
        await api.ConnectEchoAsync();
        var orb = await api.SetToolAccessAsync(await api.CreateOrbAsync(new Orbs.Application.Orbs.SaveOrbRequest("Pip")), "calculator", Orbs.Domain.Orbs.ToolAccess.Allow);
        var chat = await api.StartConversationAsync(orb.Id);

        var result = await api.SendAsync(chat.Id, """/tool calculator {"expression":"6*7"}""");

        result.Events.Select(e => e.Type).ShouldBe(["start", "tool_call", "tool_result", "delta", "delta", "delta", "done"]);
        var call = result.Events.Single(e => e.Type == ChatEvent.ToolCallType);
        call.Tool.ShouldBe("calculator");
        call.Arguments.ShouldBe("""{"expression":"6*7"}""");
        result.Events.Single(e => e.Type == ChatEvent.ToolResultType).Text.ShouldBe("42");
        result.Reply.ShouldBe("calculator said: 42");

        var saved = (await api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).Messages[^1];
        saved.Model.ShouldBe("echo-1");
        var record = saved.ToolCalls.ShouldNotBeNull().ShouldHaveSingleItem();
        record.Tool.ShouldBe("calculator");
        record.Result.ShouldBe("42");
        record.IsError.ShouldBeFalse();
        record.Decision.ShouldBe("allowed");
    }

    [Fact]
    public async Task A_failing_tool_tells_the_model_what_went_wrong()
    {
        var api = new ApiClient(_factory.CreateClient());
        await api.ConnectEchoAsync();
        var orb = await api.SetToolAccessAsync(await api.CreateOrbAsync(new Orbs.Application.Orbs.SaveOrbRequest("Pip")), "calculator", Orbs.Domain.Orbs.ToolAccess.Allow);
        var chat = await api.StartConversationAsync(orb.Id);

        var result = await api.SendAsync(chat.Id, """/tool calculator {"expression":"1/0"}""");

        var toolResult = result.Events.Single(e => e.Type == ChatEvent.ToolResultType);
        toolResult.IsError.ShouldBe(true);
        result.Reply.ShouldStartWith("calculator said: The result is not a finite number");
    }
}

public sealed class ApprovalApiTests : IDisposable
{
    private readonly OrbsApiFactory _factory = new();
    private readonly ApiClient _api;

    public ApprovalApiTests() => _api = new ApiClient(_factory.CreateClient());

    public void Dispose() => _factory.Dispose();

    private async Task<(Orbs.Application.Orbs.OrbDto Orb, ConversationDto Chat)> PipWithAChatAsync()
    {
        await _api.ConnectEchoAsync();
        var orb = await _api.CreateOrbAsync(new Orbs.Application.Orbs.SaveOrbRequest("Pip"));
        return (orb, await _api.StartConversationAsync(orb.Id));
    }

    [Fact]
    public async Task By_default_an_orb_asks_before_using_a_tool_and_waits()
    {
        var (_, chat) = await PipWithAChatAsync();

        var result = await _api.SendAsync(chat.Id, "/tool current_time");

        result.Events.Select(e => e.Type).ShouldBe(["start", "approval_required"]);
        var ask = result.Events[^1];
        ask.Tool.ShouldBe("current_time");
        ask.ApprovalId.ShouldNotBeNull();
        (await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}")).PendingApproval.ShouldNotBeNull().Tool.ShouldBe("current_time");
    }

    [Fact]
    public async Task Approving_runs_the_tool_and_finishes_the_reply()
    {
        var (_, chat) = await PipWithAChatAsync();
        var ask = (await _api.SendAsync(chat.Id, """/tool calculator {"expression":"2*21"}""")).Events[^1];

        var rest = await _api.DecideAsync(chat.Id, ask.ApprovalId!.Value, ApprovalDecision.Approve);

        rest.Events[0].Resumed.ShouldBe(true);
        rest.Events.Single(e => e.Type == ChatEvent.ToolResultType).Decision.ShouldBe("approved");
        rest.Reply.ShouldBe("calculator said: 42");
        var saved = await _api.GetAsync<ConversationDto>($"/api/conversations/{chat.Id}");
        saved.PendingApproval.ShouldBeNull();
        saved.Messages[^1].ToolCalls.ShouldNotBeNull().Single().Decision.ShouldBe("approved");
    }

    [Fact]
    public async Task Denying_tells_the_model_and_the_tool_never_runs()
    {
        var (_, chat) = await PipWithAChatAsync();
        var ask = (await _api.SendAsync(chat.Id, """/tool calculator {"expression":"2*21"}""")).Events[^1];

        var rest = await _api.DecideAsync(chat.Id, ask.ApprovalId!.Value, ApprovalDecision.Deny);

        rest.Events.Single(e => e.Type == ChatEvent.ToolResultType).Decision.ShouldBe("denied");
        rest.Reply.ShouldStartWith("calculator said: The person declined");
    }

    [Fact]
    public async Task Always_allows_the_tool_from_now_on()
    {
        var (orb, chat) = await PipWithAChatAsync();
        var ask = (await _api.SendAsync(chat.Id, "/tool current_time")).Events[^1];

        await _api.DecideAsync(chat.Id, ask.ApprovalId!.Value, ApprovalDecision.Always);

        (await _api.GetAsync<Orbs.Application.Orbs.OrbDto>($"/api/orbs/{orb.Id}")).ToolPermissions!["current_time"].ShouldBe(Orbs.Domain.Orbs.ToolAccess.Allow);
        (await _api.SendAsync(chat.Id, "/tool current_time")).Events.Select(e => e.Type).ShouldContain("done");
    }

    [Fact]
    public async Task Blocked_tools_are_not_offered_to_the_model()
    {
        var (orb, chat) = await PipWithAChatAsync();
        await _api.SetToolAccessAsync(orb, "current_time", Orbs.Domain.Orbs.ToolAccess.Block);

        var result = await _api.SendAsync(chat.Id, "/tool current_time");

        result.Reply.ShouldBe("You said: /tool current_time");
    }

    [Fact]
    public async Task An_answered_request_cannot_be_answered_again()
    {
        var (_, chat) = await PipWithAChatAsync();
        var ask = (await _api.SendAsync(chat.Id, "/tool current_time")).Events[^1];
        await _api.DecideAsync(chat.Id, ask.ApprovalId!.Value, ApprovalDecision.Approve);

        var again = await _api.Http.PostAsJsonAsync($"/api/conversations/{chat.Id}/approvals/{ask.ApprovalId}", new DecideRequest(ApprovalDecision.Approve), ApiClient.Json);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
