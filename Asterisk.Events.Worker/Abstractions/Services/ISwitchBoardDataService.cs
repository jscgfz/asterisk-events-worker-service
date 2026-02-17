namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardDataService
{
  string Name(string @interface);
  string Nit(string linkedId);
}
