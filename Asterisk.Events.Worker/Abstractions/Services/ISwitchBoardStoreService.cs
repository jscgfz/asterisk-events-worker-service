using System.Diagnostics.CodeAnalysis;
using Asterisk.Events.Worker.Models.Store;
using Asterisk.Events.Worker.Models.ViewModels;

namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardStoreService
{
  string? Add(QueueMemberStore queueMember);
  string? AddTimeline(Dictionary<string, string> channel);
  Task Publish(IEnumerable<string> companies);
  string? CloseChannel(Dictionary<string, string> channel);
}
