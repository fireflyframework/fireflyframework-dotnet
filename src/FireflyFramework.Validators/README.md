# FireflyFramework.Validators

Drop-in validation attributes for financial and domain-specific values. Mirrors `fireflyframework-validators`.

## Attributes

| Attribute | Validates |
|---|---|
| `[ValidIban]` | International Bank Account Number — country prefix + length + ISO 7064 mod-97 |
| `[ValidBic]` | BIC/SWIFT code (8 or 11 chars) |
| `[ValidCreditCard]` | Card number with Luhn check (13–19 digits) |
| `[ValidCvv]` | 3 or 4 numeric digits |
| `[ValidCurrencyCode]` | ISO 4217 code (3 uppercase letters), against the runtime culture set |
| `[ValidPhoneNumber]` | E.164 international format |
| `[ValidAmount]` | Decimal monetary amount with bounds + max fraction digits + sign control |
| `[ValidInterestRate]` | Numeric range, defaults 0–100 |
| `[ValidDate]` / `[ValidDateTime]` | Pattern-based parsing |
| `[ValidPin]` | Numeric PIN, configurable length range |
| `[ValidSortCode]` | UK/Ireland sort code (NN-NN-NN) |
| `[ValidAccountNumber]` | Numeric account number, configurable length |
| `[ValidTaxId]` | Country-agnostic tax id length check |
| `[ValidNationalId]` | National identity document length check |
| `[ValidPasswordStrength]` | Length + character classes + blacklist |

## Example

```csharp
public sealed record CreatePaymentRequest(
    [property: ValidIban]               string DebtorIban,
    [property: ValidIban]               string CreditorIban,
    [property: ValidCurrencyCode]       string Currency,
    [property: ValidAmount(Min = 0.01)] decimal Amount,
    [property: ValidPasswordStrength(MinLength = 12, RequireSymbol = true)] string AuthorisationCode);
```

## Helpers

`PasswordStrengthUtils.Evaluate(password, policy)` returns a `PasswordEvaluation` with `IsAcceptable`, a 0-6 strength score and a list of violation codes (`password.tooShort`, `password.noUppercase`, `password.blacklisted:<term>`, etc.). Use it from FluentValidation rules or service code.
