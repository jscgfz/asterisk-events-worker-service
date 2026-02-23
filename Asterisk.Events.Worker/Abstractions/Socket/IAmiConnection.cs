using Asterisk.Events.Worker.Socket.Handlers;

namespace Asterisk.Events.Worker.Abstractions.Socket;

internal interface IAmiConnection
{
  event EventRecievedHandler? OnEvent;
  Task Disconnect(CancellationToken cancellationToken = default);
  Task Start(CancellationToken cancellationToken = default);
  Task Send(byte[] buffer, CancellationToken cancellationToken = default);
}
