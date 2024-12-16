using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

public class LicenseRequestModelBinderProvider : IModelBinderProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public LicenseRequestModelBinderProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata.ModelType == typeof(LicenseRequest))
        {
            var logger = _loggerFactory.CreateLogger<LicenseRequestModelBinder>();
            return new LicenseRequestModelBinder(logger);
        }
        return null;
    }
}
