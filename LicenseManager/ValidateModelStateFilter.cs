//using Microsoft.AspNetCore.Mvc.Filters;
//using Microsoft.AspNetCore.Mvc;
//using static System.Net.Mime.MediaTypeNames;

//public class ValidateModelStateFilter : ActionFilterAttribute
//{
//    public override async Task OnActionExecutionAsync(
//        ActionExecutingContext context,
//        ActionExecutionDelegate next)
//    {

//        ArgumentNullException.ThrowIfNull(context);
//        ArgumentNullException.ThrowIfNull(next);

//        OnActionExecuting(context);
//        if (context.Result == null)
//        {
//            OnActionExecuted(null);
//        }

//        if (!context.ModelState.IsValid)
//        {
//            foreach (var error in context.ModelState)
//            {
//                Console.WriteLine($"Error in {error.Key}: {error.Value.Errors[0].ErrorMessage}");
//            }
//            context.Result = new BadRequestObjectResult(context.ModelState);
//        }

//    }
//}