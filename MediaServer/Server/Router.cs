using System.Net;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MediaServer.Server
{
    public delegate Task RequestHandler(HttpListenerContext context);

    public class Router
    {
        private readonly Dictionary<string, RequestHandler> routes = new Dictionary<string, RequestHandler>();

        public void AddRoute(string method, string path, RequestHandler handler)
        {
            string key = $"{method.ToUpper()}:{path}";
            routes[key] = handler;
        }

        public async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            if (request.Url == null)
            {
                await SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid URL." });
                return;
            }

            string key = $"{request.HttpMethod.ToUpper()}:{request.Url.LocalPath.ToLower()}";

            Console.WriteLine($"Received request: {key}");

            if (routes.TryGetValue(key, out RequestHandler? handler) && handler is not null)
            {
                try
                {
                    await handler(context);
                }
                catch (Exception ex)
                {
                    await SendResponse(response, HttpStatusCode.InternalServerError, new { error = "An internal error occurred." });
                    Console.WriteLine($"Error processing request: {ex.Message}");
                }
            }
            else
            {
                await SendResponse(response, HttpStatusCode.NotFound, new { error = "Endpoint not found." });
            }
        }

        public static async Task SendResponse<T>(HttpListenerResponse response, HttpStatusCode statusCode, T content)
        {
            string json = JsonSerializer.Serialize(content);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = (int)statusCode;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}