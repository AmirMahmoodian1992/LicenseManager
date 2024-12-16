using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using LicenseManager;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace LicenseManager
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            //  services.AddScoped<ValidationActionFilter>();
           //services.AddScoped<LicenseRequestModelBinder>();

            services.AddControllers(options =>
            {
                // options.Filters.Add<ValidationActionFilter>(order: int.MinValue);
                //options.ModelBinderProviders.Insert(0, new LicenseRequestModelBinder();
            });

            services.AddEndpointsApiExplorer();
            services.AddLogging();

            services.AddSingleton<ActivationState>();
            services.AddSingleton<ActivationManager>();
            //services.AddSwaggerGen();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                logger.LogWarning("The service is not running with administrative privileges.");
            }
            

            //app.UseMiddleware<RequestLoggingMiddleware>();

            //app.UseSwagger();
            //app.UseSwaggerUI(c =>
            //{
            //    c.SwaggerEndpoint("/swagger/v1/swagger.json", "License API V1");
            //});

            if (env.IsDevelopment())
            {
                logger.LogInformation("In Development environment");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                logger.LogInformation("In Production environment");
            }

            logger.LogInformation("Application is starting up");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // Root URL
                endpoints.MapGet("/", async context =>
                {
                    // Fetch version from appsettings.json
                    var appVersion = _configuration["AppVersion"];

                    // Alternatively, fetch version from assembly
                    if (string.IsNullOrWhiteSpace(appVersion))
                    {
                        appVersion = Assembly.GetExecutingAssembly()
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? "1.0.0"; // Default version
                    }

                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync($@"
                        <!DOCTYPE html>
                        <html lang='en'>
                        <head>
                            <meta charset='UTF-8'>
                            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                            <title>App Version</title>
                        </head>
                        <body>
                            <h2>Barsa License Manager APIs</h2>
                            <p>App Version: <strong>{appVersion}</strong></p>
                        </body>
                        </html>
                    ");
                });
            });

        }
    }
}