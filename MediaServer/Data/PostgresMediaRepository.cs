using MediaServer.DataObjects;
using MediaServer.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Data
{
    public class PostgresMediaRepository : IMediaRepository
    {

        private readonly string _connectionString;

        public PostgresMediaRepository(string connectionString)
        {
            _connectionString = connectionString;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                // Debug-Ausgabe 1
                Console.WriteLine($"Verbinde zu Datenbank: ");

                using var conn = new NpgsqlConnection(_connectionString);
                conn.Open();

                // Debug-Ausgabe 2
                Console.WriteLine("Verbindung erfolgreich! Erstelle Tabellen...");

                var sql = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id SERIAL PRIMARY KEY,
                        Username TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        Email TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS MediaEntries (
                        Id SERIAL PRIMARY KEY,
                        CreatorId INT REFERENCES Users(Id),
                        Title TEXT NOT NULL,
                        Description TEXT NOT NULL,
                        Type INT NOT NULL,
                        Genre TEXT NOT NULL,
                        ReleaseYear INT NOT NULL,
                        AgeRestriction INT NOT NULL,
                        AverageRating DOUBLE PRECISION DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS Ratings (
                        Id SERIAL PRIMARY KEY,
                        MediaEntryId INT REFERENCES MediaEntries(Id) ON DELETE CASCADE,
                        UserId INT REFERENCES Users(Id),
                        Score INT NOT NULL,
                        Comment TEXT,
                        IsConfirmed BOOLEAN DEFAULT FALSE,
                        Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS Favorites (
                        UserId INT REFERENCES Users(Id),
                        MediaId INT REFERENCES MediaEntries(Id) ON DELETE CASCADE,
                        PRIMARY KEY (UserId, MediaId)
                    );

                    CREATE TABLE IF NOT EXISTS RatingLikes (
                        UserId INT REFERENCES Users(Id),
                        RatingId INT REFERENCES Ratings(Id) ON DELETE CASCADE,
                        PRIMARY KEY (UserId, RatingId)
                    );
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.ExecuteNonQuery();

                // Debug-Ausgabe 3
                Console.WriteLine("Tabellen wurden erfolgreich erstellt (oder existierten bereits).");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"KRITISCHER DATENBANK-FEHLER: {ex.Message}");
                Console.ResetColor();
            }
        }

        private NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            return conn;
        }

        // User Methoden
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var conn = GetConnection();
            using var cmd =
                new NpgsqlCommand("SELECT Id, Username, PasswordHash, Email FROM Users WHERE Username = @Username",
                    conn);

            // gegen SQL Injection sichern
            cmd.Parameters.AddWithValue("@Username", username);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Manuelles Mapping
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    Email = reader.GetString(3)
                };
            }

            return null; // wenn kein User gefunden
        }

        public async Task<User> AddUserAsync(User user)
        {
            using var conn = GetConnection();
            var sql = @"
                INSERT INTO Users (Username, PasswordHash, Email) 
                VALUES (@Username, @PasswordHash, @Email)
                RETURNING Id;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Username", user.Username);
            cmd.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@Email", user.Email);

            // ExecuteScalar liest den ersten Wert der ersten Zeile (hier die zurückgegebene ID)
            var newId = await cmd.ExecuteScalarAsync();

            if (newId != null)
            {
                user.Id = Convert.ToInt32(newId);
            }

            return user;
        }

        public async Task<List<UserStats>> GetPublicLeaderboardAsync()
        {
            var result = new List<UserStats>();
            using var conn = GetConnection();
            var sql = @"
                SELECT u.Username, COUNT(r.Id) as TotalRatings
                FROM Users u
                JOIN Ratings r ON u.Id = r.UserId
                GROUP BY u.Id, u.Username
                ORDER BY TotalRatings DESC
                LIMIT 10;";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new UserStats
                {
                    Username = reader.GetString(0),
                    TotalRatings = reader.GetInt32(1) // oder reader.GetInt64(1) je nach Count-Typ in PG
                });
            }

            return result;
        }

        // Media Entry Methoden
        public async Task<MediaEntry> AddMediaAsync(MediaEntry media)
        {
            using var conn = GetConnection();
            var sql = @"
                INSERT INTO MediaEntries (CreatorId, Title, Description, Type, Genre, ReleaseYear, AgeRestriction, AverageRating)
                VALUES (@CreatorId, @Title, @Description, @Type, @Genre, @ReleaseYear, @AgeRestriction, 0)
                RETURNING Id;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CreatorId", media.CreatorId);
            cmd.Parameters.AddWithValue("@Title", media.Title);
            cmd.Parameters.AddWithValue("@Description", media.Description);
            cmd.Parameters.AddWithValue("@Type", (int)media.Type); // Enum zu Int casten
            cmd.Parameters.AddWithValue("@Genre", media.Genre);
            cmd.Parameters.AddWithValue("@ReleaseYear", media.ReleaseYear);
            cmd.Parameters.AddWithValue("@AgeRestriction", media.AgeRestriction);

            var newId = await cmd.ExecuteScalarAsync();
            if (newId != null) media.Id = Convert.ToInt32(newId);

            return media;
        }

        public async Task<List<MediaEntry>> SearchMediaAsync(string? searchTerm, string? genre, MediaEntryType? type,
            int? minRating)
        {
            var list = new List<MediaEntry>();
            using var conn = GetConnection();

            // 1. Basis-SQL
            var sql = "SELECT Id, CreatorId, Title, Description, Type, Genre, ReleaseYear, AgeRestriction, AverageRating FROM MediaEntries WHERE 1=1"; ;

            using var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            // 2. Filter dynamisch anhängen
            if (!string.IsNullOrEmpty(searchTerm))
            {
                sql += " AND Title ILIKE @Search"; // ILIKE = Case Insensitive in Postgres
                cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");
            }

            if (!string.IsNullOrEmpty(genre))
            {
                sql += " AND Genre = @Genre";
                cmd.Parameters.AddWithValue("@Genre", genre);
            }

            if (type.HasValue)
            {
                sql += " AND Type = @Type";
                cmd.Parameters.AddWithValue("@Type", (int)type.Value);
            }

            if (minRating.HasValue)
            {
                sql += " AND AverageRating >= @MinRating";
                cmd.Parameters.AddWithValue("@MinRating", (double)minRating.Value);
            }

            // 3. Ausführen und Mappen
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new MediaEntry
                {
                    Id = reader.GetInt32(0),
                    CreatorId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    Description = reader.GetString(3),
                    Type = (MediaEntryType)reader.GetInt32(4),
                    Genre = reader.GetString(5),
                    ReleaseYear = reader.GetInt32(6),
                    AgeRestriction = reader.GetInt32(7),
                    AverageRating = reader.GetDouble(8)
                });
            }

            return list;
        }

        private MediaEntry MapReaderToMediaEntry(NpgsqlDataReader reader)
        {
            return new MediaEntry
            {
                Id = reader.GetInt32(0),
                CreatorId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.GetString(3),
                Type = (MediaEntryType)reader.GetInt32(4), // Int zu Enum casten
                Genre = reader.GetString(5),
                ReleaseYear = reader.GetInt32(6),
                AgeRestriction = reader.GetInt32(7),
                AverageRating = reader.GetDouble(8)
            };
        }

        // Rating Methoden
        public async Task<Rating> AddRatingAsync(Rating rating)
        {
            using var conn = GetConnection();
            var sql = @"
                INSERT INTO Ratings (MediaEntryId, UserId, Score, Comment, IsConfirmed)
                VALUES (@MediaId, @UserId, @Score, @Comment, @IsConfirmed)
                RETURNING Id;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MediaId", rating.MediaEntryId);
            cmd.Parameters.AddWithValue("@UserId", rating.UserId);
            cmd.Parameters.AddWithValue("@Score", rating.Score);
            cmd.Parameters.AddWithValue("@IsConfirmed", rating.IsConfirmed);

            // Handling von NULL bei Parametern (Comment ist optional)
            if (rating.Comment == null)
            {
                cmd.Parameters.AddWithValue("@Comment", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@Comment", rating.Comment);
            }

            var newId = await cmd.ExecuteScalarAsync();
            if (newId != null) rating.Id = Convert.ToInt32(newId);

            await RecalculateAverageRatingAsync(rating.MediaEntryId);

            return rating;
        }

        public async Task<List<Rating>> GetRatingsForMediaAsync(int mediaId)
        {
            var list = new List<Rating>();
            using var conn = GetConnection();
            var sql =
                "SELECT Id, MediaEntryId, UserId, Score, Comment, IsConfirmed, Timestamp FROM Ratings WHERE MediaEntryId = @Mid AND IsConfirmed = TRUE";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Mid", mediaId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Rating
                {
                    Id = reader.GetInt32(0),
                    MediaEntryId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    Score = reader.GetInt32(3),

                    // WICHTIG: Handling von NULL beim Lesen (Comment kann NULL sein in DB)
                    Comment = reader.IsDBNull(4) ? null : reader.GetString(4),

                    IsConfirmed = reader.GetBoolean(5),
                    Timestamp = reader.GetDateTime(6),
                    // LikeCount wird hier noch nicht geladen, dafür bräuchte man einen JOIN oder Subquery
                    LikeCount = 0
                });
            }

            return list;
        }

        public async Task ToggleFavoriteAsync(int userId, int mediaId)
        {
            using var conn = GetConnection();

            // Gibt es den Favoriten schon
            using var checkCmd = new NpgsqlCommand("SELECT COUNT(1) FROM Favorites WHERE UserId = @Uid AND MediaId = @Mid", conn);
            checkCmd.Parameters.AddWithValue("@Uid", userId);
            checkCmd.Parameters.AddWithValue("@Mid", mediaId);

            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count > 0)
            {
                // Entfernen (Unmark)
                using var delCmd = new NpgsqlCommand("DELETE FROM Favorites WHERE UserId = @Uid AND MediaId = @Mid", conn);
                delCmd.Parameters.AddWithValue("@Uid", userId);
                delCmd.Parameters.AddWithValue("@Mid", mediaId);
                await delCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Hinzufügen (Mark)
                using var insCmd = new NpgsqlCommand("INSERT INTO Favorites (UserId, MediaId) VALUES (@Uid, @Mid)", conn);
                insCmd.Parameters.AddWithValue("@Uid", userId);
                insCmd.Parameters.AddWithValue("@Mid", mediaId);
                await insCmd.ExecuteNonQueryAsync();
            }

        }

        public async Task<List<MediaEntry>> GetFavoritesAsync(int userId)
        {
            var list = new List<MediaEntry>();
            using var conn = GetConnection();
            var sql = @"
                SELECT m.Id, m.CreatorId, m.Title, m.Description, m.Type, m.Genre, m.ReleaseYear, m.AgeRestriction, m.AverageRating
                FROM MediaEntries m
                JOIN Favorites f ON m.Id = f.MediaId
                WHERE f.UserId = @Uid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReaderToMediaEntry(reader)); // Helper verwenden
            }
            return list;
        }
        public async Task ToggleRatingLikeAsync(int userId, int ratingId)
        {
            using var conn = GetConnection();

            // Gibt es diesen Like schon
            var checkSql = "SELECT COUNT(1) FROM RatingLikes WHERE UserId = @UserId AND RatingId = @RatingId";

            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("@UserId", userId);
            checkCmd.Parameters.AddWithValue("@RatingId", ratingId);

            // ExecuteScalarAsync gibt ein Objekt zurück, das wir zu int konvertieren müssen.
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count > 0)
            {
                // Like existiert bereits
                var deleteSql = "DELETE FROM RatingLikes WHERE UserId = @UserId AND RatingId = @RatingId";

                using var deleteCmd = new NpgsqlCommand(deleteSql, conn);
                deleteCmd.Parameters.AddWithValue("@UserId", userId);
                deleteCmd.Parameters.AddWithValue("@RatingId", ratingId);

                await deleteCmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Like existiert noch nicht
                var insertSql = "INSERT INTO RatingLikes (UserId, RatingId) VALUES (@UserId, @RatingId)";

                using var insertCmd = new NpgsqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@UserId", userId);
                insertCmd.Parameters.AddWithValue("@RatingId", ratingId);

                await insertCmd.ExecuteNonQueryAsync();
            }
        }
        public async Task<List<MediaEntry>> GetLikedMediaByRatingsAsync(int userId)
        {
            var list = new List<MediaEntry>();
            using var conn = GetConnection();

            var sql = @"
                SELECT m.Id, m.CreatorId, m.Title, m.Description, m.Type, m.Genre, m.ReleaseYear, m.AgeRestriction, m.AverageRating
                FROM MediaEntries m
                JOIN Ratings r ON m.Id = r.MediaEntryId
                WHERE r.UserId = @Uid AND r.Score >= 4";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapReaderToMediaEntry(reader));
            }
            return list;
        }
        public async Task<List<int>> GetRatedMediaIdsAsync(int userId)
        {
            var ids = new List<int>();
            using var conn = GetConnection();

            var sql = "SELECT MediaEntryId FROM Ratings WHERE UserId = @Uid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt32(0));
            }
            return ids;
        }
        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            using var conn = GetConnection();

            // Basis-Daten & Einfache Stats holen
            // Wir nutzen Sub-Selects für Count und Avg direkt im Select
            var sqlBasic = @"
                SELECT 
                    Username, 
                    Email,
                    (SELECT COUNT(*) FROM Ratings WHERE UserId = @Uid) as TotalRatings,
                    (SELECT COALESCE(AVG(Score), 0) FROM Ratings WHERE UserId = @Uid) as AvgScore
                FROM Users 
                WHERE Id = @Uid";

            using var cmd = new NpgsqlCommand(sqlBasic, conn);
            cmd.Parameters.AddWithValue("@Uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync()) return null; // User nicht gefunden

            var profile = new UserProfileDto
            {
                Username = reader.GetString(0),
                Email = reader.GetString(1),
                TotalRatings = reader.GetInt32(2), // Subquery Result 1
                AverageScoreGiven = reader.GetDouble(3), // Subquery Result 2
                FavoriteGenre = null // Füllen wir gleich
            };

            // Reader schließen, damit wir eine neue Query senden können
            await reader.CloseAsync();

            // 2. Lieblings-Genre ermitteln
            // Logik: Join Ratings mit Media, Gruppiere nach Genre, Sortiere nach Anzahl
            if (profile.TotalRatings > 0)
            {
                var sqlGenre = @"
                    SELECT m.Genre
                    FROM Ratings r
                    JOIN MediaEntries m ON r.MediaEntryId = m.Id
                    WHERE r.UserId = @Uid
                    GROUP BY m.Genre
                    ORDER BY COUNT(*) DESC
                    LIMIT 1";

                using var cmdGenre = new NpgsqlCommand(sqlGenre, conn);
                cmdGenre.Parameters.AddWithValue("@Uid", userId);

                // ExecuteScalar gibt das erste Feld der ersten Zeile zurück (oder null)
                var result = await cmdGenre.ExecuteScalarAsync();
                if (result != null)
                {
                    profile.FavoriteGenre = result.ToString();
                }
            }

            return profile;
        }
        public async Task UpdateUserSensitiveDataAsync(int userId, string newUsername, string newEmail, string? newPasswordHash)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            // Wir bauen das SQL Statement dynamisch auf
            var sql = "UPDATE Users SET Username = @Username, Email = @Email";

            // Parameter hinzufügen
            cmd.Parameters.AddWithValue("@Username", newUsername);
            cmd.Parameters.AddWithValue("@Email", newEmail);
            cmd.Parameters.AddWithValue("@Id", userId);

            // Nur wenn ein neues Passwort da ist, updaten wir auch den Hash
            if (!string.IsNullOrEmpty(newPasswordHash))
            {
                sql += ", PasswordHash = @Hash";
                cmd.Parameters.AddWithValue("@Hash", newPasswordHash);
            }

            sql += " WHERE Id = @Id";

            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<List<Rating>> GetPendingRatingsForMediaAsync(int mediaId)
        {
            var list = new List<Rating>();
            using var conn = GetConnection();

            var sql = @"
                SELECT Id, MediaEntryId, UserId, Score, Comment, IsConfirmed, Timestamp 
                FROM Ratings 
                WHERE MediaEntryId = @Mid AND IsConfirmed = FALSE AND Comment IS NOT NULL";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Mid", mediaId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Rating
                {
                    Id = reader.GetInt32(0),
                    MediaEntryId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    Score = reader.GetInt32(3),
                    Comment = reader.GetString(4), // Hier sicher String, da IS NOT NULL filtert
                    IsConfirmed = reader.GetBoolean(5),
                    Timestamp = reader.GetDateTime(6)
                });
            }
            return list;
        }
        public async Task<bool> IsMediaCreatorAsync(int mediaId, int userId)
        {
            using var conn = GetConnection();
            var sql = "SELECT COUNT(1) FROM MediaEntries WHERE Id = @Mid AND CreatorId = @Uid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Mid", mediaId);
            cmd.Parameters.AddWithValue("@Uid", userId);

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }
        public async Task<int?> GetMediaIdByRatingIdAsync(int ratingId)
        {
            using var conn = GetConnection();
            var sql = "SELECT MediaEntryId FROM Ratings WHERE Id = @Rid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Rid", ratingId);

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : Convert.ToInt32(result);
        }
        public async Task ApproveRatingAsync(int ratingId)
        {
            using var conn = GetConnection();
            var sql = "UPDATE Ratings SET IsConfirmed = TRUE WHERE Id = @Rid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Rid", ratingId);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task DeleteRatingAsync(int ratingId)
        {
            using var conn = GetConnection();
            
            var sql = "DELETE FROM Ratings WHERE Id = @Rid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Rid", ratingId);

            await cmd.ExecuteNonQueryAsync();
            // Durchschnitt neu berechnen
            var mediaId = await GetMediaIdByRatingIdAsync(ratingId);
            if (mediaId.HasValue)
            {
                await RecalculateAverageRatingAsync(mediaId.Value);
            }
        }
        public async Task<Rating?> GetRatingByIdAsync(int id)
        {
            using var conn = GetConnection();

            var sql = "SELECT Id, MediaEntryId, UserId, Score, Comment, IsConfirmed, Timestamp FROM Ratings WHERE Id = @Id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Rating
                {
                    Id = reader.GetInt32(0),
                    MediaEntryId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    Score = reader.GetInt32(3),
                    Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsConfirmed = reader.GetBoolean(5),
                    Timestamp = reader.GetDateTime(6)
                };
            }
            return null;
        }
        public async Task DeleteMediaAsync(int id)
        {
            using var conn = GetConnection();
            var sql = "DELETE FROM MediaEntries WHERE Id = @Id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task UpdateMediaAsync(MediaEntry media)
        {
            using var conn = GetConnection();
            var sql = @"
                UPDATE MediaEntries 
                SET Title = @Title, 
                    Description = @Desc, 
                    Type = @Type, 
                    Genre = @Genre, 
                    ReleaseYear = @Year, 
                    AgeRestriction = @Age
                WHERE Id = @Id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", media.Id);
            cmd.Parameters.AddWithValue("@Title", media.Title);
            cmd.Parameters.AddWithValue("@Desc", media.Description);
            cmd.Parameters.AddWithValue("@Type", (int)media.Type);
            cmd.Parameters.AddWithValue("@Genre", media.Genre);
            cmd.Parameters.AddWithValue("@Year", media.ReleaseYear);
            cmd.Parameters.AddWithValue("@Age", media.AgeRestriction);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<List<Rating>> GetRatingsByUserAsync(int userId)
        {
            var list = new List<Rating>();
            using var conn = GetConnection();
            var sql = "SELECT * FROM Ratings WHERE UserId = @Uid ORDER BY Timestamp DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Uid", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new Rating
                {
                    Id = reader.GetInt32(0),
                    MediaEntryId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    Score = reader.GetInt32(3),
                    Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsConfirmed = reader.GetBoolean(5),
                    Timestamp = reader.GetDateTime(6)
                });
            }
            return list;
        }
        public async Task UpdateRatingAsync(int ratingId, int score, string? comment)
        {
            using var conn = GetConnection();
            
            var isConfirmed = string.IsNullOrEmpty(comment);

            var sql = @"
                UPDATE Ratings 
                SET Score = @Score, 
                    Comment = @Comment, 
                    IsConfirmed = @IsConfirmed,
                    Timestamp = CURRENT_TIMESTAMP
                WHERE Id = @Id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", ratingId);
            cmd.Parameters.AddWithValue("@Score", score);
            cmd.Parameters.AddWithValue("@IsConfirmed", isConfirmed);

            if (comment == null)
                cmd.Parameters.AddWithValue("@Comment", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("@Comment", comment);


            await cmd.ExecuteNonQueryAsync();
            // Durchschnitt neu berechnen
            var mediaId = await GetMediaIdByRatingIdAsync(ratingId);
            if (mediaId.HasValue)
            {
                await RecalculateAverageRatingAsync(mediaId.Value);
            }
        }
        private async Task RecalculateAverageRatingAsync(int mediaId)
        {
            using var conn = GetConnection();

            // 1. Durchschnitt berechnen (SQL macht das effizient)
            // COALESCE(AVG(...), 0) sorgt dafür, dass 0 rauskommt, wenn keine Ratings mehr da sind.
            var sqlCalc = "SELECT COALESCE(AVG(Score), 0) FROM Ratings WHERE MediaEntryId = @Mid";

            using var cmdCalc = new NpgsqlCommand(sqlCalc, conn);
            cmdCalc.Parameters.AddWithValue("@Mid", mediaId);

            var newAverage = Convert.ToDouble(await cmdCalc.ExecuteScalarAsync());

            // 2. Wert in MediaEntries Tabelle updaten
            var sqlUpdate = "UPDATE MediaEntries SET AverageRating = @Avg WHERE Id = @Mid";

            using var cmdUpdate = new NpgsqlCommand(sqlUpdate, conn);
            cmdUpdate.Parameters.AddWithValue("@Avg", newAverage);
            cmdUpdate.Parameters.AddWithValue("@Mid", mediaId);

            await cmdUpdate.ExecuteNonQueryAsync();
        }
        public Task<User?> GetUserByIdAsync(int id) => throw new NotImplementedException();
        public Task<MediaEntry?> GetMediaByIdAsync(int id) => throw new NotImplementedException();
    }
}

