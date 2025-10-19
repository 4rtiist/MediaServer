using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaServer.Models;

namespace MediaServer.Data
{
    public interface IMediaRepository
    {
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User> AddUserAsync(User user);

        Task<List<MediaEntry>> GetAllMediaAsync();
        Task<Rating> AddRatingAsync(Rating rating);

    }
}
