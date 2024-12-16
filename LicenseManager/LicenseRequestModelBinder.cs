using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class LicenseRequestModelBinder : IModelBinder
{
    private readonly ILogger<LicenseRequestModelBinder> _logger;

    public LicenseRequestModelBinder(ILogger<LicenseRequestModelBinder> logger)
    {
        _logger = logger;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        // Log when the model binding starts
        _logger.LogInformation("Starting to bind LicenseRequest model.");

        // Enable buffering to read the request body
        bindingContext.HttpContext.Request.EnableBuffering();

        // Read the request body as a string
        string requestBody;
        using (var reader = new StreamReader(bindingContext.HttpContext.Request.Body))
        {
            requestBody = await reader.ReadToEndAsync();
            _logger.LogInformation("Raw request body: {RequestBody}", requestBody);

            // Reset the request body stream position so it can be read again
            bindingContext.HttpContext.Request.Body.Position = 0;
        }

        // Try to deserialize the model
        LicenseRequest model;
        try
        {
            model = JsonSerializer.Deserialize<LicenseRequest>(requestBody);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError("Failed to deserialize request body: {ErrorMessage}", jsonEx.Message);
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        if (model == null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        bindingContext.Result = ModelBindingResult.Success(model);
    }
}
