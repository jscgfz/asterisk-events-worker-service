using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Models.Store;
using Asterisk.Events.Worker.Resolvers;

namespace Asterisk.Events.Worker.Services;

internal sealed class SwitchBoardSenderService(
  ILogger<SwitchBoardSenderService> logger,
  ISwitchBoardDataService data,
  ISwitchBoardStoreService store
) : ISwitchBoardSenderService
{
  private readonly ILogger<SwitchBoardSenderService> _logger = logger;
  private readonly ISwitchBoardDataService _data = data;
  private readonly ISwitchBoardStoreService _store = store;

  public async Task Process(IEnumerable<Dictionary<string, string>> buffer)
  {
    IEnumerable<string?> changes = [];// buffer.Select(Resolve);
    foreach (Dictionary<string, string> iter in buffer)
      changes = changes.Append(Resolve(iter));

    IEnumerable<string> filter = changes.OfType<string>().Distinct();
    if (filter.Any()) await _store.Publish(filter);
  }

  private string? Resolve(Dictionary<string, string> managerEvent)
    => !managerEvent.TryGetValue("event", out string? name) ? null : name switch
    {
      "QueueMember" => QueueMember(managerEvent),
      "QueueMemberStatus" => QueueMember(managerEvent),
      "Hangup" or "AgentComplete" => _store.CloseChannel(managerEvent),
      "Unhold" or "Hold" or "Status" or "Newchannel" or "Newstate" or "AgentConnect" or "Rename" => _store.AddTimeline(managerEvent),
      _ => LogUnhandled(managerEvent)
      //_ => null
    };

  private string? LogUnhandled(Dictionary<string, string> managerEvent)
  {
    _logger.LogInformation("Unhandled event {@event}", managerEvent);
    return null;
  }

  private string? QueueMember(Dictionary<string, string> queueMember)
  {
    string[] sourceArray = ["location", "interface"];

    QueueMemberStore store = new(
      queueMember.First(p => sourceArray.Contains(p.Key)).Value,
      _data.Name(SwitchBoardResolver.PlaneInterface(queueMember.First(p => sourceArray.Contains(p.Key)).Value)),
      queueMember["membership"],
      int.Parse(queueMember["callstaken"]),
      SwitchBoardResolver.Date(long.Parse(queueMember["lastcall"])),
      queueMember["paused"] == "1",
      int.Parse(queueMember["status"]),
      queueMember["incall"] == "1",
      SwitchBoardResolver.Date(long.Parse(queueMember["logintime"])),
      queueMember["queue"]
    );

    return _store.Add(store);
  }
}
