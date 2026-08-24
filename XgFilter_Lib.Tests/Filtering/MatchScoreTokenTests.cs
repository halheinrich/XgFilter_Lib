using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Filtering;

/// <summary>
/// The score-token grammar's own suite: the exported vocabulary and the
/// wordless verdict. Everything here pins <em>facts</em> — the spellings, the
/// fault, the replacement data — and nothing pins a sentence: the lib rules on
/// the token, the consumer words the ruling
/// (halheinrich/backgammon#121).
/// </summary>
public class MatchScoreTokenTests
{
    // -----------------------------------------------------------------------
    //  The exported vocabulary
    // -----------------------------------------------------------------------

    [Fact]
    public void TokenSpellings_AreTheExportedConstants()
    {
        // The spellings live here once; a consumer renders these rather than
        // its own literals, so this is the pin that a rename is a deliberate,
        // visible act.
        MatchScoreToken.MoneyWithJacoby.Should().Be("moneyJ");
        MatchScoreToken.MoneyWithoutJacoby.Should().Be("moneyNJ");
        MatchScoreToken.RetiredMoney.Should().Be("money");
    }

    [Fact]
    public void RetiredMoneyReplacements_AreBothRuleBearingTokens_InOrder()
    {
        // The data behind the Retired fault: both tokens, because listing both
        // is what the retired token used to mean.
        MatchScoreToken.RetiredMoneyReplacements.Should().Equal(
            MatchScoreToken.MoneyWithJacoby,
            MatchScoreToken.MoneyWithoutJacoby);
    }

    [Fact]
    public void RetiredMoneyReplacements_AreNotThemselvesFaulted()
    {
        // A replacement a consumer offers must be a token this grammar
        // accepts — otherwise the repair would re-fault.
        foreach (string replacement in MatchScoreToken.RetiredMoneyReplacements)
            MatchScoreToken.GetFault(replacement).Should().Be(MatchScoreTokenFault.None);
    }

    // -----------------------------------------------------------------------
    //  GetFault: the typed verdict
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("3a5a")]
    [InlineData("1a5aC")]
    [InlineData("1a1a")]        // post-Crawford tie / the 1-point match's only game
    [InlineData("1a2aC")]
    [InlineData("4A5A")]        // uppercase away-separators
    [InlineData("1a5ac")]       // lowercase Crawford suffix
    [InlineData(" 4a5a ")]      // trimmed
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void GetFault_AcceptedTokens_ReturnNone(string token)
    {
        MatchScoreToken.GetFault(token).Should().Be(MatchScoreTokenFault.None);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("MONEY")]
    [InlineData("Money")]
    [InlineData(" money ")]
    public void GetFault_RetiredMoneyToken_ReturnsRetired(string token)
    {
        // The distinction the fault vocabulary exists for: this token is not
        // merely wrong, it is a spelling that used to work — so a consumer can
        // name its replacements instead of just refusing it.
        MatchScoreToken.GetFault(token).Should().Be(MatchScoreTokenFault.Retired);
    }

    [Theory]
    [InlineData("moneyj")]      // the money tokens follow the grammar's casing rule
    [InlineData("MONEYJ")]
    [InlineData("moneynj")]
    [InlineData("MoNeYnJ")]
    [InlineData(" moneyNJ ")]
    public void GetFault_MoneyTokenCasingAndWhitespaceVariants_ReturnNone(string token)
    {
        MatchScoreToken.GetFault(token).Should().Be(MatchScoreTokenFault.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("3a5")]
    [InlineData("4 a 5a")]      // embedded whitespace: trimming strips ends only
    [InlineData("3a5a5a")]
    [InlineData("-1a5a")]
    [InlineData("moneyX")]      // a near-miss on the money spelling is not money
    [InlineData("money J")]
    [InlineData("moneyNJJ")]
    [InlineData("0a5a")]        // well-formed, impossible: a 0-away side has won
    [InlineData("5a0a")]
    [InlineData("3a5aC")]       // Crawford without a 1-away side
    [InlineData("1a1aC")]       // a (1,1) game is always post-Crawford
    public void GetFault_RejectedTokens_ReturnMalformed(string token)
    {
        // Wrong characters and impossible-but-well-formed scores share the
        // Malformed member: the user's remedy is the same in both cases, and
        // neither is retired vocabulary.
        MatchScoreToken.GetFault(token).Should().Be(MatchScoreTokenFault.Malformed);
    }

    [Fact]
    public void GetFault_NullToken_ReturnsMalformedRatherThanThrowing()
    {
        // An explicit JSON null can reach a deserialized MatchScores list, and
        // FilterConfig.GetInvalidFields never throws — judging a value is not
        // using it.
        MatchScoreToken.GetFault(null).Should().Be(MatchScoreTokenFault.Malformed);
    }

    [Fact]
    public void GetFault_NeverThrows_OnAnyInput()
    {
        string?[] inputs =
        [
            null, "", "   ", "money", "moneyJ", "garbage", "0a0a", "1a1aC",
            "\t", "\n", new string('9', 400), "9999999999a5a",
        ];

        foreach (string? input in inputs)
        {
            var act = () => MatchScoreToken.GetFault(input);
            act.Should().NotThrow($"'{input}' must be judged, not rejected by exception");
        }
    }

    [Fact]
    public void GetFault_AwayCountTooLargeForInt_IsMalformedNotAnOverflow()
    {
        // \d+ matches it, int.TryParse does not — the shape check has to
        // absorb that rather than let an overflow escape.
        MatchScoreToken.GetFault("9999999999a5a").Should().Be(MatchScoreTokenFault.Malformed);
    }
}
