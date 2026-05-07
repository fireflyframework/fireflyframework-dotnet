// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
