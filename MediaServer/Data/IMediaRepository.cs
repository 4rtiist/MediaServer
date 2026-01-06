using MediaServer.DataObjects;
using MediaServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Data
{
    public interface IMediaRepository
    {
        // User
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(int id);
        Task<User> AddUserAsync(User user);
        Task<List<UserStats>> GetPublicLeaderboardAsync();
        // Media
        Task<MediaEntry> AddMediaAsync(MediaEntry media);
        Task<MediaEntry?> GetMediaByIdAsync(int id);
        Task UpdateMediaAsync(MediaEntry media);
        Task DeleteMediaAsync(int id);
        Task<int?> GetMediaIdByRatingIdAsync(int ratingId);
        Task UpdateRatingAsync(int ratingId, int score, string? comment);
        // suche&filterung
        Task<List<MediaEntry>> SearchMediaAsync(string? searchTerm, string? genre, MediaEntryType? type, int? minRating);
        Task<List<MediaEntry>> GetFavoritesAsync(int userId);
        Task ToggleFavoriteAsync(int userId, int mediaId); // Add/Remove
        Task<List<MediaEntry>> GetLikedMediaByRatingsAsync(int userId);
        Task<List<int>> GetRatedMediaIdsAsync(int userId);
        // Ratings
        Task<Rating> AddRatingAsync(Rating rating);
        Task<Rating?> GetRatingByIdAsync(int id);
        Task DeleteRatingAsync(int id);
        Task<List<Rating>> GetRatingsForMediaAsync(int mediaId);
        Task ToggleRatingLikeAsync(int userId, int ratingId);
        Task<List<Rating>> GetPendingRatingsForMediaAsync(int mediaId);
        Task<bool> IsMediaCreatorAsync(int mediaId, int userId);
        Task ApproveRatingAsync(int ratingId);
        Task<List<Rating>> GetRatingsByUserAsync(int userId);
        // User & Stats
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task UpdateUserSensitiveDataAsync(int userId, string newUsername, string newEmail, string? newPasswordHash);

    }
}
