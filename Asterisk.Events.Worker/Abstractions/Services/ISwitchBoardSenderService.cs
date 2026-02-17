namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardSenderService
{
  Task Process(IEnumerable<Dictionary<string, string>> buffer);
}
