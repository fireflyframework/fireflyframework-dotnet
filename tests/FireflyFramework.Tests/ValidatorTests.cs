using System.ComponentModel.DataAnnotations;
using FireflyFramework.Validators.Attributes;
using FireflyFramework.Validators.Utilities;
using FluentAssertions;
using Xunit;

namespace FireflyFramework.Tests;

public class ValidatorTests
{
    [Theory]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]   // Deutsche Bank sample
    [InlineData("GB82 WEST 1234 5698 7654 32", true)]   // UK sample
    [InlineData("XX00 NOTAREAL IBAN", false)]
    public void ValidIban_evaluates_iso7064_correctly(string value, bool expected)
    {
        new ValidIbanAttribute().IsValid(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("4111111111111111", true)]   // Visa test PAN (Luhn-valid)
    [InlineData("4111111111111112", false)]
    public void ValidCreditCard_uses_luhn(string value, bool expected)
    {
        new ValidCreditCardAttribute().IsValid(value).Should().Be(expected);
    }

    [Fact]
    public void PasswordStrength_flags_missing_classes()
    {
        var policy = new PasswordPolicy();
        var result = PasswordStrengthUtils.Evaluate("alllowercase", policy);
        result.IsAcceptable.Should().BeFalse();
        result.Violations.Should().Contain("password.noUppercase");
    }
}
