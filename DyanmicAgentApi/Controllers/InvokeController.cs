using Microsoft.AspNetCore.Mvc;

namespace DyanmicAgentApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InvokeController : ControllerBase
    {
        [HttpPost]
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
                foreach (var header in Request.Headers.Where(x=> x.Key != "Host"))
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                HttpContent? content = null;
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync(cancellationToken);
                if (!requestBody.IsNullOrEmpty())
                {
                    content = !requestBody.IsNullOrEmpty()
                        ? new StringContent(requestBody,
                            System.Text.Encoding.UTF8,
                            "application/json")
                        : null;
                    responseMessage = await httpClient.PostAsync(new Uri(url), content, cancellationToken);
                }
                else if (Request.Form.Count > 0)
                {
                    KeyValuePair<string, string>[] keyValuePairs = new KeyValuePair<string, string>[Request.Form.Count];
                    int count = 0;
                    foreach (var form in Request.Form)
                    {
                        keyValuePairs[count++] = new KeyValuePair<string, string>(form.Key, form.Value.ToString());
                    }

                    var formContent = new FormUrlEncodedContent(keyValuePairs);
                    responseMessage = await httpClient.PostAsync(new Uri(url), formContent, cancellationToken);
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

                // foreach (var header in _responseMessage.Headers)
                // {
                //     context.HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
                // }

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
}