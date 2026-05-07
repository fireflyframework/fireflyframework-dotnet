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

namespace FireflyFramework.Cqrs.Validation;

public sealed class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationFailure> Failures { get; }

    private ValidationResult(bool isValid, IReadOnlyList<ValidationFailure> failures)
    {
        IsValid = isValid;
        Failures = failures;
    }

    public static ValidationResult Successful() => new(true, Array.Empty<ValidationFailure>());

    public static ValidationResult Failed(IEnumerable<ValidationFailure> failures) => new(false, failures.ToList());

    public static ValidationResult Failed(string field, string message) =>
        new(false, new[] { new ValidationFailure(field, message) });
}

public sealed record ValidationFailure(string Field, string Message, string? Code = null);
