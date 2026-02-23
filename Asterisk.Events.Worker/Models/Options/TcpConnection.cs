using System.Security.AccessControl;
using Asterisk.Events.Worker.Abstractions.Options;

namespace Asterisk.Events.Worker.Models.Options;

internal sealed class TcpConnection : IRemoteConnectionOptions
{
  public required string Host { get; set; }
  public required int Port { get; set; }
  public required string Username { get; set; }
  public required string Password { get; set; }
  public required string Events { get; set; }
  public required IEnumerable<CompanyFilter> Filters { get; set; }
  public required TimeSpan WatchDogInterval { get; set; }
  public required TimeSpan TimeoutInterval { get; set; }
  public required TimeSpan HeartBeatInterval { get; set; }
}
