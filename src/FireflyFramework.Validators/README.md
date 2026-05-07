# FireflyFramework.Validators

Drop-in `System.ComponentModel.DataAnnotations` validation attributes
for the financial-services and identity values that show up
everywhere in core-banking, lending, payments, and back-office
software. Plus reusable helpers for password strength evaluation and
the modulus-N check-digit algorithms (IBAN ISO 7064 mod-97, Luhn).

Mirrors `org.fireflyframework:firefly-common-validators`. Each
attribute matches its Java counterpart's algorithm exactly, so a
payload that the Java service accepts is one a .NET service accepts
and vice versa — wire-compatibility is the design goal.

---

## Why a separate validators project?

The .NET BCL ships a useful but small set of validation attributes
(`[Required]`, `[StringLength]`, `[EmailAddress]`, `[Phone]`,
`[CreditCard]`). For a financial-services platform that needs to
reject malformed IBANs, BIC codes, sort codes, ISO 4217 currencies,
and password strength policies, the BCL is not enough — you'd write
the same regex / mod-97 logic in every service.

Pulling the family of "is-this-thing-shaped-right" rules into one
project means:

* The algorithms live in one place. The IBAN ISO 7064 mod-97
  implementation in `Internal/CheckDigit.cs` is the single source of
  truth across the whole framework.
* Service authors compose attributes on records and the rest is free.
  ASP.NET Core model binding runs the validators automatically; the
  Web layer's RFC 7807 mapper translates the failures into a
  structured `application/problem+json` response without extra glue.
* The same payload validates identically on Java and .NET. The
  algorithms aren't reinterpreted — they were ported byte-for-byte
  from the Java line.

---

## Mental model

```
                        ┌────────────────────────────┐
   model binding ──────►│   ValidationAttribute      │◄── DataAnnotations base
                        │   (System.Component…)      │
                        └────────────┬───────────────┘
                                     │
              ┌──────────────────────┼──────────────────────────────┐
              ▼                      ▼                              ▼
   ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
   │ Identity / banking   │  │  Financial values    │  │   Identity strings   │
   │ [ValidIban]          │  │  [ValidAmount]       │  │   [ValidPhoneNumber] │
   │ [ValidBic]           │  │  [ValidInterestRate] │  │   [ValidPin]         │
   │ [ValidCreditCard]    │  │  [ValidCurrencyCode] │  │   [ValidNationalId]  │
   │ [ValidCvv]           │  │  …                   │  │   [ValidTaxId]       │
   │ [ValidSortCode]      │  │                      │  │   [ValidPasswordStr] │
   │ [ValidAccountNumber] │  │                      │  │   [ValidDate]        │
   └──────────────────────┘  └──────────────────────┘  │   [ValidDateTime]    │
                                                       └──────────────────────┘
```

Each attribute is a sealed `ValidationAttribute` that returns `true`
for `null` (use `[Required]` if you want non-null) and `true` /
`false` based on a single decision rule. Errors surface through
`FormatErrorMessage(name)` and ultimately into ASP.NET Core's
`ModelStateDictionary` — and from there into the framework's RFC 7807
response.

---

## Quick start

```csharp
using FireflyFramework.Validators.Attributes;
using System.ComponentModel.DataAnnotations;

public sealed record CreatePaymentRequest(
    [Required, ValidIban]                              string DebtorIban,
    [Required, ValidIban]                              string CreditorIban,
    [Required, ValidCurrencyCode]                      string Currency,
    [Required, ValidAmount(Min = 0.01, MaxFractionDigits = 2)]
                                                       decimal Amount,
    [ValidPhoneNumber]                                 string? CallbackPhone);
```

Wire-up is zero — ASP.NET Core's automatic model validation runs
these on every request:

```csharp
app.MapPost("/api/v1/payments", (CreatePaymentRequest body) =>
{
    // If we got here, validators passed. Failures already returned
    // a 400 Bad Request with an RFC 7807 envelope.
    return Results.Accepted();
});
```

---

## Attribute reference

