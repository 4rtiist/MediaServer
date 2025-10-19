using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.IO;

namespace MediaServer.Server
{
    public static class RequestReader
    {
        public static async Task<T?> ReadAndDeserialize<T>(HttpListenerRequest request) where T : class
        {
            if (request.ContentLength64 == 0)
            {
                return null;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var json = await reader.ReadToEndAsync();

            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
