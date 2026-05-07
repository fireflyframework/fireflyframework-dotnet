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

using FireflyFramework.Web.Errors.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FireflyFramework.Web.Errors.Converters;

/// <summary>
/// Walks the registered <see cref="IExceptionConverter"/>s, returning the first one
/// whose target type matches the thrown exception. Mirrors Java
/// <c>ExceptionConverterService</c>.
/// </summary>
public sealed class ExceptionConverterRegistry
{
    private readonly IReadOnlyList<IExceptionConverter> _converters;

    public ExceptionConverterRegistry(IEnumerable<IExceptionConverter> converters)
    {
        _converters = converters.ToList();
    }

    public ServiceException? TryConvert(Exception exception, HttpContext context)
    {
        foreach (var converter in _converters)
        {
            if (converter.ExceptionType.IsInstanceOfType(exception))
            {
                return converter.Convert(exception, context);
            }
        }

        return null;
    }
}
