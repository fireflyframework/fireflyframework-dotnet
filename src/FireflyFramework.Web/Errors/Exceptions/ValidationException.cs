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

using FireflyFramework.Web.Errors.Models;

namespace FireflyFramework.Web.Errors.Exceptions;

/// <summary>400 Bad Request — input validation failure with optional per-field errors.</summary>
public class ValidationException : ServiceException
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(string message, IEnumerable<ValidationError>? errors = null, Exception? cause = null)
        : base(message, 400, "VALIDATION_FAILED", ErrorCategory.Validation, ErrorSeverity.Medium, false, null, cause)
    {
        Errors = (errors ?? Array.Empty<ValidationError>()).ToList();
    }
}
