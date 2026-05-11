namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardDataService
{
  string Name(string @interface);
  KeyValuePair<string, string> Nit(string linkedId);
}
