using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Socket.Handlers;
using WSocket = System.Net.Sockets.Socket;

namespace Asterisk.Events.Worker.Socket;

internal sealed class AmiConnection(
  TcpConnection connection,
  AmiSerializationOptions options,
  ILogger<AmiConnection> logger
)
{
  private readonly TcpConnection _connection = connection;
  private readonly ILogger<AmiConnection> _logger = logger;
  private readonly AmiSerializationOptions _options = options;
  private readonly WSocket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
  private bool Logged = true;
  private string Data = string.Empty;
  private Task? RecievedTask;
  private Task? PingTask;

  public event EventRecievedHandler? OnEvent;

  public async Task Connect(CancellationToken cancellationToken = default)
  {
    IPEndPoint enpoint = new(IPAddress.Parse(_connection.Host), _connection.Port);
    await _socket.ConnectAsync(enpoint, cancellationToken);
    _logger.LogInformation("socket connected");
  }

  public async Task Disconnect(CancellationToken cancellationToken = default)
  {
    Logged = false;
    RecievedTask?.Dispose();
    PingTask?.Dispose();
    await _socket.DisconnectAsync(true, cancellationToken);
  }
  public async Task Start(CancellationToken cancellationToken = default)
  {
    RecievedTask = ContinuousRecieve(cancellationToken);
    byte[] login = AmiCommandBuilder.New(_options)
      .WithActionName("Login")
      .WithArgument("Username", _connection.Username)
      .WithArgument("Secret", _connection.Password)
      .WithArgument("Events", "call,agent")
      .Build();
    await _socket.SendAsync(login, SocketFlags.None, cancellationToken);
    _logger.LogInformation("login sended");
    PingTask = Ping(cancellationToken);
  }

  public async Task Send(byte[] buffer, CancellationToken cancellationToken = default)
  {
    await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
  }

  private async Task Ping(CancellationToken cancellationToken)
  {
    byte[] ping = AmiCommandBuilder.New(_options)
      .WithActionName("Ping")
      .Build();

    while (Logged)
    {
      await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
      await _socket.SendAsync(ping, SocketFlags.None, cancellationToken);
    }
  }

  public async Task ContinuousRecieve(CancellationToken cancellationToken)
  {
    byte[] buffer = new byte[1024];
    while (Logged)
    {
      int len = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None, cancellationToken);
      if (len > 0)
      {
        Data += Encoding.ASCII.GetString(buffer, 0, len);
        IEnumerable<string> parts = Data.Split(_options.CommandBreak);
        Data = parts.Last();
        parts = parts.SkipLast(1);
        IEnumerable<Dictionary<string, string>> events = parts.Select(ParseEvent);

        foreach (Dictionary<string, string> e in events)
          OnEvent?.Invoke(this, e);
      }
    }
  }

  private Dictionary<string, string> ParseEvent(string plainEvent)
  {
    IEnumerable<string> lines = plainEvent.Split(_options.LineBreak).Where(l => l.Contains(_options.PropertyBreak));
    IEnumerable<KeyValuePair<string, string>> parts = lines.Select(
      line =>
      {
        IEnumerable<string> props = line.Split(_options.PropertyBreak);
        return KeyValuePair.Create(props.ElementAt(0).ToLower(), props.ElementAt(1));
      }
    );

    return parts.ToDictionary();
  }
}
