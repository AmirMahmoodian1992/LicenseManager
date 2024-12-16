using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        // Check if request has a body
        if (context.Request.ContentLength == null || context.Request.ContentLength == 0)
        {
            _logger.LogWarning("Request body is empty.");
            await _next(context);
            return;
        }

        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogWarning("Request body is empty or whitespace.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Request body cannot be empty."
                });
                return;
            }

            _logger.LogInformation("Request Body: {Body}", body);

            try
            {
                var licenseRequest = JsonSerializer.Deserialize<LicenseRequest>(body);
                context.Items["LicenseRequest"] = licenseRequest;
            }
            catch (JsonException ex)
            {
                _logger.LogError("Deserialization error: {Error}", ex.Message);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Invalid request body format.",
                    error = ex.Message
                });
                return;
            }

            context.Request.Body.Position = 0;
        }

        await _next(context);
    }

}
