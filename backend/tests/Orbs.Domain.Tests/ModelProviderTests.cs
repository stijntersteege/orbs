using Orbs.Domain.Common;
using Orbs.Domain.Providers;

namespace Orbs.Domain.Tests;

public class ModelProviderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Kind_is_stored_in_lower_case()
    {
        ModelProvider.Create("Local", "OpenAI-Compatible", Now).Kind.ShouldBe("openai-compatible");
    }

    [Theory]
    [InlineData("open ai")]
    [InlineData("-leading")]
    [InlineData("under_score")]
    public void Kind_must_be_a_slug(string kind)
    {
        Should.Throw<DomainException>(() => ModelProvider.Create("Local", kind, Now));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    public void Base_url_must_be_absolute_http(string url)
    {
        var provider = ModelProvider.Create("Local", "openai-compatible", Now);

        Should.Throw<DomainException>(() => provider.PointAt(url, Now));
    }

    [Fact]
    public void A_blank_api_key_removes_the_key()
    {
        var provider = ModelProvider.Create("Local", "openai-compatible", Now);
        provider.SetProtectedApiKey("encrypted", Now);

        provider.SetProtectedApiKey("  ", Now);

        provider.HasApiKey.ShouldBeFalse();
    }

    [Fact]
    public void A_blank_default_model_means_none()
    {
        var provider = ModelProvider.Create("Local", "openai-compatible", Now);

        provider.SetDefaultModel("   ", Now);

        provider.DefaultModel.ShouldBeNull();
    }

    [Fact]
    public void Providers_can_be_switched_off_and_on()
    {
        var provider = ModelProvider.Create("Local", "echo", Now);

        provider.Disable(Now);
        provider.IsEnabled.ShouldBeFalse();

        provider.Enable(Now);
        provider.IsEnabled.ShouldBeTrue();
    }
}
