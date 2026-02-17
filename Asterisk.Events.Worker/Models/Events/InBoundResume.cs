using System.Text.Json.Serialization;
using Asterisk.Events.Worker.Models.Store;

namespace Asterisk.Events.Worker.Models.Events;

internal sealed record InBoundResume(
  Dictionary<string, QueueMemberStore> Members,
  Dictionary<string, QueueMemberStore> Available,
  Dictionary<string, QueueMemberStore> Disconected,
  [property: JsonPropertyName("in_call")] Dictionary<string, ActiveCallStore> InCall,
  Dictionary<string, object?> Paused,
  Dictionary<string, ActiveCallStore> Ringing
);
