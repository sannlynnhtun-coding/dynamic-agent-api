using Microsoft.AspNetCore.Mvc;

namespace DynamicAgentApi.Controllers;

[ApiController]
[Route("")]
public class InvokeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [Route("invoke")]
    [HttpGet, HttpPost, HttpPut, HttpPatch, HttpDelete]
    public async Task<IActionResult> Invoke(
        [FromServices] HttpClient httpClient,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken,
        [FromQuery] string url)
    {
        try
        {
            HttpResponseMessage? responseMessage = null;
            httpClient.DefaultRequestHeaders.Clear();

            // Copy request headers from the incoming request to the HttpClient request
            foreach (var header in Request.Headers.Where(x => x.Key != "Host"))
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            HttpContent? content = null;
            var contentType = Request.ContentType;

            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync(cancellationToken);

            // Handle different content types for POST/PUT/PATCH requests
            if (!requestBody.IsNullOrEmpty())
            {
                // Check the Content-Type header to determine how to handle the request body
                if (contentType == "application/json")
                {
                    content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
                }
                else if (contentType == "application/x-www-form-urlencoded")
                {
                    var formData = requestBody.Split('&')
                        .Select(kv => kv.Split('='))
                        .ToDictionary(kv => kv[0], kv => kv.Length > 1 ? kv[1] : string.Empty);
                    content = new FormUrlEncodedContent(formData);
                }
                else if (contentType == "multipart/form-data")
                {
                    // For multipart/form-data, handle the form data
                    // NOTE: Multipart form data requires a more complex handling for files.
                    throw new Exception("multipart/form-data is not supported in this sample implementation.");
                }
                else
                {
                    content = new StringContent(requestBody, System.Text.Encoding.UTF8,
                        contentType ?? "text/plain");
                }
            }
            // Only access form data if the content type is appropriate
            else if (contentType == "application/x-www-form-urlencoded" && Request.Form.Count > 0)
            {
                KeyValuePair<string, string>[] keyValuePairs = new KeyValuePair<string, string>[Request.Form.Count];
                int count = 0;
                foreach (var form in Request.Form)
                {
                    keyValuePairs[count++] = new KeyValuePair<string, string>(form.Key, form.Value.ToString());
                }

                content = new FormUrlEncodedContent(keyValuePairs);
            }

            // Determine the method type (GET, POST, PUT, PATCH, DELETE) and send the corresponding request
            switch (Request.Method.ToUpper())
            {
                case "GET":
                    responseMessage = await httpClient.GetAsync(new Uri(url), cancellationToken);
                    break;
                case "POST":
                    responseMessage = await httpClient.PostAsync(new Uri(url), content, cancellationToken);
                    break;
                case "PUT":
                    responseMessage = await httpClient.PutAsync(new Uri(url), content, cancellationToken);
                    break;
                case "PATCH":
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), new Uri(url))
                        { Content = content };
                    responseMessage = await httpClient.SendAsync(request, cancellationToken);
                    break;
                case "DELETE":
                    responseMessage = await httpClient.DeleteAsync(new Uri(url), cancellationToken);
                    break;
                default:
                    throw new Exception("Unsupported HTTP method");
            }

            if (responseMessage != null)
                return ConvertHttpResponseMessageToIActionResult(responseMessage);
                //return new HttpResponseMessageResult(responseMessage);
            return BadRequest();

            //if (responseMessage != null)
            //    return responseMessage;

            //return null;
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Message = ex.ToString()
            });

            //throw;
        }
    }

    public IActionResult ConvertHttpResponseMessageToIActionResult(HttpResponseMessage httpResponseMessage)
    {
        // Extract the content as a string if available
        var contentTask = httpResponseMessage.Content?.ReadAsStringAsync();
        contentTask?.Wait();
        var content = contentTask?.Result ?? string.Empty;

        // Create a ContentResult to represent the response
        var result = new ContentResult
        {
            Content = content,
            StatusCode = (int)httpResponseMessage.StatusCode,
            ContentType = httpResponseMessage.Content?.Headers?.ContentType?.ToString() ?? "text/plain"
        };

        // Copy headers from HttpResponseMessage to ContentResult
        foreach (var header in httpResponseMessage.Headers)
        {
            HttpContext.Response.Headers[header.Key] = string.Join(",", header.Value);
        }

        // Copy content-specific headers (if they exist)
        if (httpResponseMessage.Content != null)
        {
            foreach (var contentHeader in httpResponseMessage.Content.Headers)
            {
                HttpContext.Response.Headers[contentHeader.Key] = string.Join(",", contentHeader.Value);
            }
        }

        return result;
    }

    private class HttpResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _responseMessage;

        public HttpResponseMessageResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            // Set status code
            context.HttpContext.Response.StatusCode = (int)_responseMessage.StatusCode;

            // Clear existing headers
            context.HttpContext.Response.Headers.Clear();

            // Copy headers from HttpResponseMessage, ensuring UTF-8 content-type if applicable
            foreach (var header in _responseMessage.Headers)
            {
                context.HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }

            // Ensure Content-Type includes UTF-8
            if (_responseMessage.Content.Headers.ContentType != null)
            {
                var charset = _responseMessage.Content.Headers.ContentType.CharSet;

                // If the charset is not UTF-8, set it to UTF-8
                if (string.IsNullOrEmpty(charset) || !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                {
                    _responseMessage.Content.Headers.ContentType.CharSet = "utf-8";
                }
            }

            // Copy content to the response body with UTF-8 encoding
            await _responseMessage.Content.CopyToAsync(context.HttpContext.Response.Body);
        }
    }
}

public static class DevCode
{
    public static bool IsNullOrEmpty(this object str)
    {
        bool result = true;
        try
        {
            result = str == null || string.IsNullOrEmpty(str.ToString().Trim()) ||
                     string.IsNullOrWhiteSpace(str.ToString().Trim());
        }
        catch
        {
        }

        return result;
    }
}