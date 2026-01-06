using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class UpdateProfileDto
    {
        [JsonPropertyName("username")]
        public required string Username { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        // Optional
        [JsonPropertyName("newPassword")]
        public string? NewPassword { get; set; }
    }
}