using System.Collections.Concurrent;
using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Models.Options;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace Asterisk.Events.Worker.Services;

internal sealed class SwitchBoardDataService(
  IOptionsMonitor<SwitchBoard> options,
  ILogger<SwitchBoardSenderService> logger
) : BackgroundService, ISwitchBoardDataService
{
  private readonly IOptionsMonitor<SwitchBoard> _options = options;
  private readonly ILogger<SwitchBoardSenderService> _logger = logger;
  private readonly ConcurrentDictionary<string, string> _names = new();
  private MySqlConnection Connection => new(
    string.Join(';',
      $"server={_options.CurrentValue.DataConnection.Host}",
      $"port={_options.CurrentValue.DataConnection.Port}",
      $"uid={_options.CurrentValue.DataConnection.Username}",
      $"pwd={_options.CurrentValue.DataConnection.Password}",
      $"database={_options.CurrentValue.DataConnection.Database}"
    )
  );

  private MySqlConnection NitConnection => new(
    string.Join(';',
      $"server={_options.CurrentValue.DataConnection.Host}",
      $"port={_options.CurrentValue.DataConnection.Port}",
      $"uid={_options.CurrentValue.DataConnection.Username}",
      $"pwd={_options.CurrentValue.DataConnection.Password}",
      $"database={_options.CurrentValue.DataConnection.NitDatabase}"
    )
  );

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        MySqlConnection connection = Connection;
        await connection.OpenAsync(stoppingToken);

        MySqlCommand command = connection.CreateCommand();
        string query = "select ph.extension, ph.fullname from phones as ph";

        if (!_names.IsEmpty)
        {
          IEnumerable<KeyValuePair<string, string>> pairs = _names.Select((n, i) => KeyValuePair.Create($"@ext{i}", n.Key));
          command.CommandText = $"{query} where ph.extension not in ({string.Join(", ", pairs.Select(p => p.Key))})";
          foreach (KeyValuePair<string, string> pair in pairs)
            command.Parameters.AddWithValue(pair.Key, pair.Value);
        }
        else
          command.CommandText = query;

        using MySqlDataReader reader = command.ExecuteReader();
        while (await reader.ReadAsync(stoppingToken))
        {
          string id = reader.GetString("extension");
          string name = reader.GetString("fullname");
          _names.AddOrUpdate(id, name, (_, _) => name);
        }

        await connection.CloseAsync();
      }
      catch (MySqlException ex)
      {
        _logger.LogError(ex, "Error retrieving names");
      }

      await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
  }

  public string Name(string @interface)
  {
    if (_names.TryGetValue(@interface, out string? name)) return name;
    try
    {
      MySqlConnection connection = Connection;
      connection.Open();

      MySqlCommand command = connection.CreateCommand();
      command.CommandText = "select ph.extension, ph.fullname from phones as ph where ph.extension = @ext limit 1";
      command.Parameters.AddWithValue("@ext", @interface);
      using MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
        string id = reader.GetString("extension");
        name = reader.GetString("fullname");
        _names.AddOrUpdate(id, name, (_, _) => name);
      }
      connection.Close();
    }
    catch (MySqlException ex)
    {
      _logger.LogError(ex, "Error retrieving names");
    }

    return name ?? "unknown";
  }

  public string Nit(string linkkedId)
  {
    string? nit = null;
    try
    {
      MySqlConnection connection = NitConnection;
      connection.Open();

      MySqlCommand command = connection.CreateCommand();
      command.CommandText = "select ci.cedula from cedulaIVR as ci where ci.linkedid = @id limit 1";
      command.Parameters.AddWithValue("@id", linkkedId);
      using MySqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
        nit = reader.GetString("cedula");
      connection.Close();
    }
    catch (MySqlException ex)
    {
      _logger.LogError(ex, "Error retrieving names");
    }

    return nit ?? "unknown";
  }
}
