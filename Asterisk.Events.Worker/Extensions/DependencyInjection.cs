using Asterisk.Events.Worker.Abstractions.Services;
using Asterisk.Events.Worker.Models.Options;
using Asterisk.Events.Worker.Services;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Serilog;

namespace Asterisk.Events.Worker.Extensions;

internal static class DependencyInjection
{
  public static HostApplicationBuilder WithSwitchBoarCommandConfiguration(this HostApplicationBuilder builder)
  {
    builder.Services.AddSingleton<SwitchBoardCommandService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SwitchBoardCommandService>());
    return builder;
  }
  public static HostApplicationBuilder WithSwitchBoarStoreConfiguration(this HostApplicationBuilder builder)
  {
    builder.Services.AddSingleton<ISwitchBoardStoreService, SwitchBoardStoreService>();
    return builder;
  }
  public static HostApplicationBuilder WithSwitchBoardEventsConfiguration(this HostApplicationBuilder builder)
  {
    builder.Services.AddSingleton<SwitchBoardEventService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SwitchBoardEventService>());
    builder.Services.AddSingleton<ISwitchBoardEventService>(sp => sp.GetRequiredService<SwitchBoardEventService>());
    return builder;
  }

  public static HostApplicationBuilder WithSwitchBoardDataConfiguration(this HostApplicationBuilder builder)
  {
    builder.Services.AddSingleton<SwitchBoardDataService>();
    builder.Services.AddSingleton<ISwitchBoardDataService>(sp => sp.GetRequiredService<SwitchBoardDataService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SwitchBoardDataService>());
    return builder;
  }

  public static HostApplicationBuilder WithSwitchBoardSenderConfiguration(this HostApplicationBuilder builder)
  {
    builder.Services.AddSingleton<ISwitchBoardSenderService, SwitchBoardSenderService>();
    return builder;
  }

  public static HostApplicationBuilder WithKafkaBroker(this HostApplicationBuilder builder)
  {
    builder.Services
      .AddOptions<BrokerOptions>()
      .Bind(builder.Configuration.GetSection(nameof(BrokerOptions)))
      .ValidateOnStart();

    builder.Services.AddSingleton(sp =>
    {
      ProducerConfig producerConfig = sp.GetRequiredService<IOptionsMonitor<BrokerOptions>>()
        .CurrentValue
        .ProducerConfig;

      return new ProducerBuilder<string, string>(producerConfig)
        .Build();
    });

    builder.Services.AddSingleton(sp =>
    {
      ConsumerConfig consumerConfig = sp.GetRequiredService<IOptionsMonitor<BrokerOptions>>()
      .CurrentValue
      .ConsumerConfig;

      return new ConsumerBuilder<string, string>(consumerConfig)
        .Build();
    });

    return builder;
  }

  public static HostApplicationBuilder WithPhoneConfiguration(this HostApplicationBuilder builder)
  {
    builder
      .Configuration
      .AddJsonFile(path: "appsettings.json", optional: true, reloadOnChange: true)
      .AddJsonFile(path: $"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
      .AddJsonFile(path: "appsettings.Phone.json", optional: true, reloadOnChange: true)
      .AddEnvironmentVariables();

    builder.Services.AddOptions<SwitchBoard>()
      .Bind(builder.Configuration.GetSection(nameof(SwitchBoard)))
      .ValidateOnStart();

    builder.Services.AddOptions<SubscriptionBehaviorOptions>()
      .Bind(builder.Configuration.GetSection(nameof(SubscriptionBehaviorOptions)))
      .ValidateOnStart();

    builder.Services.AddOptions<AmiSerializationOptions>()
      .Bind(builder.Configuration.GetSection(nameof(AmiSerializationOptions)))
      .ValidateOnStart();

    return builder;
  }

  public static HostApplicationBuilder WithHostedService<TService>(this HostApplicationBuilder builder)
      where TService : class, IHostedService
  {
    builder.Services.AddHostedService<TService>();
    return builder;
  }

  public static HostApplicationBuilder WithCustomLogging(this HostApplicationBuilder builder)
  {
    builder.Services.AddSerilog((sp, logging) =>
    {
      logging.ReadFrom.Configuration(sp.GetRequiredService<IConfiguration>());
    });
    return builder;
  }

}
