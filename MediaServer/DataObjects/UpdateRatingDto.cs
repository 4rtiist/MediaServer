using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class UpdateRatingDto
    {
        [JsonPropertyName("ratingId")]
        public int RatingId { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; } // 1-5

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
    }
}