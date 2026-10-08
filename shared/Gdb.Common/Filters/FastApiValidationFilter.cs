using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Gdb.Common.DTOs;
using System.Linq;

namespace Gdb.Common.Filters;

public class FastApiValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(e => e.Value!.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e => new ValidationError
                {
                    Loc = new object[] { "body", kvp.Key }, // Simplify mapping for parity
                    Msg = e.ErrorMessage,
                    Type = "value_error"
                }))
                .ToArray();

            var response = new HTTPValidationError { Detail = errors };
            
            context.Result = new ObjectResult(response)
            {
                StatusCode = 422
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
