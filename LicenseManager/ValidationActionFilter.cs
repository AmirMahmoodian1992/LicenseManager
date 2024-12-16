//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;
//using Microsoft.Extensions.Logging;
//using System.Linq;

//public class ValidationActionFilter : IActionFilter
//{
//    private readonly ILogger<ValidationActionFilter> _logger;

//    public ValidationActionFilter(ILogger<ValidationActionFilter> logger)
//    {
//        _logger = logger;
//    }

//    public void OnActionExecuting(ActionExecutingContext context)
//    {
//        if (!context.ModelState.IsValid)
//        {
//            var validationErrors = context.ModelState.Values
//                                    .SelectMany(v => v.Errors)
//                                    .Select(e => e.ErrorMessage)
//                                    .ToList();

//            _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationErrors));

//            context.Result = new BadRequestObjectResult(new
//            {
//                message = "Validation failed",
//                errors = validationErrors
//            });
//        }
//    }

//    public void OnActionExecuted(ActionExecutedContext context) { }
//}