| Attribute | Validates | Defaults |
|---|---|---|
| `[ValidIban]` | International Bank Account Number — country prefix + length 15-34 + ISO 7064 mod-97 check digit | none |
| `[ValidBic]` | BIC / SWIFT code — 8 or 11 alphanumerics | none |
| `[ValidCreditCard]` | Card number — Luhn check digit + 13-19 digits | none |
| `[ValidCvv]` | 3 or 4 digits | none |
| `[ValidPin]` | Numeric PIN, configurable length range | `MinLength=4, MaxLength=8` |
| `[ValidSortCode]` | UK / Ireland sort code (`NN-NN-NN` or `NNNNNN`) | none |
| `[ValidAccountNumber]` | Numeric account number | `MinLength=4, MaxLength=20` |
| `[ValidCurrencyCode]` | ISO 4217 — three uppercase letters | none |
| `[ValidPhoneNumber]` | E.164 international format (leading `+`, 8-15 digits) | none |
| `[ValidAmount]` | Decimal monetary amount | `Min=0, Max=double.MaxValue, MaxFractionDigits=2, AllowNegative=false` |
| `[ValidInterestRate]` | Numeric range, fraction-digit cap | `Min=0, Max=100, MaxFractionDigits=4` |
| `[ValidDate]` | Date string parses against a pattern | `Pattern="yyyy-MM-dd"` |
| `[ValidDateTime]` | DateTime string parses against a pattern | `Pattern="yyyy-MM-ddTHH:mm:ss"` |
| `[ValidTaxId]` | Country-aware tax identifier length / format | `Country=null` (no country gating) |
| `[ValidNationalId]` | Country-aware national identity document | `Country=null` |
| `[ValidPasswordStrength]` | Length + character classes + blacklist | `MinLength=8, MaxLength=128, RequireUppercase, RequireLowercase, RequireDigit, RequireSymbol` |

Every attribute exposes the standard DataAnnotations
`ErrorMessage` / `ErrorMessageResourceType` / `ErrorMessageResourceName`
for i18n.

---

## Helpers

### `PasswordStrengthUtils.Evaluate(string password, PasswordPolicy policy)`

Underlies `[ValidPasswordStrength]` and is exposed publicly so service
code (e.g. an account-creation handler that wants to render strength
feedback as the user types) can call it directly without going through
the attribute pipeline.

```csharp
using FireflyFramework.Validators.Utilities;

var policy = new PasswordPolicy
{
    MinLength        = 12,
    MaxLength        = 128,
    RequireUppercase = true,
    RequireLowercase = true,
    RequireDigit     = true,
    RequireSymbol    = true,
    Blacklist        = new[] { "Password", "Welcome", "Letmein" },
};

var evaluation = PasswordStrengthUtils.Evaluate(candidate, policy);

if (!evaluation.IsAcceptable)
{
    foreach (var violation in evaluation.Violations)
    {
        // "password.tooShort", "password.noUppercase",
        // "password.blacklisted:Welcome", ...
        renderer.Show(i18n[violation]);
    }
}
```

`PasswordEvaluation` exposes:

* `IsAcceptable` (`bool`) — does the password meet *every* policy rule?
* `Score` (`int`, 0-6) — strength score, useful for a meter UI.
* `Violations` (`IReadOnlyList<string>`) — stable, kebab-cased
  violation codes that map cleanly onto i18n message keys.

### `CheckDigit`

Public algorithms used by the attributes:

* `CheckDigit.Iban(normalised)` — ISO 7064 mod-97 over the
  letters-as-numbers transformation. Returns `true` for a valid IBAN
  (already pre-normalised: spaces removed, upper-case).
* `CheckDigit.Luhn(digits)` — Luhn mod-10 check. Used by
  `[ValidCreditCard]`; usable independently for any Luhn-checked
  identifier.

Exposed publicly so that custom validators in consumer code can reuse
the same battle-tested implementations rather than rolling their own.

---

## Common patterns

### Custom error messages with i18n

Every attribute accepts `ErrorMessageResourceType` and
`ErrorMessageResourceName` from the DataAnnotations base class. Wire
them to a `.resx` file for localised messages:

```csharp
public sealed record CreatePaymentRequest(
    [ValidIban(
        ErrorMessageResourceType = typeof(PaymentErrors),
        ErrorMessageResourceName = "InvalidDebtorIban")]
    string DebtorIban);
```

If the resource lookup fails, the attribute falls back to the default
`'{name}' is not a valid IBAN` template.

### Programmatic validation outside ASP.NET Core

Use `Validator.TryValidateObject` from the BCL to run the same
attribute set on a record without going through model binding —
useful in CLI tools, background workers, or as a guard at a service
boundary:

```csharp
var ctx = new ValidationContext(request);
var errors = new List<ValidationResult>();
if (!Validator.TryValidateObject(request, ctx, errors, validateAllProperties: true))
{
    foreach (var err in errors)
    {
        logger.LogWarning("validation failed: {Member} {Message}",
            string.Join(",", err.MemberNames), err.ErrorMessage);
    }
}
```

### Composing with FluentValidation

If you already use FluentValidation for some of your validation
rules, the two systems compose. The DataAnnotations attributes from
this project run during ASP.NET Core model binding (before the
controller action), and FluentValidation runs at the boundary you
choose to invoke it. Avoid duplicating the same rule in both places
to keep error-handling deterministic.

---

## Pitfalls and gotchas

**`null` is always valid by design.** Every attribute returns `true`
for `null`. This is the standard DataAnnotations contract — combine
with `[Required]` if `null` should be rejected. The pattern is
`[Required, ValidIban]`, never `[ValidIban]` alone if the property
must not be null.

