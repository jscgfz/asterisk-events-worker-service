using System.Diagnostics.CodeAnalysis;
using Asterisk.Events.Worker.Models.Store;
using Asterisk.Events.Worker.Models.ViewModels;

namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardStoreService
{
  string? RemoveMember(string memberId, string queue);
  string? Add(QueueMemberStore queueMember);
  string? AddTimeline(Dictionary<string, string> channel);
  Task Publish(IEnumerable<string> companies);
  string? CloseChannel(Dictionary<string, string> channel);
  string? Entry(Dictionary<string, string> entry);
  string? DropEntry(Dictionary<string, string> entry);
}
