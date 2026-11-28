using System.Net;
using Microsoft.Extensions.Configuration;
using Orbs.Infrastructure.Security;

namespace Orbs.Infrastructure.Tests;

public class SafeHttpTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("64:ff9b::a00:1")]
    public void Internal_addresses_are_recognized(string ip)
    {
        SafeHttp.IsInternal(IPAddress.Parse(ip)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string ip)
    {
        SafeHttp.IsInternal(IPAddress.Parse(ip)).ShouldBeFalse();
    }

    [Fact]
    public async Task Connecting_to_a_private_address_is_refused_with_a_helpful_message()
    {
        var error = await Should.ThrowAsync<BlockedAddressException>(async () =>
            await SafeHttp.ConnectAsync(NetworkPolicy.Default, new DnsEndPoint("127.0.0.1", 80), CancellationToken.None));

        error.Message.ShouldContain("AllowedHosts");
    }

    [Fact]
    public async Task Blocked_hosts_are_refused_before_any_lookup()
    {
        var policy = NetworkPolicy.Default with { BlockedHosts = ["example.com"] };

        await Should.ThrowAsync<BlockedAddressException>(async () =>
            await SafeHttp.ConnectAsync(policy, new DnsEndPoint("www.example.com", 443), CancellationToken.None));
    }

    [Fact]
    public async Task A_client_with_the_safe_handler_cannot_reach_localhost()
    {
        using var client = new HttpClient(SafeHttp.CreateHandler(NetworkPolicy.Default));

        var error = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(new Uri("http://localhost:1/")));

        (error as BlockedAddressException ?? error.InnerException).ShouldBeOfType<BlockedAddressException>();
    }

    [Fact]
    public void Lists_come_from_arrays_or_comma_separated_values()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Orbs:Network:AllowedHosts"] = "wiki.lan, nas.lan",
            ["Orbs:Network:BlockedHosts:0"] = "evil.example",
        }).Build();

        var policy = NetworkPolicy.From(configuration);

        policy.AllowedHosts.ShouldBe(["wiki.lan", "nas.lan"]);
        policy.BlockedHosts.ShouldBe(["evil.example"]);
        NetworkPolicy.Matches("docs.wiki.lan", policy.AllowedHosts).ShouldBeTrue();
        NetworkPolicy.Matches("notwiki.lan", policy.AllowedHosts).ShouldBeFalse();
    }
}
