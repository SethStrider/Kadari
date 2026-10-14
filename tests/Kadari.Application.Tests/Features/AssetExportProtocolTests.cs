using Kadari.Application.Abstractions.Reports;
using Kadari.Application.Features.Exit;

namespace Kadari.Application.Tests.Features;

public class AssetExportProtocolTests
{
    private static readonly IReportIntegritySigner Signer = new FakeSigner();

    private static AssetForSalePackage APackage() => new(
        AssetId: Guid.CreateVersion7(),
        PassportVerificationId: "AP-ABC123",
        DisplayName: "Samsung 65\" TV",
        Category: "Electronics",
        Assurance: "ValueSupported",
        ReplacementValueMinor: 2_749_900,
        Currency: "ZAR",
        ImageReference: "kadari/cust/still.img",
        IssuedAt: DateTimeOffset.UnixEpoch);

    [Fact]
    public void Encode_then_decode_round_trips_the_package()
    {
        var package = APackage();

        var token = AssetExportProtocol.Encode(package, Signer);
        var ok = AssetExportProtocol.TryDecode(token, Signer, out var decoded);

        Assert.True(ok);
        Assert.Equal(package, decoded);
    }

    [Fact]
    public void A_tampered_payload_fails_verification()
    {
        var token = AssetExportProtocol.Encode(APackage(), Signer);

        // Flip a character in the payload half (before the '.') — the signature no longer matches.
        var dot = token.LastIndexOf('.');
        var tampered = (token[0] == 'A' ? 'B' : 'A') + token[1..dot] + token[dot..];

        Assert.False(AssetExportProtocol.TryDecode(tampered, Signer, out var decoded));
        Assert.Null(decoded);
    }

    [Fact]
    public void A_wrong_signature_fails_verification()
    {
        var token = AssetExportProtocol.Encode(APackage(), Signer);
        var payload = token[..token.LastIndexOf('.')];

        Assert.False(AssetExportProtocol.TryDecode($"{payload}.not-the-signature", Signer, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-dot")]
    [InlineData("payload.")]
    [InlineData(".signature")]
    public void A_malformed_token_is_rejected(string token)
    {
        Assert.False(AssetExportProtocol.TryDecode(token, Signer, out _));
    }

    // A deterministic stand-in for the HMAC signer: Sign is a stable transform, Verify recomputes it.
    private sealed class FakeSigner : IReportIntegritySigner
    {
        public string Sign(string canonicalContent) => Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("k:" + canonicalContent)));

        public bool Verify(string canonicalContent, string signature) => Sign(canonicalContent) == signature;
    }
}
