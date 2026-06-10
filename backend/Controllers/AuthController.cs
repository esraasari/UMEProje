using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace UMEProje.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Bu satır sayesinde adres "api/auth" olur
    public class AuthController : ControllerBase
    {
        [HttpPost("login")] // Bu satır sayesinde tam adres "api/auth/login" olur
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // React'ten gelen bilgileri kontrol ediyoruz
            if (request.Username == "admin" && request.Password == "1234")
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                // Program.cs içindeki şifrenin BİREBİR aynısı olmak ZORUNDA
                var key = Encoding.ASCII.GetBytes("TubitakUmeSuperSecretKey1234567890!"); 
                
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, request.Username),
                        new Claim(ClaimTypes.Role, "Engineer") // UME Mühendisi Rolü
                    }),
                    Expires = DateTime.UtcNow.AddHours(1),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                
                var token = tokenHandler.CreateToken(tokenDescriptor);
                return Ok(new { token = tokenHandler.WriteToken(token) });
            }

            return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı" });
        }
    }

    // React'in gönderdiği JSON verisini (admin, 1234) C#'ta yakalayan kalıp
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}