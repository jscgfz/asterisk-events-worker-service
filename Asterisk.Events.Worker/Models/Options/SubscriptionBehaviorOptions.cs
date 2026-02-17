namespace Asterisk.Events.Worker.Models.Options;

internal sealed class SubscriptionBehaviorOptions
{
  public TimeSpan TimeSpan { get; set; }
  public int LotCount { get; set; }
}
