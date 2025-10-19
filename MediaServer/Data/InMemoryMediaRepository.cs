using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaServer.Models;

namespace MediaServer.Data
{
    public class InMemoryMediaRepository : IMediaRepository
    {
        private readonly List<User?> _users = new List<User?>();
        private int _nextUserId = 1;

        public Task<User?> GetUserByUsernameAsync(string username)
        {
            var user = _users.FirstOrDefault(u => u is not null && u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<User> AddUserAsync(User user)
        {
            user.Id = _nextUserId++;
            _users.Add(user);
            return Task.FromResult(user);
        }

        public Task<List<MediaEntry>> GetAllMediaAsync() => Task.FromResult(new List<MediaEntry>());
        public Task<Rating> AddRatingAsync(Rating rating) => Task.FromResult(rating);
    }
}
