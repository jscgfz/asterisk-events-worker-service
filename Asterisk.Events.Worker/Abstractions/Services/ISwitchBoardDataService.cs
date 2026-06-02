using Asterisk.Events.Worker.Models.ViewModels;

namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardDataService
{
  string Name(string @interface);
  EntryCallViewModel Nit(string linkedId);
}
