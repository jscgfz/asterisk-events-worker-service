namespace Asterisk.Events.Worker.Socket.Handlers;

internal delegate void EventRecievedHandler(object sender, Dictionary<string, string> @event);
