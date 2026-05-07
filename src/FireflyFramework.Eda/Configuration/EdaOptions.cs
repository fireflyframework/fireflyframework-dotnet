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
