using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class FavoriteDto
    {
        [JsonPropertyName("mediaId")]
        public int MediaId { get; set; }
    }
}