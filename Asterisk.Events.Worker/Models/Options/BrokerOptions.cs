using Confluent.Kafka;

namespace Asterisk.Events.Worker.Models.Options;

internal sealed class BrokerOptions
{
  public ProducerConfig ProducerConfig { get; set; } = default!;
  public ConsumerConfig ConsumerConfig { get; set; } = default!;
}
