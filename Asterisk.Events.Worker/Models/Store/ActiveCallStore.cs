using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Asterisk.Events.Worker.Constants;
using Asterisk.Events.Worker.Models.ViewModels;
using Asterisk.Events.Worker.Resolvers;

namespace Asterisk.Events.Worker.Models.Store;

internal sealed class ActiveCallStore
{
  public static ActiveCallStore New(ILogger logger) => new(logger);
  private ActiveCallStore(ILogger logger) => _logger = logger;

  private readonly ILogger _logger;

  internal static readonly IEnumerable<string> inboundContexts = ["trunkinbound", "colas", "verMiem"];

  private readonly ConcurrentQueue<Dictionary<string, string>> Timeline = [];

  public string? PhoneNumber { get; private set; }
  public string? CompanyId { get; private set; }
  public string? ClientChannel { get; private set; }
  public string? ExtensionChannel { get; private set; }
  public string? LinkedId { get; private set; }
  public string? UniqueId { get; private set; }
  public int? State { get; private set; }
  public string? Interface => ExtensionChannel?.Split('-').FirstOrDefault();
  public IEnumerable<Dictionary<string, string>> Events => Timeline
    .OrderBy(t => double.Parse(t.GetValueOrDefault("timestamp") ?? "0"));
  public string? Queue { get; private set; }
  public string? Nit { get; private set; }
  public CallTypes? Type { get; private set; }
  public bool? Paused { get; private set; }
  [JsonIgnore] public HoldTypes? HoldType { get; private set; }
  public DateTime? QueueEntryDate { get; private set; }
  public DateTime? AttendedDate { get; private set; }


  public void AddChannel(Dictionary<string, string> obj, Func<string, EntryCallViewModel> nitFunc)
  {
    Dictionary<string, string> timeline = new(obj);
    if (timeline.TryGetValue("event", out string? eventName))
    {
      lock (Timeline)
      {
        Timeline.Enqueue(timeline);
      }

      switch (eventName)
      {
        case "Hold":
          if(
            timeline.TryGetValue("channelstate", out string? holdcChannelstate) &&
            holdcChannelstate == "6"
          )
          {
            Paused = true;
            HoldType = HoldTypes.Client;
          }
          break;
        case "Unhold":
          Paused = false;
          HoldType = HoldTypes.Client;
          break;
        case "custom-QueueCallerJoin":
          if (timeline.TryGetValue("queue", out string? queue))
            Queue = queue;
          break;
        default:
          if (
            timeline.TryGetValue("channel", out string? channel) &&
            SwitchBoardResolver.IsValidChannel(channel) &&
            timeline.TryGetValue("uniqueid", out string? uniqueid) &&
            timeline.TryGetValue("linkedid", out string? linkedid) &&
            timeline.TryGetValue("channelstate", out string? channelstate) &&
            !channelstate.Equals("0")
          )
          {
            State = int.TryParse(channelstate, out int numberstate) ? numberstate : State;
            LinkedId ??= linkedid;
            UniqueId ??= uniqueid;

            if (SwitchBoardConstants.Clientchannels.IsMatch(channel))
              ClientChannel = channel;

            if (uniqueid.Equals(linkedid))
            {
              if (
                timeline.TryGetValue("context", out string? context)
              )
              {
                bool inbound = inboundContexts.Any(t => t.Equals(context)) || context.StartsWith("ivr", StringComparison.InvariantCultureIgnoreCase);
                Type ??= inbound ? CallTypes.Inbound : CallTypes.OutBound;
                Queue ??= timeline.TryGetValue("application", out string? application) &&
                  application.Equals("queue", StringComparison.InvariantCultureIgnoreCase) ?
                  timeline.GetValueOrDefault("data")?.Split(',').FirstOrDefault() : Queue;
                Queue ??= timeline.GetValueOrDefault("queue");

                if(timeline.TryGetValue(inbound ? "calleridnum" : "exten", out string? phone))
                  PhoneNumber = phone.Contains('*') ? phone.Split('*').First() : phone;
                ExtensionChannel ??= !inbound && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? channel : ExtensionChannel;
                if(Nit == null || CompanyId == null || PhoneNumber == null)
                {
                  EntryCallViewModel pairs = nitFunc.Invoke(linkedid);
                  Nit ??= pairs.Nit;
                  CompanyId ??= pairs.CompanyId;
                  PhoneNumber ??= pairs.PhoneNumber;
                }
              }
            }
            else
            {
              ExtensionChannel = !Type.HasValue || Type.Equals(CallTypes.Inbound) && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? channel : ExtensionChannel;
              if (Nit == null || CompanyId == null)
              {
                EntryCallViewModel pairs = nitFunc.Invoke(linkedid);
                Nit ??= pairs.Nit;
                CompanyId ??= pairs.CompanyId;
                PhoneNumber ??= pairs.PhoneNumber;
              }
            }

            if(channelstate == "6")
            {
              //_logger.LogWarning("State captured for {linkedId}, {interface}", linkedid, Interface);
              if(timeline.TryGetValue("timestamp", out string? timestamp)) AttendedDate ??= SwitchBoardResolver.DateFromTimeStamp(timestamp);
            }

            if (!string.IsNullOrWhiteSpace(CompanyId) && CompanyId.Equals("unknown", StringComparison.InvariantCultureIgnoreCase) && timeline.TryGetValue("accountcode", out string? accountcode))
              CompanyId = accountcode;
          }
          break;
      }
    }
  }

  public void SetServerPause(bool paused)
  {
    Paused = paused;
    HoldType = HoldTypes.Server;
  }
}

internal enum CallTypes
{
  Inbound,
  OutBound
}

internal enum HoldTypes
{
  Client,
  Server
}