using MediaServer.Data;
using MediaServer.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaServer.Services
{
    public class RecommendationService
    {
        private readonly IMediaRepository _repository;
        public RecommendationService(IMediaRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<MediaEntry>> GetRecommendationsForUserAsync(int userId)
        {
            // 1. Was hat der User schon gesehen/bewertet? (Blacklist)
            var ratedIds = await _repository.GetRatedMediaIdsAsync(userId);
            var ratedIdSet = new HashSet<int>(ratedIds);

            // 2. Lieblings-Genre ermitteln (Basis: Ratings >= 4)
            var highlyRated = await _repository.GetLikedMediaByRatingsAsync(userId);

            List<MediaEntry> recommendations = new List<MediaEntry>();

            if (highlyRated.Count > 0)
            {
                // Wir haben ein Lieblingsgenre!
                var topGenre = highlyRated
                    .GroupBy(m => m.Genre)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                // Suche nach Kandidaten in diesem Genre (minRating etwas lockerer: 3.0)
                var candidates = await _repository.SearchMediaAsync(null, topGenre, null, 3);

                recommendations = candidates
                    .Where(m => !ratedIdSet.Contains(m.Id)) // Nicht die eigenen filtern
                    .Take(5)
                    .ToList();
            }

            // 3. FALLBACK: Wenn Liste noch nicht voll ist (oder User keine Likes hat)
            // Fülle auf mit den generell besten Filmen aller Genres
            if (recommendations.Count < 5)
            {
                // Suche global nach guten Filmen (minRating 3)
                var globalTop = await _repository.SearchMediaAsync(null, null, null, 3);

                foreach (var media in globalTop)
                {
                    if (recommendations.Count >= 5) break;

                    // Nur hinzufügen, wenn noch nicht in der Liste UND noch nicht gesehen
                    if (!ratedIdSet.Contains(media.Id) && !recommendations.Any(r => r.Id == media.Id))
                    {
                        recommendations.Add(media);
                    }
                }
            }

            // 4. ULTIMATIVER FALLBACK: Wenn immer noch leer (DB hat nur schlechte Filme oder sehr wenige)
            // Hole einfach ALLES, was der User noch nicht gesehen hat
            if (recommendations.Count == 0)
            {
                var allMedia = await _repository.SearchMediaAsync(null, null, null, 0); // minRating 0 = alles
                recommendations = allMedia
                   .Where(m => !ratedIdSet.Contains(m.Id))
                   .Take(5)
                   .ToList();
            }

            return recommendations;
        }
    }
}