using NUnit.Framework;
using MediaServer.Models;
using MediaServer.DataObjects;
using System;

namespace MediaServer.Tests
{
    [TestFixture]
    public class ModelTests
    {
        [Test]
        public void Rating_ByDefault_IsUnconfirmed()
        {
            // Requirement: "requires confirmation... comments are not publicly visible"
            // Wir prüfen, ob der Default-Wert im Model sicher (False) ist.
            var rating = new Rating { MediaEntryId = 1, UserId = 1, Score = 5 };

            // NUnit 4 Syntax
            Assert.That(rating.IsConfirmed, Is.False);
        }

        [Test]
        public void Rating_Timestamp_DefaultsToNow()
        {
            var rating = new Rating { MediaEntryId = 1, UserId = 1, Score = 5 };

            // Check ob Timestamp gesetzt wurde (nicht default 0001-01-01)
            // Prüft, ob der Zeitstempel "jetzt" ist (mit 1 Sekunde Toleranz)
            Assert.That(rating.Timestamp, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void MediaEntry_AverageRating_DefaultsToZero()
        {
            var media = new MediaEntry
            {
                Title = "Test",
                Description = "Desc",
                Type = MediaEntryType.Movie,
                Genre = "Action",
                ReleaseYear = 2022,
                AgeRestriction = 12
            };

            Assert.That(media.AverageRating, Is.EqualTo(0));
        }

        [Test]
        public void CreateRatingDto_CanBeSerialized()
        {
            // Einfacher Test ob DTO instanziiert werden kann
            var dto = new CreateRatingDto { MediaId = 1, Score = 5, Comment = "Test" };

            Assert.That(dto.Score, Is.EqualTo(5));
            Assert.That(dto.Comment, Is.EqualTo("Test"));
        }
    }
}