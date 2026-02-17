namespace Asterisk.Events.Worker.Models.Options;

internal sealed class SwitchBoard
{
  public required TcpConnection EventsConnection { get; set; }
  public required DataConnection DataConnection { get; set; }
}
