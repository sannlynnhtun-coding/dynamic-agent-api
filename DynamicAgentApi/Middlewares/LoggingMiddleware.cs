using System.Diagnostics;
using System.Text;

namespace DynamicAgentApi.Middlewares;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;
    private Guid _guid;

    StringBuilder sb = new StringBuilder();

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _guid = Guid.NewGuid();

        var stopwatch = Stopwatch.StartNew();

        // Log request details (Method, Path, etc.)
        Log($"Handling request: {context.Request.Method} {context.Request.Path}");

        // Log request body
        var requestBody = await ReadRequestBodyAsync(context.Request);
        if (!string.IsNullOrEmpty(requestBody))
        {
            Log($"Request Body: {requestBody}");
        }

        // Log request headers
        foreach (var header in context.Request.Headers)
        {
            Log($"Request Header: {header.Key}: {header.Value}");
        }

        // // Capture the original response body stream
        // var originalResponseBodyStream = context.Response.Body;
        //
        // // Use a memory stream to capture the response body
        // using var responseBodyStream = new MemoryStream();
        // context.Response.Body = responseBodyStream;

        // Continue with the next middleware in the pipeline
        Stream originalBody = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        // call to the following middleware 
        // response should be produced by one of the following middlewares
        await _next(context);

        memStream.Position = 0;
        string responseBody = await new StreamReader(memStream).ReadToEndAsync();

        memStream.Position = 0;
        await memStream.CopyToAsync(originalBody);

        // Log response status code
        Log($"Response Status Code: {context.Response.StatusCode}");

        // Log response body
        Log($"Response Body: {responseBody}");

        stopwatch.Stop();
        Log($"Processing time: {stopwatch.ElapsedMilliseconds} ms");

        await File.AppendAllTextAsync($"log-{DateTime.Now.ToString("yyyyMMddhh")}.txt", sb.ToString());
        sb.Clear();
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        // Allow the request body to be read multiple times
        request.EnableBuffering();

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        // Reset the request body stream position so the next middleware can read it
        request.Body.Position = 0;

        return body;
    }

    private async Task<string> ReadResponseBodyAsync(MemoryStream responseBodyStream)
    {
        responseBodyStream.Position = 0;

        using var reader = new StreamReader(responseBodyStream);
        var body = await reader.ReadToEndAsync();

        // Reset the response body stream position for future operations
        responseBodyStream.Position = 0;

        return body;
    }

    private void Log(string message)
    {
        sb.Append($"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt")}] [{_guid.ToString("N")}]- " + message + "\n");
        _logger.LogInformation(message);
    }
}