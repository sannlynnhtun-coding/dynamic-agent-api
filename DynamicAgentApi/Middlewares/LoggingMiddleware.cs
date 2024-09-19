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

        Log($"Handling request: {context.Request.Method} {context.Request.Path}");

        var requestBody = await ReadRequestBodyAsync(context.Request);
        if (!string.IsNullOrEmpty(requestBody))
        {
            Log($"Request Body: {requestBody}");
        }

        foreach (var header in context.Request.Headers)
        {
            Log($"Request Header: {header.Key}: {header.Value}");
        }

        Stream originalBody = context.Response.Body;
        using var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await _next(context);

        memStream.Position = 0;
        string responseBody = await new StreamReader(memStream).ReadToEndAsync();

        memStream.Position = 0;
        await memStream.CopyToAsync(originalBody);

        Log($"Response Status Code: {context.Response.StatusCode}");
        Log($"Response Body: {responseBody}");

        stopwatch.Stop();
        Log($"Processing time: {stopwatch.ElapsedMilliseconds} ms");

        await File.AppendAllTextAsync($"log-{DateTime.Now.ToString("yyyyMMddhh")}.txt", sb.ToString());
        sb.Clear();
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }

    private async Task<string> ReadResponseBodyAsync(MemoryStream responseBodyStream)
    {
        responseBodyStream.Position = 0;
        using var reader = new StreamReader(responseBodyStream);
        var body = await reader.ReadToEndAsync();
        responseBodyStream.Position = 0;
        return body;
    }

    private void Log(string message)
    {
        sb.Append($"[{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt")}] [{_guid.ToString("N")}]- " + message + "\n");
        _logger.LogInformation(message);
    }
}