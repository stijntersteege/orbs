using Orbs.Infrastructure.Notifications;

namespace Orbs.Infrastructure.Tests;

public class VapidTests
{
    [Fact]
    public void Generated_keys_are_an_uncompressed_p256_point_and_a_32_byte_secret()
    {
        var (publicKey, privateKey) = VapidKeys.Generate();

        Decode(publicKey).Length.ShouldBe(65);
        Decode(publicKey)[0].ShouldBe((byte)0x04);
        Decode(privateKey).Length.ShouldBe(32);
        publicKey.ShouldNotContain("=");
    }

    private static byte[] Decode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
