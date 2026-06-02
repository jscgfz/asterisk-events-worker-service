namespace Asterisk.Events.Worker.Models.ViewModels;

internal sealed record EntryCallViewModel(
  string Nit,
  string CompanyId,
  string PhoneNumber
);
