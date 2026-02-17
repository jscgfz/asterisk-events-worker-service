using System.Text.Json.Serialization;
using Asterisk.Events.Worker.Models.Store;

namespace Asterisk.Events.Worker.Models.Events;

internal sealed record OutBoundResume(
  [property: JsonPropertyName("in_call")] Dictionary<string, ActiveCallStore> InCall,
  Dictionary<string, ActiveCallStore> Paused,
  Dictionary<string, ActiveCallStore> Ringing
);
