using System.Diagnostics;
using System.Text;

namespace DynamicAgentApi.Middlewares;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;
    private Guid _guid;
    private readonly StringBuilder _sb = new StringBuilder();

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
        try
        {
            var requestBody = await ReadRequestBody(context.Request);
            if (!string.IsNullOrEmpty(requestBody))
            {
                Log($"Request Body: {requestBody}");
            }
        }
        catch (NotSupportedException ex)
        {
            Log($"Request Body NotSupportedException: {ex.Message}");
        }

        // Log request headers
        foreach (var header in context.Request.Headers)
        {
            Log($"Request Header: {header.Key}: {header.Value}");
        }

        //// Capture the original response body stream
        //var originalBodyStream = context.Response.Body;

        //using var memStream = new MemoryStream();
        //context.Response.Body = memStream;

        //try
        //{
        //    // Continue with the next middleware in the pipeline
        //    await _next(context);

        //    // Log response body
        //    try
        //    {
        //        var responseBodyContent = await ReadResponseBody(context.Response);
        //        Log($"Response Status Code: {context.Response.StatusCode}");
        //        Log($"Response Body: {responseBodyContent}");
        //    }
        //    catch (NotSupportedException ex)
        //    {
        //        Log($"Response Body NotSupportedException: {ex.Message}");
        //    }
        //}
        //finally
        //{
        //    // Restore the original response body stream
        //    //memStream.Seek(0, SeekOrigin.Begin);
        //    //await memStream.CopyToAsync(originalBodyStream);
        //    //context.Response.Body = originalBodyStream;
        //}
        var originalResponseBodyStream = context.Response.Body;
        using var responseBodyMemoryStream = new MemoryStream();
        context.Response.Body = responseBodyMemoryStream;

        context.Request.EnableBuffering();

        // Proceed with the request
        await _next(context);

        // After the response is generated, reset the stream and read the response body
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();

        // Log or process the response body (assumed to be JSON here)
        _logger.LogInformation("Response Body: " + responseBodyText);

        Log($"Response Status Code: {context.Response.StatusCode}");
        Log($"Response Body: {responseBodyText}");

        // Reset the position and copy the contents to the original stream
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        await responseBodyMemoryStream.CopyToAsync(originalResponseBodyStream);

        stopwatch.Stop();
        Log($"Processing time: {stopwatch.ElapsedMilliseconds} ms");

        // Append logs to a file
        var logFileName = $"log-{DateTime.Now:yyyyMMddHH}.txt";
        await File.AppendAllTextAsync(logFileName, _sb.ToString());
        _sb.Clear();
    }

    private async Task<string> ReadRequestBody(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            // Log if the stream is non-seekable
            Log("Request Body is not seekable.");
            return null;
        }

        request.EnableBuffering(); // Ensure buffering to allow reading the body multiple times
        request.Body.Position = 0; // Reset the stream position

        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset the stream position for downstream middleware
            return body;
        }
    }

    private async Task<string> ReadResponseBody(HttpResponse response)
    {
        if (!response.Body.CanSeek)
        {
            // Log if the stream is non-seekable
            Log("Response Body is not seekable.");
            return null;
        }

        response.Body.Seek(0, SeekOrigin.Begin); // Reset the stream position
        using (var reader = new StreamReader(response.Body, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin); // Reset the stream position for further processing
            return body;
        }
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{_guid:N}] - {message}\n";
        _sb.Append(logEntry);
        _logger.LogInformation(message);
    }
}

