using NUnit.Framework;
using MediaServer.Services;
using System.Threading.Tasks;

namespace MediaServer.Tests
{
    [TestFixture]
    public class AuthServiceTests
    {
        private AuthService _authService;

        [SetUp]
        public void Setup()
        {
            _authService = new AuthService();
        }

        // --- PASSWORD HASHING ---

        [Test]
        public void HashPassword_GivenString_ReturnsDifferentString()
        {
            string password = "secretPassword";
            string hash = _authService.HashPassword(password);

            Assert.That(hash, Is.Not.EqualTo(password));
            Assert.That(hash, Is.Not.Empty);
        }

        [Test]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            string password = "password123";
            string hash = _authService.HashPassword(password);

            bool result = _authService.VerifyPassword(password, hash);

            Assert.That(result, Is.True);
        }

        [Test]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            string password = "password123";
            string hash = _authService.HashPassword(password);

            bool result = _authService.VerifyPassword("WRONG_PASSWORD", hash);

            Assert.That(result, Is.False);
        }

        // --- TOKEN HANDLING ---

        [Test]
        public async Task GenerateToken_GivenUserId_ReturnsTokenString()
        {
            int userId = 1;
            string token = await _authService.GenerateTokenAsync(userId);

            Assert.That(token, Is.Not.Null);
            Assert.That(token, Is.Not.Empty);
        }

        [Test]
        public async Task ValidateToken_GivenValidToken_ReturnsUserId()
        {
            int userId = 5;
            string token = await _authService.GenerateTokenAsync(userId);

            int? resultId = await _authService.ValidateTokenAsync(token);

            Assert.That(resultId, Is.EqualTo(userId));
        }

        [Test]
        public async Task ValidateToken_GivenInvalidToken_ReturnsNull()
        {
            int? result = await _authService.ValidateTokenAsync("INVALID_TOKEN_STRING");

            Assert.That(result, Is.Null);
        }
    }
}