using Asterisk.Events.Worker.Extensions;

IHost host = Host
  .CreateApplicationBuilder(args)
  .WithPhoneConfiguration()
  .WithCustomLogging()
  .WithKafkaBroker()
  .WithSwitchBoarStoreConfiguration()
  .WithSwitchBoardDataConfiguration()
  .WithSwitchBoardEventsConfiguration()
  .WithSwitchBoardSenderConfiguration()
  .WithSwitchBoarCommandConfiguration()
  .Build();

host.Run();
