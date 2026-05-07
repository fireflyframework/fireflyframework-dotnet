// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Messaging.Core;

/// <summary>
/// Generic message broker port. Lightweight alternative to the rich EDA
/// module — for cases that only need fire-and-forget send / single-consumer
/// receive on a logical destination.
/// </summary>
public interface IMessageBroker
{
    Task SendAsync<T>(string destination, Message<T> message, CancellationToken ct = default);
    IDisposable Subscribe<T>(string destination, Func<Message<T>, CancellationToken, Task> handler);
}

/// <summary>Handler discovered by attribute scanning.</summary>
public interface IMessageHandler<T>
{
    Task HandleAsync(Message<T> message, CancellationToken ct);
}
