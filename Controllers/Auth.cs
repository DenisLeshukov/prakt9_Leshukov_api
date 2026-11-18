using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using prakt8_leshukov.Models;
using prakt8_leshukov.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace prakt8_leshukov.Controllers
{
    [Route("api/v1/auth")]
    [ApiController]
    public class Auth : ControllerBase
    {
        private static readonly object _lockObject = new object( );
        private readonly List<User> _users;
        private Prakt8Context _db = DBService.Instance.Context;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IConfiguration _cofiguration;
       
        public Auth(
            List<User> users, IPasswordHasher<User> hasher, IConfiguration cofiguration)
        {
            _users = users;
            _hasher = hasher;
            _cofiguration = cofiguration;

            if(_users.Count == 0)
            {
                _users.AddRange(_db.Users.ToList());
            }
        }
        private string GenerateRefreshToken (User user)
        {
            
            var key = RandomNumberGenerator.GetBytes(32);
            var token_hash = SHA256.Create( ).ComputeHash(key);
            var refreshToken = new RefreshToken
            {
                TokenHash = Convert.ToHexString(token_hash),
                UserId = user.Id,
                ExpiresAt = DateTime.Now.AddDays(7)

            };

            _db.RefreshTokens.Add(refreshToken);
            _db.SaveChanges( );
            return Convert.ToHexString(key);
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())

            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cofiguration["Jwt:Key"]!));

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer:"prakt8_leshukov",
                audience: "prakt8_leshukovClient",
                claims:claims,
                expires: DateTime.UtcNow.AddSeconds(60),
                signingCredentials:credentials
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

       public record RefreshTok (string refresh_token);
        [HttpPost("refresh")]
        public IActionResult Refresh (RefreshTok rt)
        {
            RefreshToken refreshToken;
            try
            {
                var keyBytes = Convert.FromHexString(rt.refresh_token);
                var tokenHash = Convert.ToHexString(SHA256.HashData(keyBytes));
                lock (_lockObject)
                {
                    refreshToken = _db.RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
                    if (refreshToken == null)
                    {
                        return Unauthorized( );
                    }
                    if (refreshToken.ExpiresAt < DateTime.Now)
                    {
                        return Unauthorized( );
                    }
                    _db.RefreshTokens.Remove(refreshToken);
                    _db.SaveChanges( );
                }
            }
            catch (Exception ex)
            {
                return Unauthorized( );
            }
           
            
            return Ok(new
            {
                access_token = GenerateJwtToken(refreshToken.User),
                refresh_token = GenerateRefreshToken(refreshToken.User)
            });
        }
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout (RefreshTok rt)
        {
            var keyBytes = Convert.FromHexString(rt.refresh_token);
            var tokenHash = Convert.ToHexString(SHA256.HashData(keyBytes));
            var refreshToken = _db.RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);

            var user = User.Identity?.Name;
            if (refreshToken == null)
            {
                return Unauthorized();
            }
            if(user == null)
            {
                return Unauthorized( );
            }
            if (refreshToken.User.Login != user)
            {
                return Forbid();
            }
            if (refreshToken.ExpiresAt < DateTime.Now)
            {
                return Unauthorized();
            }
            _db.RefreshTokens.Remove(refreshToken);

            RevokedToken revoketToken = new RevokedToken();
            DateTime expire;
            revoketToken.Jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            try
            {
                expire = Convert.ToDateTime(User.FindFirstValue(JwtRegisteredClaimNames.Exp));

            }
            catch (Exception ex) {
                expire = DateTime.Now.AddSeconds(60);
            }
            revoketToken.ExpiresAt = expire;
            _db.RevokedTokens.Add(revoketToken);
            _db.SaveChanges();
            return NoContent();

        }

        public record AuthRequest (string Login, string Password);
        [HttpPost("register")]
        public IActionResult Register(AuthRequest request)
        {
            if (_users.Any(u => u.Login == request.Login))
                return Conflict("Логин уже существует");

            var user = new User { Login = request.Login };
            if(user.Login == "admin123")
            {
                user.Role = "Admin";
            }
            else
            {
                user.Role = "User";

            }
                
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            _users.Add(user);
            _db.Users.Add(user);
            _db.SaveChanges();
            return Ok("Пользователь зарегестрирован");
        }
        [HttpPost("login")]
        public IActionResult Login(AuthRequest request)
        {
            var user = _users.Find(u => u.Login == request.Login);
            if(user == null)
            {
                return Conflict("Неверная пара логин/пароль");
            }
            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password
                );
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Неверная пара логин/пароль");
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken(user);
            return Ok(new
            {
                access_token = token,
                refresh_token = refreshToken
            });
        }
       

    }
}
