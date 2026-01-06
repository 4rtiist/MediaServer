using NUnit.Framework;
using Moq;
using MediaServer.Services;
using MediaServer.Data;
using MediaServer.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace MediaServer.Tests
{
    [TestFixture]
    public class RecommendationServiceTests
    {
        private Mock<IMediaRepository> _mockRepo;
        private RecommendationService _service;

        [SetUp]
        public void Setup()
        {
            // Wir erstellen einen "Fake" des Repositories
            _mockRepo = new Mock<IMediaRepository>();
            _service = new RecommendationService(_mockRepo.Object);
        }

        [Test]
        public async Task GetRecommendations_UserHasNoHistory_ReturnsGeneralTopRated()
        {
            // Arrange: User hat keine Likes
            int userId = 1;
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int>());
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(new List<MediaEntry>());

            // Repository soll bei allgemeiner Suche 2 Filme zurückgeben
            var globalTop = new List<MediaEntry>
            {
                new MediaEntry { Id = 10, Title = "Top Movie 1", Genre = "Action", Description="", Type=0, ReleaseYear=2020, AgeRestriction=0 },
                new MediaEntry { Id = 11, Title = "Top Movie 2", Genre = "Drama", Description="", Type=0, ReleaseYear=2020, AgeRestriction=0 }
            };

            // Wenn SearchMediaAsync aufgerufen wird (mit null Parametern für Global Search)
            _mockRepo.Setup(r => r.SearchMediaAsync(null, null, null, It.IsAny<int>()))
                     .ReturnsAsync(globalTop);

            // Act
            var result = await _service.GetRecommendationsForUserAsync(userId);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Title, Is.EqualTo("Top Movie 1"));
        }

        [Test]
        public async Task GetRecommendations_UserLikesSciFi_ReturnsSciFiCandidates()
        {
            int userId = 1;

            // 1. User History
            var likedMovies = new List<MediaEntry>
            {
                new MediaEntry { Id = 1, Title = "Matrix", Genre = "SciFi", Description="", Type=0, ReleaseYear=1999, AgeRestriction=12 }
            };
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(likedMovies);
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int> { 1 });

            // 2. Kandidaten
            var candidates = new List<MediaEntry>
            {
                new MediaEntry { Id = 2, Title = "Inception", Genre = "SciFi", Description="", Type=0, ReleaseYear=2010, AgeRestriction=12 }
            };

            // --- SETUP 1: Genre Suche ---
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                "SciFi",
                It.IsAny<MediaEntryType?>(),
                3
            )).ReturnsAsync(candidates);

            // --- SETUP 2: Fallback Suche (NEU!) ---
            // Verhindert den Crash im if(count < 5) Block
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                It.Is<string?>(s => s == null), // Wenn Genre null ist
                It.IsAny<MediaEntryType?>(),
                It.IsAny<int?>()
            )).ReturnsAsync(new List<MediaEntry>());

            // Act
            var result = await _service.GetRecommendationsForUserAsync(userId);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Title, Is.EqualTo("Inception"));
        }

        [Test]
        public async Task GetRecommendations_FiltersOut_AlreadyRatedMedia()
        {
            int userId = 1;

            // 1. User mag "Matrix" (SciFi)
            var likedMovies = new List<MediaEntry>
            {
                new MediaEntry { Id = 1, Title = "Matrix", Genre = "SciFi", Description="", Type=0, ReleaseYear=1999, AgeRestriction=12 }
            };
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(likedMovies);

            // 2. User hat "Inception" (ID 2) auch schon bewertet (Blacklist)
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int> { 1, 2 });

            // 3. Kandidaten für SciFi-Suche
            var candidates = new List<MediaEntry>
            {
                new MediaEntry { Id = 2, Title = "Inception", Genre = "SciFi", Description="", Type=0, ReleaseYear=2010, AgeRestriction=12 },
                new MediaEntry { Id = 3, Title = "Interstellar", Genre = "SciFi", Description="", Type=0, ReleaseYear=2014, AgeRestriction=12 }
            };

            // --- SETUP 1: Der Genre-Aufruf ("SciFi") ---
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                "SciFi",              // Spezifisch für SciFi
                It.IsAny<MediaEntryType?>(),
                3
            )).ReturnsAsync(candidates);

            // --- SETUP 2: Der Fallback-Aufruf (Genre ist null) ---
            // WICHTIG: Das hat gefehlt! Der Service ruft das auf, weil die Liste < 5 ist.
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                It.Is<string?>(s => s == null), // Wenn Genre null ist
                It.IsAny<MediaEntryType?>(),
                It.IsAny<int?>()
            )).ReturnsAsync(new List<MediaEntry>()); // Gib leere Liste zurück, damit nichts durcheinander kommt

            // Act
            var result = await _service.GetRecommendationsForUserAsync(userId);

            // Assert: Inception (ID 2) darf NICHT dabei sein, Interstellar (ID 3) schon
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Title, Is.EqualTo("Interstellar"));
        }

        [Test]
        public async Task GetRecommendations_LimitsResultTo5()
        {
            int userId = 1;
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(new List<MediaEntry>());
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int>());

            // Wir simulieren 10 verfügbare Top-Filme
            var lotsOfMovies = Enumerable.Range(1, 10)
                .Select(i => new MediaEntry { Id = i, Title = $"Movie {i}", Genre = "Action", Description = "", Type = 0, ReleaseYear = 2000, AgeRestriction = 0 })
                .ToList();

            _mockRepo.Setup(r => r.SearchMediaAsync(null, null, null, It.IsAny<int>())).ReturnsAsync(lotsOfMovies);

            var result = await _service.GetRecommendationsForUserAsync(userId);

            Assert.That(result.Count, Is.EqualTo(5)); // Sollte bei 5 abgeschnitten werden
        }

        [Test]
        public async Task GetRecommendations_WithMixedGenreCounts_PicksTopGenre()
        {
            int userId = 1;

            // User mag 2x Horror, 1x Comedy
            var history = new List<MediaEntry>
            {
                new MediaEntry { Id=1, Genre="Horror", Title="Saw", Description="", Type=0, ReleaseYear=2000, AgeRestriction=18 },
                new MediaEntry { Id=2, Genre="Horror", Title="It", Description="", Type=0, ReleaseYear=2000, AgeRestriction=18 },
                new MediaEntry { Id=3, Genre="Comedy", Title="Mask", Description="", Type=0, ReleaseYear=2000, AgeRestriction=6 }
            };

            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(history);
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int> { 1, 2, 3 });

            // Wir erwarten, dass er nach "Horror" sucht
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                "Horror",
                It.IsAny<MediaEntryType?>(),
                3
            )).ReturnsAsync(new List<MediaEntry> { new MediaEntry { Id = 4, Title = "Alien", Genre = "Horror", Description = "", Type = 0, ReleaseYear = 2000, AgeRestriction = 18 } });

            // --- SETUP 2: Fallback Suche (NEU!) ---
            _mockRepo.Setup(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                It.Is<string?>(s => s == null),
                It.IsAny<MediaEntryType?>(),
                It.IsAny<int?>()
            )).ReturnsAsync(new List<MediaEntry>());

            var result = await _service.GetRecommendationsForUserAsync(userId);

            Assert.That(result[0].Title, Is.EqualTo("Alien"));

            // Verifizieren, dass SearchMediaAsync wirklich mit "Horror" aufgerufen wurde
            _mockRepo.Verify(r => r.SearchMediaAsync(
                It.IsAny<string?>(),
                "Horror",
                It.IsAny<MediaEntryType?>(),
                3
            ), Times.Once);
        }

        [Test]
        public async Task GetRecommendations_FallsBackToGlobal_IfGenreSearchEmpty()
        {
            int userId = 1;
            // User mag SciFi
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(new List<MediaEntry>
            {
                new MediaEntry { Id=1, Genre="SciFi", Title="Matrix", Description="", Type=0, ReleaseYear=2000, AgeRestriction=12 }
            });
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int> { 1 });

            // ABER: Es gibt keine weiteren SciFi Filme in der DB (leere Liste)
            _mockRepo.Setup(r => r.SearchMediaAsync(null, "SciFi", null, 3)).ReturnsAsync(new List<MediaEntry>());

            // DAFÜR: Es gibt Top-Filme allgemein
            _mockRepo.Setup(r => r.SearchMediaAsync(null, null, null, 3)).ReturnsAsync(new List<MediaEntry>
            {
                new MediaEntry { Id=2, Genre="Drama", Title="Titanic", Description="", Type=0, ReleaseYear=1997, AgeRestriction=12 }
            });

            var result = await _service.GetRecommendationsForUserAsync(userId);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Title, Is.EqualTo("Titanic")); // Fallback hat gegriffen
        }

        [Test]
        public async Task GetRecommendations_HandlesDatabaseErrorGracefully()
        {
            // Testen, ob Exception durchgereicht wird
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(It.IsAny<int>())).ThrowsAsync(new Exception("DB Down"));

            // NUnit 4 Syntax für Async Exceptions
            Assert.That(async () => await _service.GetRecommendationsForUserAsync(1), Throws.Exception);
        }

        [Test]
        public async Task GetRecommendations_UltimaRatioFallback_IfMinRating3YieldsNothing()
        {
            // Wenn selbst Search mit minRating 3 nichts liefert, soll er minRating 0 (alles) suchen
            int userId = 1;
            _mockRepo.Setup(r => r.GetLikedMediaByRatingsAsync(userId)).ReturnsAsync(new List<MediaEntry>());
            _mockRepo.Setup(r => r.GetRatedMediaIdsAsync(userId)).ReturnsAsync(new List<int>());

            // Genre search empty
            _mockRepo.Setup(r => r.SearchMediaAsync(null, null, null, 3)).ReturnsAsync(new List<MediaEntry>());

            // Ultimate fallback (minRating 0)
            _mockRepo.Setup(r => r.SearchMediaAsync(null, null, null, 0)).ReturnsAsync(new List<MediaEntry>
            {
                new MediaEntry { Id=99, Title="Bad Movie", AverageRating=1.0, Description="", Type=0, ReleaseYear=2020, AgeRestriction=0, Genre="Trash", CreatorId=0 }
            });

            var result = await _service.GetRecommendationsForUserAsync(userId);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Title, Is.EqualTo("Bad Movie"));
        }
    }
}