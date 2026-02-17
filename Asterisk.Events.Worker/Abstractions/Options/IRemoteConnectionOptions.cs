namespace Asterisk.Events.Worker.Abstractions.Options;

internal interface IRemoteConnectionOptions
{
  string Host { get; }
  int Port { get; }
  string Username { get; }
  string Password { get; }
}
