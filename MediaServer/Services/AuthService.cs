using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Services
{
    public class AuthService : IAuthService
    {
        private readonly Dictionary<string, int> _activeTokens = new Dictionary<string, int>();

        public string HashPassword(string password)
        {
            return $"HASH_{password}";
        }

        public bool VerifyPassword(string providedPassword, string storedHash)
        {
            return storedHash == $"HASH_{providedPassword}";
        }

        public Task<string> GenerateTokenAsync(int userId)
        {
            string token = Guid.NewGuid().ToString("N");
            _activeTokens[token] = userId;
            return Task.FromResult(token);
        }

        public Task<int?> ValidateTokenAsync(string token)
        {
            if (_activeTokens.TryGetValue(token, out int userId))
            {
                return Task.FromResult<int?>(userId);
            }
            return Task.FromResult<int?>(null);
        }
    }
}
