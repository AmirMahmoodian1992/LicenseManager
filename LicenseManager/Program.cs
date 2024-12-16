using LicenseManager;
using Microsoft.AspNetCore.Hosting;
using Serilog;

var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Barsa", "LicenseManager");

if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddConfiguration(configuration);
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<WorkerService>();
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.ConfigureKestrel(serverOptions =>
        {
            var url = configuration.GetValue<string>("Kestrel:Endpoints:Http:Url") /*?? "http://localhost:8080"*/;
            var uri = new Uri(url);
            var port = uri.Port;

            serverOptions.ListenAnyIP(port);
        });

        webBuilder.UseStartup<Startup>();
    })
   .UseSerilog((context, services, configuration) =>
   {
       var logEnvironment = context.Configuration.GetValue<string>("log");

       if (string.IsNullOrEmpty(logEnvironment))
       {
           logEnvironment = "Production";
       }

       if (logEnvironment == "Dev")
       {
           configuration.MinimumLevel.Debug()
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .WriteTo.File(Path.Combine(logDirectory, "logs", "development-log-.txt"), rollingInterval: RollingInterval.Day);
       }
       else if (logEnvironment == "Production")
       {
           configuration.MinimumLevel.Information()
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .WriteTo.File(Path.Combine(logDirectory, "logs", "production-log-.txt"), rollingInterval: RollingInterval.Day);
       }
       else
       {
           configuration.MinimumLevel.Fatal()
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .WriteTo.File(Path.Combine(logDirectory, "logs", "default-log-.txt"), rollingInterval: RollingInterval.Day);
       }
   });

await builder.Build().RunAsync();
