using Asterisk.Events.Worker.Constants;

namespace Asterisk.Events.Worker.Resolvers;

internal static class SwitchBoardResolver
{
  public static string PlaneInterface(string @interface)
    => @interface.Replace(SwitchBoardConstants.InterfacePrefix, string.Empty);

  public static DateTime Date(long offset)
    => new DateTime(1970, 1, 1).AddSeconds(offset);

  public static DateTime DateFromTimeStamp(string timestamp)
  {
    double seconds = double.Parse(timestamp);
    DateTimeOffset timeOffset = DateTimeOffset.FromUnixTimeSeconds((long)seconds);
    return timeOffset.DateTime;
  }

  public static bool IsValidChannel(string channel)
    => new[] { SwitchBoardConstants.Extensionchannels, SwitchBoardConstants.Clientchannels }.Any(r => r.IsMatch(channel));
}
