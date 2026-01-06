using MediaServer.Models;
using MediaServer.Server;
using MediaServer.Services;
using MediaServer.Data;
using MediaServer.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaServer.Server
{
    public static class ApiEndpoints
    {
        private static readonly IAuthService _authService = new AuthService();
        private static IMediaRepository _repository = null!;
        private static RecommendationService _recommendationService = null!;
        public static void Configure(IMediaRepository repository)
        {
            _repository = repository;
            _recommendationService = new RecommendationService(repository);
        }

        public static void RegisterEndpoints(Router router)
        {
            // UserAuth
            router.AddRoute("POST", "/api/users/register", Register);
            router.AddRoute("POST", "/api/users/login", Login);
            // Search
            router.AddRoute("GET", "/api/media/search", SearchMedia);
            // Favorites & Recommendations
            router.AddRoute("POST", "/api/media/favorite", ToggleFavorite);
            router.AddRoute("GET", "/api/media/recommendations", GetRecommendations);
            router.AddRoute("GET", "/api/media/moderation", GetPendingRatings);
            // Ratings
            router.AddRoute("POST", "/api/ratings/like", ToggleLike);
            router.AddRoute("POST", "/api/ratings", AddRating);
            router.AddRoute("GET", "/api/leaderboard", GetLeaderboard);
            router.AddRoute("POST", "/api/ratings/approve", ApproveRating);
            router.AddRoute("DELETE", "/api/ratings", DeleteRating);
            router.AddRoute("PUT", "/api/ratings", UpdateRating);
            // Media
            router.AddRoute("POST", "/api/media", CreateMedia);
            router.AddRoute("PUT", "/api/media", UpdateMedia);
            router.AddRoute("DELETE", "/api/media", DeleteMedia);
            // User
            router.AddRoute("GET", "/api/users/profile", GetUserProfile);
            router.AddRoute("PUT", "/api/users/profile", UpdateProfile);
            router.AddRoute("GET", "/api/users/favorites", GetMyFavorites);
            router.AddRoute("GET", "/api/users/ratings", GetMyRatings);
        }

        private static async Task Register(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var dto = await RequestReader.ReadAndDeserialize<UserRegisterDto>(request);

            if (dto == null)
            {
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid request body." });
                return;
            }

            var existingUser = await _repository.GetUserByUsernameAsync(dto.Username);
            if (existingUser != null)
            {
                await Router.SendResponse(response, HttpStatusCode.Conflict, new { error = "Username already exists." });
                return;
            }

            var newUser = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = _authService.HashPassword(dto.Password)
            };

            await _repository.AddUserAsync(newUser);
            await Router.SendResponse(response, HttpStatusCode.Created, new { message = "Registration successful." });
        }

        private static async Task Login(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var dto = await RequestReader.ReadAndDeserialize<UserLoginDto>(request);

            if (dto == null)
            {
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid request body." });
                return;
            }

            User? user = await _repository.GetUserByUsernameAsync(dto.Username);

            if (user == null)
            {
                await Router.SendResponse(response, HttpStatusCode.Unauthorized, new { error = "Invalid credentials." });
                return;
            }

            if (!_authService.VerifyPassword(dto.Password, user!.PasswordHash))
            {
                await Router.SendResponse(response, HttpStatusCode.Unauthorized, new { error = "Invalid credentials." });
                return;
            }

            string token = await _authService.GenerateTokenAsync(user.Id);
            await Router.SendResponse(response, HttpStatusCode.OK, new { token = token, userId = user.Id });
        }
        private static async Task SearchMedia(HttpListenerContext context)
        {
            var request = context.Request;
            var query = request.QueryString;

            string? searchTerm = query["q"];      // "q" ist Standard für Search Queries
            string? genre = query["genre"];

            // Parsing für Integer und Enums (mit TryParse für Sicherheit)
            int? minRating = null;
            if (int.TryParse(query["minRating"], out int r)) minRating = r;

            MediaEntryType? type = null;
            if (int.TryParse(query["type"], out int t) && Enum.IsDefined(typeof(MediaEntryType), t))
            {
                type = (MediaEntryType)t;
            }

            // Aufruf ans Repository
            var results = await _repository.SearchMediaAsync(searchTerm, genre, type, minRating);

            await Router.SendResponse(context.Response, HttpStatusCode.OK, results);
        }
        private static async Task ToggleFavorite(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return; // Authenticate sendet bereits die Error-Response

            // Body lesen
            var dto = await RequestReader.ReadAndDeserialize<FavoriteDto>(context.Request);
            if (dto == null || dto.MediaId <= 0)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid mediaId." });
                return;
            }

            // Logik ausführen
            try
            {
                await _repository.ToggleFavoriteAsync(userId.Value, dto.MediaId);
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = "Favorite status toggled." });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Database error." });
            }
        }
        private static async Task GetRecommendations(HttpListenerContext context)
        {
            // Authentifizierung 
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Service aufrufen
            try
            {
                var recommendations = await _recommendationService.GetRecommendationsForUserAsync(userId.Value);
                await Router.SendResponse(context.Response, HttpStatusCode.OK, recommendations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recommendation Error: {ex.Message}");
                // Fallback: Leere Liste statt Crash
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new List<MediaEntry>());
            }
        }
        private static async Task<int?> Authenticate(HttpListenerContext context)
        {
            var authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Unauthorized, new { error = "Missing Authorization." });
                return null;
            }

            string token = authHeader.Substring("Bearer ".Length).Trim();
            int? userId = await _authService.ValidateTokenAsync(token);

            if (!userId.HasValue)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Invalid token." });
                return null;
            }
            return userId;
        }
        private static async Task ToggleLike(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Authentifizierung
            int? userId = await Authenticate(context);
            if (userId == null) return; // Authenticate sendet bereits die Error-Response

            // Request Body lesen

            var dto = await RequestReader.ReadAndDeserialize<RatingLikeDto>(request);

            if (dto == null || dto.RatingId <= 0)
            {
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Invalid request body or ratingId." });
                return;
            }

            // Aktion durchführen
            try
            {
                await _repository.ToggleRatingLikeAsync(userId.Value, dto.RatingId);

                await Router.SendResponse(response, HttpStatusCode.OK, new { message = "Rating like status toggled successfully." });
            }
            catch (Exception ex)
            {
                // Falls RatingId nicht existiert
                Console.WriteLine($"Error toggling like: {ex.Message}");
                await Router.SendResponse(response, HttpStatusCode.BadRequest, new { error = "Could not toggle like. Does the rating exist?" });
            }
        }
        private static async Task CreateMedia(HttpListenerContext context)
        {
            // Authentifizierung
            int? userId = await Authenticate(context);
            if (userId == null) return;

            var dto = await RequestReader.ReadAndDeserialize<CreateMediaDto>(context.Request);
            if (dto == null)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid body." });
                return;
            }

            // Mapping
            var newMedia = new MediaEntry
            {
                CreatorId = userId.Value,
                Title = dto.Title,
                Description = dto.Description,
                Type = dto.Type,
                Genre = dto.Genre,
                ReleaseYear = dto.ReleaseYear,
                AgeRestriction = dto.AgeRestriction,
                AverageRating = 0
            };

            try
            {
                var created = await _repository.AddMediaAsync(newMedia);
                // Created (201) zurückgeben
                await Router.SendResponse(context.Response, HttpStatusCode.Created, created);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating media: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Could not create media." });
            }
        }
        private static async Task AddRating(HttpListenerContext context)
        {
            int? userId = await Authenticate(context);
            if (userId == null) return;

            var dto = await RequestReader.ReadAndDeserialize<CreateRatingDto>(context.Request);

            // Validierung: Score muss 1-5 sein
            if (dto == null || dto.Score < 1 || dto.Score > 5)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid rating. Score must be 1-5." });
                return;
            }

            // Moderations-Logik wenn kommentar dabie ist muss es erst bestätigt werden
            bool autoConfirm = string.IsNullOrEmpty(dto.Comment);

            var rating = new Rating
            {
                MediaEntryId = dto.MediaId,
                UserId = userId.Value,
                Score = dto.Score,
                Comment = dto.Comment,
                IsConfirmed = autoConfirm,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var created = await _repository.AddRatingAsync(rating);

                var message = autoConfirm
                    ? "Rating added successfully."
                    : "Rating added via draft. Comment requires confirmation.";

                await Router.SendResponse(context.Response, HttpStatusCode.Created, new { message, rating = created });
            }
            catch (Exception ex)
            {
                //Media ID existiert nicht oder User hat schon bewertet
                Console.WriteLine($"Error adding rating: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Could not add rating. Check media ID." });
            }
        }
        private static async Task GetLeaderboard(HttpListenerContext context)
        {
            // Öffentliches Leaderboard also kein Auth
            try
            {
                var stats = await _repository.GetPublicLeaderboardAsync();
                await Router.SendResponse(context.Response, HttpStatusCode.OK, stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Error fetching leaderboard." });
            }
        }
        private static async Task GetUserProfile(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            try
            {
                // 2. Daten laden
                var profile = await _repository.GetUserProfileAsync(userId.Value);

                if (profile == null)
                {
                    // Eigentlich durch Auth abgedeckt aber zur Sicherheit
                    await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "User not found." });
                    return;
                }

                // 3. Antworten
                await Router.SendResponse(context.Response, HttpStatusCode.OK, profile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profile Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Could not fetch profile." });
            }
        }
        private static async Task UpdateProfile(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Input lesen
            var dto = await RequestReader.ReadAndDeserialize<UpdateProfileDto>(context.Request);

            // Validierung
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Username))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Username and Email are required." });
                return;
            }

            // Passwort-Logik
            string? passwordHashToSave = null;

            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                passwordHashToSave = _authService.HashPassword(dto.NewPassword);
            }

            try
            {
                // Update in DB ausführen
                await _repository.UpdateUserSensitiveDataAsync(userId.Value, dto.Username, dto.Email, passwordHashToSave);

                // Erfolg melden
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = "Profile updated successfully." });
            }
            catch (Npgsql.PostgresException pgEx)
            {
                // Fehlercode 23505 = Unique Constraint Violation (Username schon vergeben)
                if (pgEx.SqlState == "23505")
                {
                    await Router.SendResponse(context.Response, HttpStatusCode.Conflict, new { error = "Username already taken." });
                }
                else
                {
                    Console.WriteLine($"DB Error: {pgEx.Message}");
                    await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Database error." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Internal error." });
            }
        }
        private static async Task GetPendingRatings(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // MediaId aus Query lesen (?mediaId=1)
            var query = context.Request.QueryString;
            if (!int.TryParse(query["mediaId"], out int mediaId))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Missing mediaId parameter." });
                return;
            }

            // Berechtigung prüfen
            bool isCreator = await _repository.IsMediaCreatorAsync(mediaId, userId.Value);
            if (!isCreator)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Only the creator can moderate comments." });
                return;
            }

            // Daten holen
            var pending = await _repository.GetPendingRatingsForMediaAsync(mediaId);
            await Router.SendResponse(context.Response, HttpStatusCode.OK, pending);
        }
        private static async Task ApproveRating(HttpListenerContext context)
        {
            // A. Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Input: Rating ID
            var dto = await RequestReader.ReadAndDeserialize<RatingLikeDto>(context.Request);

            if (dto == null || dto.RatingId <= 0)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid ratingId." });
                return;
            }

            // Berechtigungen prüfen
            // Zu welchem Film gehört das Rating?
            int? mediaId = await _repository.GetMediaIdByRatingIdAsync(dto.RatingId);
            if (mediaId == null)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "Rating not found." });
                return;
            }

            // Gehört dieser Film dem eingeloggten User?
            bool isCreator = await _repository.IsMediaCreatorAsync(mediaId.Value, userId.Value);
            if (!isCreator)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "You are not the creator of this media entry." });
                return;
            }

            // Freischalten
            await _repository.ApproveRatingAsync(dto.RatingId);

            await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = "Comment approved and now public." });
        }
        private static async Task DeleteRating(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Rating ID aus Query lesen (?id=123)
            var query = context.Request.QueryString;
            if (!int.TryParse(query["id"], out int ratingId))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Missing or invalid id parameter." });
                return;
            }

            // Rating laden (um zu sehen, wem es gehört und zu welchem Film es gehört)
            var rating = await _repository.GetRatingByIdAsync(ratingId);
            if (rating == null)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "Rating not found." });
                return;
            }

            // Berechtigungen prüfen
            // Opt1: Ist es sein eigenes Rating?
            bool isAuthor = rating.UserId == userId.Value;

            // Opt2: Ist er der Besitzer des Films (Moderator)?
            bool isMediaCreator = await _repository.IsMediaCreatorAsync(rating.MediaEntryId, userId.Value);

            if (!isAuthor && !isMediaCreator)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Not authorized to delete this rating." });
                return;
            }

            // 5. Löschen ausführen
            try
            {
                await _repository.DeleteRatingAsync(ratingId);

                string msg = isAuthor ? "Rating deleted." : "Rating rejected/moderated successfully.";
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = msg });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Could not delete rating." });
            }
        }
        private static async Task DeleteMedia(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // ID aus Query lesen
            if (!int.TryParse(context.Request.QueryString["id"], out int mediaId))
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Missing id parameter." });
                return;
            }

            // Berechtigung prüfen
            bool isCreator = await _repository.IsMediaCreatorAsync(mediaId, userId.Value);

            if (!isCreator)
            {
                // existiert Media?
                var media = await _repository.GetMediaByIdAsync(mediaId);
                if (media == null)
                {
                    await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "Media not found." });
                    return;
                }

                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Only the creator can delete this media." });
                return;
            }

            // Löschen
            try
            {
                await _repository.DeleteMediaAsync(mediaId);
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = "Media deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Media Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Database error." });
            }
        }
        private static async Task UpdateMedia(HttpListenerContext context)
        {
            // Auth
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Body lesen
            var dto = await RequestReader.ReadAndDeserialize<UpdateMediaDto>(context.Request);
            if (dto == null)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid body." });
                return;
            }

            // Berechtigungen prüfen
            bool isCreator = await _repository.IsMediaCreatorAsync(dto.Id, userId.Value);
            if (!isCreator)
            {
                // Existiert Media?
                var existing = await _repository.GetMediaByIdAsync(dto.Id);
                if (existing == null)
                {
                    await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "Media not found." });
                    return;
                }

                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "Only the creator can edit this media." });
                return;
            }

            // Update Bauen
            var mediaUpdate = new MediaEntry
            {
                Id = dto.Id,
                CreatorId = userId.Value, // Wird behalten
                Title = dto.Title,
                Description = dto.Description,
                Type = dto.Type,
                Genre = dto.Genre,
                ReleaseYear = dto.ReleaseYear,
                AgeRestriction = dto.AgeRestriction,
                AverageRating = 0 // Wird ignoriert da nicht im SET
            };

            try
            {
                await _repository.UpdateMediaAsync(mediaUpdate);
                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = "Media updated successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Media Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Database error." });
            }
        }
        private static async Task GetMyFavorites(HttpListenerContext context)
        {
            int? userId = await Authenticate(context);
            if (userId == null) return;

            var favs = await _repository.GetFavoritesAsync(userId.Value);
            await Router.SendResponse(context.Response, HttpStatusCode.OK, favs);
        }
        private static async Task GetMyRatings(HttpListenerContext context)
        {
            int? userId = await Authenticate(context);
            if (userId == null) return;

            var ratings = await _repository.GetRatingsByUserAsync(userId.Value);
            await Router.SendResponse(context.Response, HttpStatusCode.OK, ratings);
        }
        private static async Task UpdateRating(HttpListenerContext context)
        {
            // Auth Check
            int? userId = await Authenticate(context);
            if (userId == null) return;

            // Body lesen
            var dto = await RequestReader.ReadAndDeserialize<UpdateRatingDto>(context.Request);

            // Validierung
            if (dto == null || dto.RatingId <= 0 || dto.Score < 1 || dto.Score > 5)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.BadRequest, new { error = "Invalid body or score (must be 1-5)." });
                return;
            }

            // Berechtigung prüfen
            var existingRating = await _repository.GetRatingByIdAsync(dto.RatingId);

            if (existingRating == null)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.NotFound, new { error = "Rating not found." });
                return;
            }

            if (existingRating.UserId != userId.Value)
            {
                await Router.SendResponse(context.Response, HttpStatusCode.Forbidden, new { error = "You can only edit your own ratings." });
                return;
            }

            // Updaten
            try
            {
                await _repository.UpdateRatingAsync(dto.RatingId, dto.Score, dto.Comment);

                string msg = string.IsNullOrEmpty(dto.Comment)
                    ? "Rating updated."
                    : "Rating updated. Comment set to pending moderation.";

                await Router.SendResponse(context.Response, HttpStatusCode.OK, new { message = msg });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update Rating Error: {ex.Message}");
                await Router.SendResponse(context.Response, HttpStatusCode.InternalServerError, new { error = "Database error." });
            }
        }
    }
}
