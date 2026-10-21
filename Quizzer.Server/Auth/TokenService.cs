using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quizzer.DataModels.Models.Auth;
using Quizzer.Server.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.Server.Auth
{
    /// <summary>
    /// Stellt Ausweise aus. Der Zugangsausweis ist ein kurzlebiges JWT (HS256), der
    /// Erneuerungsschluessel 32 Zufallsbyte, von denen nur der SHA-256 gespeichert wird.
    /// </summary>
    public sealed class TokenService
    {
        private readonly JwtOptions options;

        public TokenService(IOptions<ServerOptions> options)
        {
            this.options = options.Value.Jwt;
            SigningKey = new SymmetricSecurityKey(LoadKey(this.options));
        }

        public SymmetricSecurityKey SigningKey { get; }

        public JwtOptions Options => options;

        public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(Account account, string playerName, DateTimeOffset now)
        {
            var expires = now + options.AccessTokenLifetime;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(JwtRegisteredClaimNames.Name, account.UserName),
                new(CurrentAccount.PlayerIdClaim, account.PlayerId.ToString()),
                new(CurrentAccount.RoleClaim, account.Role.ToString()),
                new("player_name", playerName),
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = options.Issuer,
                Audience = options.Audience,
                Subject = new ClaimsIdentity(claims),
                NotBefore = now.UtcDateTime,
                IssuedAt = now.UtcDateTime,
                Expires = expires.UtcDateTime,
                SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            };

            // Ohne MapInboundClaims/Outbound-Zuordnung: die Anspruchsnamen stehen im Ausweis genau so,
            // wie sie hier heissen - sub, name, role, player_id.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            handler.OutboundClaimTypeMap.Clear();
            return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
        }

        public static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        public static string HashRefreshToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        public TokenValidationParameters ValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = CurrentAccount.RoleClaim,
        };

        private static byte[] LoadKey(JwtOptions options)
        {
            var text = options.SigningKey;

            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(options.SigningKeyFile))
                text = File.ReadAllText(options.SigningKeyFile).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException(
                    "Kein Signierschluessel. Setze Quizzer__Jwt__SigningKey (Base64, mindestens 32 Byte) "
                    + "oder Quizzer__Jwt__SigningKeyFile.");
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(text);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Der Signierschluessel ist kein Base64.", ex);
            }

            if (key.Length < 32)
                throw new InvalidOperationException("Der Signierschluessel muss mindestens 32 Byte lang sein.");

            return key;
        }
    }
}
