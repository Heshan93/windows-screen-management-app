using System.Net;
using FluentAssertions;
using OrderBuddy.ScreenManager.Core.Diagnostics;
using OrderBuddy.ScreenManager.Core.Security;

namespace ScreenManager.Tests;

public class NetworkDiagnosticsTests
{
    [Theory]
    [InlineData("192.168.1.27", true)]
    [InlineData("192.168.1.250", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("169.254.10.1", true)]
    [InlineData("localhost", true)]
    [InlineData("printer.local", true)]
    [InlineData("[::1]", true)]
    [InlineData("fd00::1", true)]
    [InlineData("app.orderbuddy.tech", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("", false)]
    public void IsLocalNetworkHost_ClassifiesHosts(string host, bool expected)
    {
        NetworkAddressClassifier.IsLocalNetworkHost(host).Should().Be(expected);
    }

    [Fact]
    public void IsPrivateAddress_HandlesIpv4MappedIpv6()
    {
        NetworkAddressClassifier.IsPrivateAddress(IPAddress.Parse("::ffff:192.168.1.27")).Should().BeTrue();
    }

    [Fact]
    public void Redact_MasksSensitiveJsonValues_KeepsOthers()
    {
        const string json = "{\"account_id\":\"acp_k1\",\"password\":\"Secret123\",\"positive_auth_token\":\"0881571225925570\",\"positive_pairing_code\":\"3670\",\"terminal_ip\":\"192.168.1.250\",\"pin\":1234}";

        var result = SensitiveDataRedactor.Redact(json);

        result.Should().NotContain("Secret123");
        result.Should().NotContain("0881571225925570");
        result.Should().NotContain("3670");
        result.Should().NotContain("1234");
        result.Should().Contain("acp_k1");
        result.Should().Contain("192.168.1.250");
    }

    [Fact]
    public void Redact_MasksSensitiveFormAndQueryValues()
    {
        var result = SensitiveDataRedactor.Redact("http://192.168.1.27/proxy.php?route=start&token=abcdef123456&amount=320");

        result.Should().NotContain("abcdef123456");
        result.Should().Contain("route=start");
        result.Should().Contain("amount=320");
    }

    [Fact]
    public void Redact_MasksBearerHeader()
    {
        SensitiveDataRedactor.Redact("Authorization: Bearer eyJhbGciOi.payload.sig").Should().NotContain("eyJhbGciOi");
    }

    [Fact]
    public void Redact_DoesNotMaskKeysThatOnlyContainPinSubstring()
    {
        SensitiveDataRedactor.Redact("{\"shipping\":\"standard\"}").Should().Contain("standard");
    }

    [Fact]
    public void Redact_Fingerprint_IsStableAndHidesValue()
    {
        const string json = "{\"positive_auth_token\":\"0881571225925570\"}";

        var first = SensitiveDataRedactor.Redact(json, fingerprint: true);
        var second = SensitiveDataRedactor.Redact(json, fingerprint: true);
        var changed = SensitiveDataRedactor.Redact("{\"positive_auth_token\":\"9999999999999999\"}", fingerprint: true);

        first.Should().Be(second);
        first.Should().Contain("fp=");
        first.Should().NotContain("0881571225925570");
        changed.Should().NotBe(first);
    }

    [Fact]
    public void Redact_TruncatesLongText()
    {
        var result = SensitiveDataRedactor.Redact(new string('a', 50), maxLength: 10);
        result.Should().StartWith("aaaaaaaaaa...(+40 chars)");
    }
}
