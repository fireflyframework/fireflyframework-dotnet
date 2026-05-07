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

using Microsoft.Extensions.DependencyInjection;

namespace FireflyFramework.Callbacks.Sdk;

public static class CallbackClientExtensions
{
    /// <summary>
    /// Registers <see cref="ICallbackClient"/> backed by a typed <see cref="HttpClient"/>
    /// targeting the supplied base address. Mirrors the
    /// <c>AddOrdersServiceClient</c> pattern from the canonical service Sdk in
    /// <c>samples/FireflyFramework.Samples.OrdersService.Sdk</c>.
    /// </summary>
    public static IHttpClientBuilder AddCallbackClient(this IServiceCollection services, Uri baseAddress) =>
        services.AddHttpClient<ICallbackClient, CallbackClient>(http => http.BaseAddress = baseAddress);
}
