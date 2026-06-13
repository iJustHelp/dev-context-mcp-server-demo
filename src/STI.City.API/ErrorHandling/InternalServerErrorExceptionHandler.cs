using Microsoft.AspNetCore.Diagnostics;

namespace STI.City.API.ErrorHandling;

public sealed class InternalServerErrorExceptionHandler(
    ILogger<InternalServerErrorExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled request failure for trace {TraceId}.",
            httpContext.TraceIdentifier);

        var problem = TypedResults.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal server error",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            });

        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
