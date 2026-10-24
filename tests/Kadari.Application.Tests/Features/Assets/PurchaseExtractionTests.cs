using Kadari.Application.Features.Assets.Services;

namespace Kadari.Application.Tests.Features.Assets;

/// <summary>The model's reply is untrusted (CARE/06 §3–4): every field is checked and dropped, never repaired.</summary>
public class PurchaseExtractionTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    [Fact]
    public void Parse_ReadsEachFieldWithItsConfidence()
    {
        const string reply = """
            {"retailer":{"value":"Makro Woodmead","confidence":0.92},"purchaseDate":{"value":"2026-03-14","confidence":0.88},
             "brand":{"value":"Bosch","confidence":0.7},"model":null,"price":{"value":11499.00,"confidence":0.81}}
            """;

        var proposal = PurchaseExtraction.Parse(reply, Today);

        Assert.Equal(
            ("Makro Woodmead", 0.92, "2026-03-14", 1_149_900L, (ProposedValueDto?)null),
            (proposal.Retailer!.Value, proposal.Retailer.Confidence, proposal.PurchasedOn!.Value, proposal.Price!.AmountMinor, proposal.Model));
    }

    [Fact]
    public void Parse_DropsAFutureDate()
        => Assert.Null(PurchaseExtraction.Parse("""{"purchaseDate":{"value":"2027-01-01","confidence":1},"brand":"Bosch"}""", Today).PurchasedOn);

    [Fact]
    public void Parse_DropsAnUnparseableDateRatherThanRepairingIt()
        => Assert.Null(PurchaseExtraction.Parse("""{"purchaseDate":"14/03/2026","brand":"Bosch"}""", Today).PurchasedOn);

    [Fact]
    public void Parse_DropsAnImplausiblePrice()
        => Assert.Null(PurchaseExtraction.Parse("""{"price":{"value":99999999,"confidence":1},"brand":"Bosch"}""", Today).Price);

    [Fact]
    public void Parse_KeepsASerialExactlyAsPrinted()
        => Assert.Equal("FD 9908 / 01234", PurchaseExtraction.Parse("""{"serialNumber":{"value":" FD 9908 / 01234 ","confidence":0.9}}""", Today).SerialNumber!.Value);

    [Fact]
    public void Parse_AcceptsAFencedReplyAndABareValueAtUnknownConfidence()
    {
        var proposal = PurchaseExtraction.Parse("```json\n{\"brand\":\"Hisense\"}\n```", Today);

        Assert.Equal(("Hisense", 0.5), (proposal.Brand!.Value, proposal.Brand.Confidence));
    }

    [Fact]
    public void Parse_WithNothingUsable_SaysNothingWasRead()
    {
        var proposal = PurchaseExtraction.Parse("""{"brand":null,"model":{"value":""}}""", Today);

        Assert.Equal((false, ExtractionUnavailable.NothingRead), (proposal.Read, proposal.Unavailable));
    }

    [Fact]
    public void Parse_NotJson_SaysNothingWasRead()
        => Assert.False(PurchaseExtraction.Parse("I could not read this receipt.", Today).Read);
}
