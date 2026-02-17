using Asterisk.Events.Worker.Socket;

namespace Asterisk.Events.Worker.Abstractions.Services;

internal interface ISwitchBoardEventService
{
  Task SendAction(AmiCommandBuilder builder);
  Task SendAction(byte[] action);
  AmiCommandBuilder ActionBuilder();
}
