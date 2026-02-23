using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using Asterisk.Events.Worker.Abstractions.Socket;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Socket.Handlers;
using WSocket = System.Net.Sockets.Socket;

namespace Asterisk.Events.Worker.Socket;

internal sealed class AmiConnection : IAmiConnection
{
  private readonly TcpConnection _params;
  private readonly AmiSerializationOptions _options;
  private readonly ILogger<AmiConnection> _logger;

  private WSocket? _socket;
  private CancellationTokenSource? _connectionCts;
  private Task? _wdTask;
  private Task? _hbTask;
  private long LastRecievedTicks = DateTime.UtcNow.Ticks;
  private string Data = string.Empty;

  public event EventRecievedHandler? OnEvent;

  private DateTime LastRecievedDate
  {
    get => new(Interlocked.Read(ref LastRecievedTicks), DateTimeKind.Utc);
    set => Interlocked.Exchange(ref LastRecievedTicks, value.Ticks);
  }

  public static AmiConnection New(
    TcpConnection @params,
    AmiSerializationOptions options,
    ILoggerFactory loggerFactory
  ) => new AmiConnection(@params, options, loggerFactory);

  private AmiConnection(
    TcpConnection @params,
    AmiSerializationOptions options,
    ILoggerFactory loggerFactory
  ) => (
    _params,
    _options,
    _logger
  ) = (
    @params,
    options,
    loggerFactory.CreateLogger<AmiConnection>()
  );

  private async Task InitializeConnection(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Staring connection {host}:{port}", _params.Host, _params.Port);
    _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
    KeepAlive(ref _socket, 30000, 5000);
    IPEndPoint endpoint = new(IPAddress.Parse(_params.Host), _params.Port);
    await _socket.ConnectAsync(endpoint, cancellationToken);
    _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    LastRecievedDate = DateTime.UtcNow;
    await LoginAsync(_connectionCts.Token);
    _hbTask = Task.Run(() => HeartBeat(_connectionCts.Token), _connectionCts.Token);
    _wdTask = Task.Run(() => WatchDog(_connectionCts.Token), _connectionCts.Token);
    _logger.LogInformation("AMI Connection stablished");
  }

  private async Task Snapshot(CancellationToken cancellationToken)
  {
    byte[] queueStatus = AmiCommandBuilder.New(_options)
     .WithActionName("QueueStatus")
     .Build();
    byte[] channels = AmiCommandBuilder.New(_options)
      .WithActionName("Status")
      .Build();

    await Send(queueStatus, cancellationToken);
    await Send(channels, cancellationToken);
  }

  private async Task InitializeListenAsync(CancellationToken cancellationToken)
  {
    byte[] buffer = new byte[4096];
    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
      cancellationToken,
      _connectionCts?.Token ?? CancellationToken.None
    );

    CancellationToken token = linkedCts.Token;

    while (!token.IsCancellationRequested)
    {
      if (_socket != null)
      {
        Task<int> recieveTask = Task.Run(async () => await _socket.ReceiveAsync(
          new ArraySegment<byte>(buffer),
          SocketFlags.None,
          token
        ), token);
        Task timeoutTask = Task.Delay(_params.TimeoutInterval, token);
        Task resultTaks = await Task.WhenAny(recieveTask, timeoutTask);

        if (resultTaks == timeoutTask)
        {
          _logger.LogWarning("AMI listen Timeout. Forcing reconnect");
          break;
        }

        int len = await recieveTask;
        if (len == 0)
        {
          _logger.LogWarning("AMI socker closed by remote. forcing reconnect");
          break;
        }
        LastRecievedDate = DateTime.UtcNow;

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

  private void CleanUp()
  {
    _connectionCts?.Cancel();
    if (_socket != null)
    {
      _socket.Shutdown(SocketShutdown.Both);
      _socket.Close();
      _socket.Dispose();
      _socket = null;
    }
    _logger.LogInformation("Ami connection cleaned");
  }

  private async Task LoginAsync(CancellationToken cancellationToken)
  {
    if (_socket != null)
    {
      byte[] loginCmd = AmiCommandBuilder.New(_options)
        .WithActionName("Login")
        .WithArguments(
          KeyValuePair.Create("Username", _params.Username),
          KeyValuePair.Create("Secret", _params.Password),
          KeyValuePair.Create("Event", _params.Events)
        )
        .Build();

      await _socket.SendAsync(loginCmd, SocketFlags.None, cancellationToken);
    }
  }

  private static void KeepAlive(ref WSocket socket, int keepAliveTime, int keepAliveInterval)
  {
    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTime / 1000);
    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveInterval / 1000);
    socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
  }

  private async Task HeartBeat(CancellationToken cancellationToken)
  {
    byte[] pingCmd = AmiCommandBuilder.New(_options)
      .WithActionName("Ping")
      .Build();

    _logger.LogInformation("AMI ping heartbeat starting");

    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        if (_socket != null)
        {
          await Task.Delay(_params.HeartBeatInterval, cancellationToken);
          await _socket.SendAsync(pingCmd, SocketFlags.None, cancellationToken);
        }
      }
    }
    catch(TaskCanceledException)
    {
      _logger.LogInformation("Ami Pinging task canceled");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ami Pinging error");
    }
  }

  private async Task WatchDog(CancellationToken cancellationToken)
  {
    _logger.LogInformation("AMI Watchdog started");
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        TimeSpan inactivityInterval = DateTime.UtcNow - LastRecievedDate;
        if (inactivityInterval > _params.WatchDogInterval)
        {
          _logger.LogWarning("AMI inactivity elapsed time ({time}). Forcing Reconnect", inactivityInterval);
          _connectionCts?.Cancel();
        }
      }
    }
    catch (TaskCanceledException)
    {
      _logger.LogInformation("Ami watchdog task canceled");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "AMI watchdog error");
      _connectionCts?.Cancel();
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

    return parts.DistinctBy(p => p.Key).ToDictionary();
  }

  private async Task Connect(CancellationToken cancellationToken = default)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await InitializeConnection(cancellationToken);
        await Snapshot(cancellationToken);
        await InitializeListenAsync(cancellationToken);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Ami Connect error");
      }
      CleanUp();
      await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
  }

  public Task Disconnect(CancellationToken cancellationToken = default)
  {
    CleanUp();
    _connectionCts?.Cancel();
    return Task.CompletedTask;
  }

  public Task Start(CancellationToken cancellationToken = default)
  {
    _ = Connect(cancellationToken);
    return Task.CompletedTask;
  }

  public async Task Send(byte[] buffer, CancellationToken cancellationToken = default)
  {
    if (_socket != null)
      await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken);
  }
}