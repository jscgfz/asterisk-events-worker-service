namespace Asterisk.Events.Worker.Models.Options;

internal sealed class CompanyFilter
{
  public required string Id { get; set; }
  public required string Name { get; set; }
  public required string Filter { get; set; }
  public required Dictionary<string, string> Queues { get; set; }
}
