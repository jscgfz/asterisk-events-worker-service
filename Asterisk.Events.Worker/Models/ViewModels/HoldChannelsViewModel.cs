namespace Asterisk.Events.Worker.Models.ViewModels;

internal sealed record HoldChannelsViewModel(
  string ClientChannel,
  string ExtensionChannel
);
