using Asterisk.Events.Worker.Abstractions.Options;

namespace Asterisk.Events.Worker.Models.Options;

internal sealed class DataConnection : IRemoteConnectionOptions
{
  public required string Host { get; set; }
  public required int Port { get; set; }
  public required string Username { get; set; }
  public required string Password { get; set; }
  public required string Database { get; set; }
  public required string NitDatabase { get; set; }
}
