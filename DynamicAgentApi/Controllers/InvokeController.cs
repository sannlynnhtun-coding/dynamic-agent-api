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
                    return BadRequest("multipart/form-data is not supported in this sample implementation.");
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
                    return BadRequest("Unsupported HTTP method");
            }

            if (responseMessage != null)
                return new HttpResponseMessageResult(responseMessage);
            return BadRequest();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Message = ex.ToString()
            });
        }
    }

    [Route("test")]
    [HttpPost]
    public IActionResult Test()
    {
        return Ok(new { Message = "Testing" });
    }

    private class HttpResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _responseMessage;

        public HttpResponseMessageResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)_responseMessage.StatusCode;
            context.HttpContext.Response.Headers.Clear();

            foreach (var header in _responseMessage.Content.Headers)
            {
                context.HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }

            return _responseMessage.Content.CopyToAsync(context.HttpContext.Response.Body);
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