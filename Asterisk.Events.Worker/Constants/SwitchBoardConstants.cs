using System.Text.RegularExpressions;

namespace Asterisk.Events.Worker.Constants;

internal static class SwitchBoardConstants
{
  public const string InterfacePrefix = "SIP/";
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
  public static Regex Extensionchannels = new(@"^(SIP\/\d+-[a-z0-9]+)$");
  public static Regex Clientchannels = new(@"^(SIP\/troncal-panasonic-[a-z0-9]+)$");
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
}
