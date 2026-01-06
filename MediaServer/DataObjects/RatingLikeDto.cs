using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class RatingLikeDto
    {
        [JsonPropertyName("ratingId")]
        public int RatingId { get; set; }
    }
}