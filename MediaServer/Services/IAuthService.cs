using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaServer.Models;

namespace MediaServer.Services
{
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string providedPassword, string storedHash);

        Task<string> GenerateTokenAsync(int userId);
        Task<int?> ValidateTokenAsync(string token);
    }
}
