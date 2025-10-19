using MediaServer.Data;
using MediaServer.Server;
using MediaServer.DataObjects;
using MediaServer.Models;
using MediaServer.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer
{
    class Program
    {
        private static HttpListener listener = null!;
        private static readonly string URL = "http://localhost:8080/";
        private static readonly Router router = new Router();

        static async Task Main(string[] args)
        {
            ApiEndpoints.RegisterEndpoints(router);

            listener = new HttpListener();
            listener.Prefixes.Add(URL);

            try
            {
                listener.Start();
                Console.WriteLine($"Server started. Listening for requests at {URL}");
                await Listen();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"HttpListener failed: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
                Console.WriteLine("Server stopped.");
            }
        }

        private static async Task Listen()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                _ = router.HandleRequest(context);
            }
        }
    }
}