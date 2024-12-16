using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class LocalOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var remoteIpAddress = context.HttpContext.Connection.RemoteIpAddress;
        if (remoteIpAddress == null || !IsLocalRequest(remoteIpAddress))
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<LocalOnlyAttribute>>();
            logger?.LogWarning("Unauthorized access attempt to {Path} from {IP}",
                               context.HttpContext.Request.Path, remoteIpAddress);

            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Content = "This API can only be accessed locally."
            };
        }

        base.OnActionExecuting(context);
    }

    private bool IsLocalRequest(System.Net.IPAddress remoteIpAddress)
    {
        return remoteIpAddress.Equals(System.Net.IPAddress.Loopback) ||
               remoteIpAddress.Equals(System.Net.IPAddress.IPv6Loopback) ||
               remoteIpAddress.IsIPv4MappedToIPv6 && remoteIpAddress.MapToIPv4().Equals(System.Net.IPAddress.Loopback);
    }
}