**`[ValidAmount]`'s `MaxFractionDigits` rejects extra precision.**
A `decimal` value of `12.345` will fail `[ValidAmount(MaxFractionDigits = 2)]`
even though the type permits 28 digits. This is by design — most
financial systems carry minor-unit precision (2 for USD, 0 for JPY).
Set `MaxFractionDigits` explicitly per currency if you accept a mix.

**Attributes don't trim whitespace.** If your client occasionally
sends `" GB29NWBK60161331926819 "` (with surrounding spaces), the
validator rejects it. Trim at the model-binding layer or change the
property type to `string` and trim in the setter.

**`[ValidPhoneNumber]` is E.164 only.** It rejects domestic-format
numbers like `(212) 555-0100`. If you need to accept domestic format
and normalise to E.164, do that in a custom binder before validation.

**Don't mix attribute and constructor validation.** A `record` with
attributes on the positional parameters validates *after*
construction. If your record's primary constructor throws on bad
input, the validation pipeline never runs. Use the attributes alone
or wrap the construction in a factory; not both.

**Custom error messages must be culture-invariant or carry the
resource type.** Plain string `ErrorMessage` is rendered as-is —
ASP.NET Core won't localise it. Use the resource form for i18n.

---

## Internals (for the curious)

The IBAN mod-97 implementation in `Internal/CheckDigit.cs` does the
classical "rotate the four leading characters to the back, replace
letters by their A=10..Z=35 numeric values, treat as a big integer,
mod 97, expect 1" — but processes the string a chunk at a time
(9-digit windows) so it never allocates a `BigInteger`. That matches
the Java implementation byte-for-byte and runs in single-digit
microseconds for typical IBAN lengths.

The Luhn implementation iterates from the rightmost digit and
doubles every second digit, summing the digits of the doubled value
when it exceeds 9. This is faster than mapping through a lookup
table for short inputs (the median credit-card number is 16 digits)
and is what every textbook Luhn implementation does.

`PasswordStrengthUtils.Evaluate` builds the violation list lazily —
if a candidate fails on length, it short-circuits and skips the
character-class checks. The score is computed from the
*non-violated* checks, so a password that passes all four
classes-required checks plus length plus blacklist scores 6.

The `PasswordEvaluation` record is intentionally not bound to a
specific exception type. Consumers that want to throw
`FireflySecurityException` on a failure do that themselves; consumers
that just want to render strength feedback to the UI keep the
evaluation as data.

---

## Dependencies

| Reference | Used for |
|---|---|
| `FireflyFramework.Kernel` (project) | Calendar version, base exception (transitive) |
| `System.ComponentModel.Annotations` (BCL) | `ValidationAttribute` base type |
| `FluentValidation` (NuGet, transitive via Web) | Not used directly here — but the framework's Web layer uses FluentValidation for some flows |

The project itself is intentionally tiny in dependency footprint.

---

## Java mapping

| .NET attribute | Java annotation |
|---|---|
| `[ValidIban]` | `@ValidIban` |
| `[ValidBic]` | `@ValidBic` |
| `[ValidCreditCard]` | `@ValidCreditCard` |
| `[ValidCvv]` | `@ValidCVV` |
| `[ValidPin]` | `@ValidPIN` |
| `[ValidSortCode]` | `@ValidSortCode` |
| `[ValidAccountNumber]` | `@ValidAccountNumber` |
| `[ValidCurrencyCode]` | `@ValidCurrencyCode` |
| `[ValidPhoneNumber]` | `@ValidPhoneNumber` |
| `[ValidAmount]` | `@ValidAmount` |
| `[ValidInterestRate]` | `@ValidInterestRate` |
| `[ValidDate]` | `@ValidDate` |
| `[ValidDateTime]` | `@ValidDateTime` |
| `[ValidTaxId]` | `@ValidTaxId` |
| `[ValidNationalId]` | `@ValidNationalId` |
| `[ValidPasswordStrength]` | `@ValidPasswordStrength` |
| `PasswordStrengthUtils` | `org.fireflyframework.common.validators.PasswordStrengthUtils` |
| `CheckDigit` | `org.fireflyframework.common.validators.internal.CheckDigit` |

The semantics are wire-compatible: a payload that passes Java
validation passes .NET validation and vice versa. The constructor
parameter names on the attributes use camelCase on Java
(`@ValidAmount(min = ...)`) and PascalCase on .NET
(`[ValidAmount(Min = ...)]`) — that's idiomatic per language and the
underlying validation rule is identical.

---

## See also

* [`FireflyFramework.Web`](../FireflyFramework.Web/README.md) — `ValidationException` and the RFC 7807 mapping layer that surfaces validator failures as structured problem responses.
* [`docs/CONFIGURATION.md`](../../docs/CONFIGURATION.md) — `Firefly:*` configuration sections.
