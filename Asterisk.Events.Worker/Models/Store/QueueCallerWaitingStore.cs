using Asterisk.Events.Worker.Resolvers;

namespace Asterisk.Events.Worker.Models.Store;
public sealed class QueueCallerWaitingStore
{
  private readonly static IEnumerable<string> _validEvents = ["QueueEntry", "QueueCallerJoin"];
  public required string UniqueId { get; set; }
  public string? LinkedId { get; set; }
  public required DateTime EntryDate { get; set; }
  public required string Queue { get; set; }
  public required int Position { get; set; }
  public required string CallerNumber { get; set; }
  public required Dictionary<string, string> Event { get; set; }

  public static QueueCallerWaitingStore FromEntry(Dictionary<string, string> entry)
  {
    string? eventName = entry.GetValueOrDefault("event") ?? throw new InvalidDataException();
    if (!_validEvents.Contains(eventName)) throw new InvalidDataException();

    string queue = entry.GetValueOrDefault(nameof(queue)) ?? throw new ArgumentException(nameof(queue));
    string uniqueid = entry.GetValueOrDefault(nameof(uniqueid)) ?? throw new ArgumentException(nameof(uniqueid));
    string? linkedid = entry.GetValueOrDefault(nameof(linkedid));
    DateTime entryDate = eventName == "QueueEntry" ?
      DateTime.UtcNow.AddSeconds(int.Parse(entry.GetValueOrDefault("wait") ?? "0") * -1) :
      SwitchBoardResolver.DateFromTimeStamp(entry.GetValueOrDefault("timestamp") ?? throw new ArgumentException(nameof(entryDate)));
    if (eventName == "QueueEntry") entry.Add("timestamp", new DateTimeOffset(entryDate).ToUnixTimeSeconds().ToString());
    int position = int.Parse(entry.GetValueOrDefault(nameof(position)) ?? "0");
    string calleridnum = entry.GetValueOrDefault("calleridnum") ?? "unknown";

    return new()
    {
      CallerNumber = calleridnum,
      EntryDate = entryDate,
      Event = entry,
      Position = position,
      Queue = queue,
      UniqueId = uniqueid,
      LinkedId = linkedid
    };
  }
}
