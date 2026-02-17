using System.Text.Json.Serialization;
using Asterisk.Events.Worker.Resolvers;

namespace Asterisk.Events.Worker.Models.Store;

internal sealed class QueueMemberStore(
  string @interface,
  string name,
  string membership,
  int callsTaken,
  DateTime lastCall,
  bool paused,
  int status,
  bool inCall,
  DateTime loginDate,
  string queue
)
{
  [JsonPropertyName("agent")] public string Interface { get; set; } = @interface;
  public long? Extension => int.TryParse(SwitchBoardResolver.PlaneInterface(Interface), out int val) ? (long)val : default;
  public string Name { get; set; } = name;
  public string Membership { get; set; } = membership;
  public int CallsTaken { get; set; } = callsTaken;
  public DateTime LastCall { get; set; } = lastCall;
  public bool Paused { get; set; } = paused;
  public int Status { get; set; } = status;
  public bool InCall { get; set; } = inCall;
  public DateTime LoginDate { get; set; } = loginDate;
  public string Queue { get; set; } = queue;
}
