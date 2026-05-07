// Copyright 2024-2026 Firefly Software Foundation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0

namespace FireflyFramework.Messaging.Annotations;

/// <summary>Subscribes a method to a destination on the in-process broker.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MessageListenerAttribute : Attribute
{
    public MessageListenerAttribute(string destination) { Destination = destination; }
    public string Destination { get; }
    public string? Selector { get; init; }
}

/// <summary>Marks a method that maps an incoming destination to an outbound reply.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SendToAttribute : Attribute
{
    public SendToAttribute(string destination) { Destination = destination; }
    public string Destination { get; }
}
