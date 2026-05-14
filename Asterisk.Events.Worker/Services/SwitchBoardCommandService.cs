using System.Text.Json;
using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Models.ViewModels;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Asterisk.Events.Worker.Services;

internal sealed class SwitchBoardCommandService(
  IConsumer<string, string> consumer,
  ISwitchBoardEventService events,
  ISwitchBoardStoreService store,
  ILogger<SwitchBoardCommandService> logger
) : BackgroundService
{
  private readonly IConsumer<string, string> _consumer = consumer;
  private readonly ISwitchBoardEventService _events = events;
  private readonly ISwitchBoardStoreService _store = store;
  private readonly ILogger<SwitchBoardCommandService> _logger = logger;


  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _consumer.Subscribe("ami-commands");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);
        switch (result.Message.Key)
        {
          case "hangup":
            await _events.SendAction(
              _events
                .ActionBuilder()
                .WithActionName("Hangup")
                .WithArgument("Channel", result.Message.Value)
            );
            break;
          case "queuechange":
            await Task.WhenAll(
              ResolveCahnges(JsonSerializer.Deserialize<JsonElement>(result.Message.Value))
            );
            break;
          case "custom-event":
            _events.AppenCustomEvent(
              JsonSerializer.Deserialize<Dictionary<string, string>>(result.Message.Value)!
            );
            break;
        }
      }
      catch (ConsumeException e)
      {
        _logger.LogError(e, "Kafka consume error - {reason}", e.Error.Reason);
      }
    }
  }

  private IEnumerable<Task> ResolveCahnges(JsonElement payload)
  {
    if (
      payload.TryGetProperty("action", out JsonElement action) &&
      payload.TryGetProperty("members", out JsonElement members) &&
      payload.TryGetProperty("queueId", out JsonElement queueId) &&
      members.ValueKind == JsonValueKind.Array
    )
      return members.EnumerateArray().Select(member => _events.SendAction(
        _events.ActionBuilder()
          .WithActionName(action.GetString() == "add" ? "QueueAdd" : "QueueRemove")
          .WithArguments(
            KeyValuePair.Create("Interface", member.GetString()!),
            KeyValuePair.Create("Queue", queueId.GetString()!)
          )
      ));
    return [];
  }
}
