# FireflyFramework.Validators

Drop-in `System.ComponentModel.DataAnnotations` validation attributes for
financial and identity values, plus reusable validation helpers. Mirrors
`org.fireflyframework:firefly-common-validators`.

Each attribute matches its Java counterpart's algorithm exactly, so a
payload accepted by one platform is accepted by the other.

## Attribute reference

| Attribute                   | Validates                                                                               |
|-----------------------------|-----------------------------------------------------------------------------------------|
| `[ValidIban]`               | International Bank Account Number — country prefix, length 15-34, ISO 7064 mod-97 check |
| `[ValidBic]`                | BIC / SWIFT code (8 or 11 characters)                                                   |
| `[ValidCreditCard]`         | Card number, Luhn check, 13-19 digits                                                   |
| `[ValidCvv]`                | 3 or 4 numeric digits                                                                   |
| `[ValidPin]`                | Numeric PIN, configurable length range                                                  |
| `[ValidSortCode]`           | UK / Ireland sort code (`NN-NN-NN` or `NNNNNN`)                                         |
| `[ValidAccountNumber]`      | Numeric account number, configurable length                                             |
| `[ValidCurrencyCode]`       | ISO 4217 code (3 uppercase letters)                                                     |
| `[ValidPhoneNumber]`        | E.164 international format                                                              |
| `[ValidAmount]`             | Decimal monetary amount with bounds, max fraction digits, sign control                  |
| `[ValidInterestRate]`       | Numeric range; defaults to 0-100 inclusive                                              |
| `[ValidDate]`               | Pattern-based date parsing                                                              |
| `[ValidDateTime]`           | Pattern-based datetime parsing                                                          |
| `[ValidTaxId]`              | Tax identification length / format check                                                |
| `[ValidNationalId]`         | National identity document length / format check                                        |
| `[ValidPasswordStrength]`   | Length + character classes + blacklist (delegates to `PasswordStrengthUtils`)           |

## Usage

Compose attributes on a record / class as you would any other DataAnnotation
attribute. ASP.NET Core model binding will run them automatically.

```csharp
using FireflyFramework.Validators.Attributes;

public sealed record CreatePaymentRequest(
    [ValidIban]                            string DebtorIban,
    [ValidIban]                            string CreditorIban,
    [ValidCurrencyCode]                    string Currency,
    [ValidAmount(Min = 0.01)]              decimal Amount,
    [ValidPasswordStrength(MinLength = 12, RequireSymbol = true)] string AuthorisationCode);
```

## Helpers

### `PasswordStrengthUtils.Evaluate`

Evaluates a candidate password against a `PasswordPolicy`:

```csharp
using FireflyFramework.Validators.Utilities;

var policy = new PasswordPolicy
{
    MinLength        = 12,
    RequireUppercase = true,
    RequireLowercase = true,
    RequireDigit     = true,
    RequireSymbol    = true,
    Blacklist        = new[] { "Password", "Welcome" },
};

var evaluation = PasswordStrengthUtils.Evaluate(candidate, policy);
if (!evaluation.IsAcceptable)
{
    foreach (var violation in evaluation.Violations)
    {
        // password.tooShort, password.noUppercase, password.blacklisted:Welcome, ...
    }
}
```

`PasswordEvaluation` exposes `IsAcceptable`, a 0-6 strength score, and an
`IReadOnlyList<string>` of violation codes that map cleanly onto i18n keys.

### `CheckDigit`

Helper exposing modulus-N check-digit routines (IBAN ISO 7064 mod-97,
Luhn) used by the attributes. Public so callers can reuse the algorithms
outside DataAnnotations.

## Dependencies

| Reference                            | Used for                         |
|--------------------------------------|----------------------------------|
| `FireflyFramework.Kernel`            | Calendar version                 |
| `System.ComponentModel.Annotations`  | `ValidationAttribute` base type  |

## Java mapping

| .NET attribute            | Java annotation                                                |
|---------------------------|----------------------------------------------------------------|
| `[ValidIban]`             | `@ValidIban`                                                   |
| `[ValidBic]`              | `@ValidBic`                                                    |
| `[ValidCreditCard]`       | `@ValidCreditCard`                                             |
| `[ValidCvv]`              | `@ValidCVV`                                                    |
| `[ValidPin]`              | `@ValidPIN`                                                    |
| `[ValidSortCode]`         | `@ValidSortCode`                                               |
| `[ValidAccountNumber]`    | `@ValidAccountNumber`                                          |
| `[ValidCurrencyCode]`     | `@ValidCurrencyCode`                                           |
| `[ValidPhoneNumber]`      | `@ValidPhoneNumber`                                            |
| `[ValidAmount]`           | `@ValidAmount`                                                 |
| `[ValidInterestRate]`     | `@ValidInterestRate`                                           |
| `[ValidDate]`             | `@ValidDate`                                                   |
| `[ValidDateTime]`         | `@ValidDateTime`                                               |
| `[ValidTaxId]`            | `@ValidTaxId`                                                  |
| `[ValidNationalId]`       | `@ValidNationalId`                                             |
| `[ValidPasswordStrength]` | `@ValidPasswordStrength`                                       |
| `PasswordStrengthUtils`   | `org.fireflyframework.common.validators.PasswordStrengthUtils` |
| `CheckDigit`              | `org.fireflyframework.common.validators.internal.CheckDigit`   |
