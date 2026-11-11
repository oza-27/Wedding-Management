using System.Text.Json;

namespace WeddingSaaS.API.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;
            logger.LogError("Unhandled API error. TraceId={TraceId}, ExceptionType={ExceptionType}", traceId, exception.GetType().Name);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { message = "An unexpected error occurred.", traceId }, cancellationToken: context.RequestAborted);
        }
    }
}
