using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Models.Events;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Models.Store;
using Asterisk.Events.Worker.Models.ViewModels;
using Asterisk.Events.Worker.Resolvers;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Asterisk.Events.Worker.Services;

internal sealed class SwitchBoardStoreService : ISwitchBoardStoreService
{
  private readonly IOptionsMonitor<SwitchBoard> _options;
  private readonly ILogger<SwitchBoardStoreService> _logger;
  private readonly ISwitchBoardDataService _data;
  private readonly IProducer<string, string> _producer;

  private Dictionary<string, QueueViewModel> Queues;
  private readonly ConcurrentDictionary<string, QueueMemberStore> _members = new();
  private readonly ConcurrentDictionary<string, ActiveCallStore> _channels = new();

  private static readonly IEnumerable<int> _disconnectedStatus = [0, 4, 5];
  private static readonly IEnumerable<int> _availableStatus = [1, 2, 3, 6, 7, 8];

  private static readonly IEnumerable<int> _availableChannelStatus = [6];
  private static readonly IEnumerable<int> _ringingChannelStatus = [3, 4, 5];

  public SwitchBoardStoreService(
    IOptionsMonitor<SwitchBoard> options,
    ILogger<SwitchBoardStoreService> logger,
    ISwitchBoardDataService data,
    IProducer<string, string> producer
  )
  {
    _producer = producer;
    _options = options;
    _logger = logger;
    _data = data;
    Queues = ArmQueues(_options.CurrentValue.EventsConnection);
    _options.OnChange(opt =>
    {
      Queues = ArmQueues(opt.EventsConnection);
    });
  }

  private static Dictionary<string, QueueViewModel> ArmQueues(TcpConnection tcp)
    => tcp
      .Filters
      .SelectMany(f => f.Queues.Select(q => KeyValuePair.Create(q.Key, new QueueViewModel(q.Key, q.Value, f.Id, f.Name, f.Filter))))
      .ToDictionary();

  public string? Add(QueueMemberStore queueMember)
  {
    if (Queues.TryGetValue(queueMember.Queue, out QueueViewModel? queue))
    {
      string key = Key(queueMember);
      _members.AddOrUpdate(key, queueMember, (_, _) => queueMember);
      return queue.CompanyId;
    }

    return null;
  }

  public async Task Publish(IEnumerable<string> companies)
  {
    IEnumerable<KeyValuePair<string, CompanyResumeVm>> resumes = companies
      .Select(c => KeyValuePair.Create(c, Resume(c)))
      .OfType<KeyValuePair<string, CompanyResumeVm>>();

    await Task.WhenAll(
      resumes.Select(async r =>
      {
        await _producer.ProduceAsync(
          "resume",
          new Message<string, string>()
          {
            Key = r.Key,
            Value = JsonSerializer.Serialize(r.Value, JsonSerializerOptions.Web)
          }
        );
      })
      //resumes.Select(r =>
      //{
      //  if(r.Key.Equals("promotec", StringComparison.InvariantCultureIgnoreCase)) _logger.LogInformation("{company} changes detected {@changes}", r.Key, r.Value);
      //  return Task.CompletedTask;
      //})
    );
  }

