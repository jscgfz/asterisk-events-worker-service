using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Abstractions.Socket;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Socket;
using Microsoft.Extensions.Options;

namespace Asterisk.Events.Worker.Services;

internal sealed class SwitchBoardEventService(
  IOptionsMonitor<SwitchBoard> options,
  IOptionsMonitor<SubscriptionBehaviorOptions> subcriptionOptions,
  IOptionsMonitor<AmiSerializationOptions> amiOptions,
  ILogger<SwitchBoardEventService> logger,
  ILoggerFactory loggerFactory,
  ISwitchBoardSenderService sender
) : IHostedService, ISwitchBoardEventService
{
  private readonly IOptionsMonitor<SwitchBoard> _options = options;
  private readonly IOptionsMonitor<SubscriptionBehaviorOptions> _subscriptionOptions = subcriptionOptions;
  private readonly ILogger<SwitchBoardEventService> _logger = logger;
  private readonly ISwitchBoardSenderService _sender = sender;
  private readonly ILoggerFactory _loggerFactory = loggerFactory;
  private readonly IOptionsMonitor<AmiSerializationOptions> _amiOptions = amiOptions;
  private IAmiConnection _managerConnection = default!;
  private readonly Subject<Dictionary<string, string>> _filterSubject = new();
  private IDisposable? _subscription;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Initial Configuration {@Configuration}", _options.CurrentValue);
    await Configure(_options.CurrentValue.EventsConnection);
    _options.OnChange(async options =>
    {
      _logger.LogInformation("Configuration changed {@Configuration}", options);
      await Configure(options.EventsConnection);
    });
  }

  private async Task Configure(TcpConnection connection)
  {
    await Connect(connection);
  }

  private async Task Connect(TcpConnection connection)
  {
    _managerConnection = AmiConnection.New(connection, _amiOptions.CurrentValue, _loggerFactory);

    _managerConnection.OnEvent += (s, e) =>
    {
      IEnumerable<string> includedEvents = [
        "QueueMember",
        "QueueMemberStatus",
        "Status",
        "Hangup",
        "Unhold",
        "Hold",
        "AgentConnect",
        "AgentComplete",
        "QueueMemberAdded",
        "QueueMemberRemoved",
        "QueueMemberPaused",
        "Newchannel",
        "Newstate",
        "Rename",
      ];

      if (e.TryGetValue("event", out string? name) && includedEvents.Contains(name))
        _filterSubject.OnNext(e);

    };

    SubscriptionBehaviorOptions subscriptionBehaviorOptions = _subscriptionOptions.CurrentValue;

    _logger.LogInformation("subscription config -< {@config}", subscriptionBehaviorOptions);

    _subscription = _filterSubject
      .Buffer(subscriptionBehaviorOptions.TimeSpan, subscriptionBehaviorOptions.LotCount)
      .Where(buffer => buffer.Count > 0)
      .ObserveOn(TaskPoolScheduler.Default)
      .Subscribe(async d => await _sender.Process(d));

    _subscriptionOptions.OnChange(options =>
    {
      _subscription?.Dispose();
      _logger.LogInformation("subscription options changed, applying new config -> {@config}", options);
      _subscription = _filterSubject
      .Buffer(options.TimeSpan, options.LotCount)
      .Where(buffer => buffer.Count > 0)
      .ObserveOn(TaskPoolScheduler.Default)
      .Subscribe(async d => await _sender.Process(d));
    });

    await _managerConnection.Start();
  }

  public AmiCommandBuilder ActionBuilder()
    => AmiCommandBuilder.New(_amiOptions.CurrentValue);

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await _managerConnection.Disconnect(cancellationToken);
  }

  public Task SendAction(AmiCommandBuilder builder)
    => SendAction(builder.Build());

  public Task SendAction(byte[] action)
    => _managerConnection.Send(action);
}
