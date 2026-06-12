using Application.Common.Observability;

namespace Application.Tests.Common.Observability;

public class PiiScrubberTests
{
    [Fact]
    public void Scrub_NullInput_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, PiiScrubber.Scrub(null));
    }

    [Fact]
    public void Scrub_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PiiScrubber.Scrub(string.Empty));
    }

    [Theory]
    [InlineData("plain text with no pii")]
    [InlineData("line numbers like 1234567890 should pass")]
    [InlineData("hex value 0x1234abcd")]
    [InlineData("version 1.6.7.2")]
    public void Scrub_NoPii_LeavesInputUnchanged(string input)
    {
        Assert.Equal(input, PiiScrubber.Scrub(input));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name+tag@subdomain.example.com")]
    [InlineData("contact: john_doe@gmail.com please")]
    [InlineData("ім'я <a@b.co>")]                           // angle-bracket form
    [InlineData("multiple a@b.com and c@d.org in one line")]
    public void Scrub_EmailFormats_AreRedacted(string input)
    {
        var result = PiiScrubber.Scrub(input);
        Assert.DoesNotContain("@", result);
        Assert.Contains(PiiScrubber.EmailPlaceholder, result);
    }

    [Fact]
    public void Scrub_MultipleEmails_AllRedacted()
    {
        var input = "from a@x.com to b@y.com via c@z.com";
        var result = PiiScrubber.Scrub(input);
        Assert.Equal("from [email] to [email] via [email]", result);
    }

    [Theory]
    [InlineData("+380671234567")]
    [InlineData("380671234567")]
    [InlineData("+380 67 123 45 67")]
    [InlineData("+38 (067) 123-45-67")]
    [InlineData("+38-067-123-45-67")]
    [InlineData("call me at +380 (67) 123 45 67 anytime")]
    public void Scrub_UkrainianInternationalPhones_AreRedacted(string input)
    {
        var result = PiiScrubber.Scrub(input);
        Assert.Contains(PiiScrubber.PhonePlaceholder, result);
        // The recognisable phone digit groups should be gone.
        Assert.DoesNotContain("123 45 67", result);
        Assert.DoesNotContain("123-45-67", result);
        Assert.DoesNotContain("1234567", result);
    }

    [Theory]
    [InlineData("0671234567")]
    [InlineData("067-123-45-67")]
    [InlineData("(067) 123-45-67")]
    [InlineData("(067) 123 45 67")]
    public void Scrub_UkrainianLocalPhones_AreRedacted(string input)
    {
        var result = PiiScrubber.Scrub(input);
        Assert.Contains(PiiScrubber.PhonePlaceholder, result);
    }

    [Theory]
    [InlineData("+14155552671")]
    [InlineData("call +442071234567 from London")]
    public void Scrub_GenericE164_AreRedacted(string input)
    {
        var result = PiiScrubber.Scrub(input);
        Assert.Contains(PiiScrubber.PhonePlaceholder, result);
    }

    [Fact]
    public void Scrub_MixedEmailAndPhone_BothRedacted()
    {
        var input = "Contact: a@b.com, +380671234567";
        var result = PiiScrubber.Scrub(input);
        Assert.Contains(PiiScrubber.EmailPlaceholder, result);
        Assert.Contains(PiiScrubber.PhonePlaceholder, result);
    }

    [Fact]
    public void Scrub_MultiLineInput_HandlesAllLines()
    {
        var input = "Line one: a@b.com\nLine two: 0671234567\nLine three: clean";
        var result = PiiScrubber.Scrub(input);
        Assert.Contains(PiiScrubber.EmailPlaceholder, result);
        Assert.Contains(PiiScrubber.PhonePlaceholder, result);
        Assert.Contains("Line three: clean", result);
    }

    [Fact]
    public void Scrub_AlreadyRedacted_LeavesPlaceholdersIntact()
    {
        var alreadyClean = "Contact: [email], phone [phone]";
        var result = PiiScrubber.Scrub(alreadyClean);
        Assert.Equal(alreadyClean, result);
    }

    [Fact]
    public void Scrub_TwicePidempotent_NoDoubleReplacement()
    {
        var input = "email a@b.com phone +380671234567";
        var once  = PiiScrubber.Scrub(input);
        var twice = PiiScrubber.Scrub(once);
        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("at line 1234567")]               // 7 digits — too short
    [InlineData("offset 123456789")]              // 9 digits, no leading 0
    [InlineData("version 1.6.7")]
    public void Scrub_PlainDigitBlobs_AreNotMistakenForPhones(string input)
    {
        var result = PiiScrubber.Scrub(input);
        Assert.DoesNotContain(PiiScrubber.PhonePlaceholder, result);
    }
}
