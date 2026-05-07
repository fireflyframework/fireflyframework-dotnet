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

namespace FireflyFramework.Eda.Configuration;

public sealed class EdaOptions
{
    public const string SectionName = "Firefly:Eda";

    public Events.PublisherType DefaultPublisher { get; set; } = Events.PublisherType.Auto;
    public Events.ConsumerType DefaultConsumer { get; set; } = Events.ConsumerType.Auto;
    public KafkaOptions Kafka { get; set; } = new();
    public RabbitMqOptions RabbitMq { get; set; } = new();
}

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string? GroupId { get; set; }
    public string? SchemaRegistryUrl { get; set; }
}

public sealed class RabbitMqOptions
{
    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}
