namespace Asterisk.Events.Worker.Models.ViewModels;

internal sealed record QueueViewModel(
  string Id,
  string Name,
  string CompanyId,
  string CompanyName,
  string CompanyFilter
);
