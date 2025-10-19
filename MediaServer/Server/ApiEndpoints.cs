using MediaServer.Models;
using MediaServer.Server;
using MediaServer.Services;
using MediaServer.Data;
using MediaServer.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaServer.Server
{
    public static class ApiEndpoints
    {
        private static readonly IAuthService _authService = new AuthService();
        private static readonly IMediaRepository _repository = new InMemoryMediaRepository();

        public static void RegisterEndpoints(Router router)
        {
            router.AddRoute("POST", "api/users/register", Register);
            router.AddRoute("POST", "api/users/login", Login);

            router.AddRoute("GET", "api/media/recommendations", GetRecommendations);
        }

        private static async Task Register(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var dto = await RequestReader.ReadAndDeserialize<UserRegisterDto>(request);

            if (dto == null)
            {
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid request body." });
                return;
            }

            var existingUser = await _repository.GetUserByUsernameAsync(dto.Username);
            if (existingUser != null)
            {
                await Router.SendResponse(response, HttpStatusCode.Conflict, new { error = "Username already exists." });
                return;
            }

            var newUser = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = _authService.HashPassword(dto.Password)
            };

            await _repository.AddUserAsync(newUser);
            await Router.SendResponse(response, HttpStatusCode.Created, new { message = "Registration successful." });
        }

        private static async Task Login(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var dto = await RequestReader.ReadAndDeserialize<UserLoginDto>(request);

            if (dto == null)
            {
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid request body." });
                return;
            }

            User? user = await _repository.GetUserByUsernameAsync(dto.Username);

            if (user == null)
            {
                await Router.SendResponse(response, HttpStatusCode.Unauthorized, new { error = "Invalid credentials." });
                return;
            }

            if (!_authService.VerifyPassword(dto.Password, user!.PasswordHash))
            {
                await Router.SendResponse(response, HttpStatusCode.Unauthorized, new { error = "Invalid credentials." });
                return;
            }

            string token = await _authService.GenerateTokenAsync(user.Id);
            await Router.SendResponse(response, HttpStatusCode.OK, new { token = token, userId = user.Id });
        }

        private static async Task GetRecommendations(HttpListenerContext context)
        {
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Unauthorized, new { error = "Missing or invalid Authorization header." });
                return;
            }

            string token = authHeader.Substring("Bearer ".Length).Trim();
            int? userId = await _authService.ValidateTokenAsync(token);

            if (!userId.HasValue)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Invalid or expired token." });
                return;
            }

            var recommendations = new[] { new { Title = "Upgraded Recommendations!", User = userId.Value } };

            await Router.SendResponse(context.Response, HttpStatusCode.OK, recommendations);
        }
    }
}
