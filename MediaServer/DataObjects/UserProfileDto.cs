using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class UserProfileDto
    {
        [JsonPropertyName("username")]
        public required string Username { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("totalRatings")]
        public int TotalRatings { get; set; }

        [JsonPropertyName("averageScoreGiven")]
        public double AverageScoreGiven { get; set; }

        [JsonPropertyName("favoriteGenre")]
        public string? FavoriteGenre { get; set; } // Kann null sein, wenn noch keine Bewertungen da sind
    }
}