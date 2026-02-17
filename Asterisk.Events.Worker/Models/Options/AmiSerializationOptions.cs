namespace Asterisk.Events.Worker.Models.Options;

internal sealed class AmiSerializationOptions
{
  public required string CommandBreak { get; set; }
  public required string LineBreak { get; set; }
  public required string PropertyBreak { get; set; }
}
