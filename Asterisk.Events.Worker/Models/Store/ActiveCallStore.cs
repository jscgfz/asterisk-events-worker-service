using System.Text.Json.Serialization;
using Asterisk.Events.Worker.Constants;
using Asterisk.Events.Worker.Resolvers;

namespace Asterisk.Events.Worker.Models.Store;

internal sealed class ActiveCallStore
{
  public static ActiveCallStore New() => new();
  private ActiveCallStore() { }

  internal static readonly IEnumerable<string> inboundContexts = ["trunkinbound", "colas", "verMiem"];

  private IEnumerable<Dictionary<string, string>> Timeline = [];

  public string? PhoneNumber { get; private set; }
  public string? CompanyId { get; private set; }
  public string? ClientChannel { get; private set; }
  public string? ExtensionChannel { get; private set; }
  public string? LinkedId { get; private set; }
  public string? UniqueId { get; private set; }
  public int? State { get; private set; }
  public string? Interface => ExtensionChannel?.Split('-').FirstOrDefault();
  public IEnumerable<Dictionary<string, string>> Events => Timeline;
  public string? Queue { get; private set; }
  public string? Nit { get; private set; }
  public CallTypes? Type { get; private set; }
  public bool? Paused { get; private set; }
  [JsonIgnore] public HoldTypes? HoldType { get; private set; }
  public DateTime? QueueEntryDate { get; private set; }
  public DateTime? AttendedDate { get; private set; }


  public void AddChannel(Dictionary<string, string> timeline, Func<string, string> nitFunc)
  {
    if (timeline.TryGetValue("event", out string? eventName))
    {
      Timeline = Timeline
              .Append(timeline)
              .OrderBy(t => double.Parse(t.GetValueOrDefault("timestamp") ?? "0"));

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
                  PhoneNumber ??= phone.Contains('*') ? phone.Split('*').First() : phone;
                ExtensionChannel ??= !inbound && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? channel : ExtensionChannel;
                CompanyId ??= !inbound && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? timeline.GetValueOrDefault("accountcode") : default;
                Nit ??= nitFunc.Invoke(linkedid);
              }
            }
            else
            {
              ExtensionChannel = !Type.HasValue || Type.Equals(CallTypes.Inbound) && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? channel : ExtensionChannel;
              CompanyId ??= !Type.HasValue || Type.Equals(CallTypes.Inbound) && SwitchBoardConstants.Extensionchannels.IsMatch(channel) ? timeline.GetValueOrDefault("accountcode") : default;
              Nit ??= nitFunc.Invoke(linkedid);
            }
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