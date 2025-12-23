using Argon.Service;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Argon Service";
});
builder.Logging.AddEventLog(config =>
    {
        config.SourceName = "Argon";
    });
LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();