using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace job_application_tracker_api.Uploads;

// Runs before form model binding, which would otherwise turn an oversized body into a 400.
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadRequestLimitAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > ImageUploadPolicy.MaxRequestBytes)
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            context.Result = new ObjectResult(factory.CreateProblemDetails(context.HttpContext,
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Uppladdningsbegäran får vara högst 6 MiB."))
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge
            };
            return;
        }

        await next();
    }
}