  private CompanyResumeVm? Resume(string companyId)
  {
    IEnumerable<string> queues = Queues
      .Where(q => q.Value.CompanyId == companyId)
      .Select(q => q.Key);

    IEnumerable<ActiveCallStore> outboundChannels = _channels.Values
      .Where(c => c.Type == CallTypes.OutBound && c.CompanyId == companyId)
      .Where(o => !string.IsNullOrWhiteSpace(o.ExtensionChannel) && !string.IsNullOrWhiteSpace(o.UniqueId));

    return new CompanyResumeVm(
      queues
        .ToDictionary(
          k => k,
          k =>
          {
            Dictionary<string, QueueMemberStore> members = _members
              .Where(q => q.Value.Queue == k)
              .ToDictionary(m => m.Value.Interface, k => k.Value);

            IEnumerable<ActiveCallStore> inboundChannels = members.Join(
              _channels.Values,
              m => m.Key,
              c => c.Interface,
              (m, c) => c
            );

            return new InBoundResume(
              members,
              members.Where(m => _availableStatus.Contains(m.Value.Status) && !m.Value.Paused && !_channels.Any(c => c.Value.Interface == m.Key)).DistinctBy(k => k.Key).ToDictionary(),
              members.Where(m => _disconnectedStatus.Contains(m.Value.Status)).DistinctBy(k => k.Key).ToDictionary(),
              inboundChannels.Where(c => c.State.HasValue && _availableChannelStatus.Contains(c.State.Value) && (!c.Paused.HasValue || !c.Paused.Value)).DistinctBy(k => k.Interface).ToDictionary(k => k.Interface!, k => k),
              members.Where(m => _availableStatus.Contains(m.Value.Status) && m.Value.Paused || inboundChannels.Any(c => c.Interface == m.Value.Interface && c.Paused.HasValue && c.Paused.Value))
                .Select(m => KeyValuePair.Create(m.Key, m.Value.Paused ? (object?)m.Value : inboundChannels.FirstOrDefault(i => i.Interface == m.Value.Interface)))
                .DistinctBy(k => k.Key)
                .ToDictionary(),
              inboundChannels.Where(c => c.State.HasValue && _ringingChannelStatus.Contains(c.State.Value) && (!c.Paused.HasValue || !c.Paused.Value)).DistinctBy(k => k.Interface).ToDictionary(k => k.Interface!, k => k)
            );
          }
        ),
        new(
          outboundChannels.Where(c => c.State.HasValue && _availableChannelStatus.Contains(c.State.Value) && (!c.Paused.HasValue || !c.Paused.Value)).DistinctBy(k => k.Interface).ToDictionary(k => k.UniqueId!, k => k),
          outboundChannels.Where(c => c.Paused.HasValue && c.Paused.Value).DistinctBy(k => k.Interface).ToDictionary(k => k.UniqueId!, k => k),
          outboundChannels.Where(c => c.State.HasValue && _ringingChannelStatus.Contains(c.State.Value) && (!c.Paused.HasValue || !c.Paused.Value)).DistinctBy(k => k.Interface).ToDictionary(k => k.UniqueId!, k => k)
        )
    );
  }

  private static string Key(QueueMemberStore queueMember)
    => $"{queueMember.Queue}:{queueMember.Interface}";

  public string? AddTimeline(Dictionary<string, string> channel)
  {
    if (channel.TryGetValue("uniqueid", out string? uniqueid))
    {
      if (_channels.TryGetValue(uniqueid, out ActiveCallStore? call))
        call.AddChannel(channel, _data.Nit);
      else
      {
        call = ActiveCallStore.New();
        call.AddChannel(channel, _data.Nit);
        _channels.TryAdd(uniqueid, call);
      }
      if (!string.IsNullOrEmpty(call.Queue) && Queues.TryGetValue(call.Queue, out QueueViewModel? queue))
        return queue.CompanyId;
      if (Queues.Values.Any(q => q.CompanyId.Equals(call.CompanyId, StringComparison.InvariantCultureIgnoreCase)))
        return call.CompanyId;

      if (channel.TryGetValue("linkedid", out string? linkedid))
        if (uniqueid == linkedid)
          foreach (string key in _channels.Where(c => c.Value.LinkedId == linkedid).Select(c => c.Key))
            _channels[key].AddChannel(channel, _data.Nit);
        else if (
          !call.Events.Any(e => e.TryGetValue("uniqueid", out string? id) && id == linkedid) &&
          _channels.TryGetValue(linkedid, out ActiveCallStore? mainChannel)
        )
          foreach (Dictionary<string, string> mainEvent in mainChannel.Events)
            call.AddChannel(mainEvent, _data.Nit);
    }

    return null;
  }

  public string? CloseChannel(Dictionary<string, string> channel)
  {
    if (channel.TryGetValue("uniqueid", out string? uniqueid))
    {
      if (_channels.TryRemove(uniqueid, out ActiveCallStore? call))
        if (!string.IsNullOrWhiteSpace(call.Queue) && Queues.TryGetValue(call.Queue, out QueueViewModel? queue))
          return call.CompanyId;
        else if (Queues.Values.Any(q => q.CompanyId == call.CompanyId))
          return call.CompanyId;
    }
    return null;
  }
}