using System.Text;
using Asterisk.Events.Worker.Models.Options;

namespace Asterisk.Events.Worker.Socket;

internal sealed class AmiCommandBuilder
{
  private readonly AmiSerializationOptions _options;
  private readonly Dictionary<string, string> _props;

  private AmiCommandBuilder(
    AmiSerializationOptions options
  ) => (_options, _props) = (options, []);

  public static AmiCommandBuilder New(AmiSerializationOptions options)
    => new(options);

  public AmiCommandBuilder WithActionName(string name)
  {
    _props["Action"] = name;
    return this;
  }

  public AmiCommandBuilder WithArgument(string argName, string argValue)
  {
    _props[argName] = argValue;
    return this;
  }

  public AmiCommandBuilder WithArguments(params IEnumerable<KeyValuePair<string, string>> parameters)
  {
    foreach (KeyValuePair<string, string> parameter in parameters)
      _props[parameter.Key] = parameter.Value;
    return this;
  }

  public byte[] Build()
  {
    IEnumerable<string> lines = _props.Select(p => $"{p.Key}{_options.PropertyBreak}{p.Value}");
    string block = string.Join(_options.LineBreak, lines);
    string command = $"{block}{_options.CommandBreak}";
    return Encoding.ASCII.GetBytes(command);
  }
}
