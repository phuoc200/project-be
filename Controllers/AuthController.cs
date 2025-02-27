using DoAnKy3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using DoAnKy3.Services;

namespace DoAnKy3.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ClinicManagementSystemContext _context;
        private readonly IConfiguration _config;

        public AuthController(ClinicManagementSystemContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // 1️⃣ Đăng ký tài khoản
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { message = "Username, email, and password are required" });
            }

            // Kiểm tra email hợp lệ
            if (!new EmailAddressAttribute().IsValid(model.Email))
            {
                return BadRequest(new { message = "Invalid email format" });
            }

            // Kiểm tra username/email đã tồn tại
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email || u.Username == model.Username);
            if (existingUser != null)
            {
                return BadRequest(new { message = "Username or email already exists" });
            }

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password), // Băm mật khẩu
                RoleId = 2, // Gán mặc định là 2 (User)
                CreatedAt = DateTime.UtcNow,
                Gender = model.Gender
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully" });
        }

        // 2️⃣ Đăng nhập và lấy JWT Token
        // Trong phương thức Login của AuthController
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var jwtService = new JwtService("this_is_a_very_long_and_secure_key_32_characters");
            var token = jwtService.GenerateToken(user);

            // Thêm log để debug
            Console.WriteLine($"Generated token for user {user.UserId}: {token}");

            return Ok(new
            {
                message = "Login successful!",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.RoleId
                },
                token
            });
        }

        [Authorize] // Yêu cầu user phải đăng nhập mới gọi được API này
        [HttpGet("userinfo")]
        public async Task<IActionResult> GetCurrentUserInfo()
        {
            var userId = GetUserIdFromToken(); // Lấy userId từ JWT token
            if (userId == null)
            {
                return Unauthorized(new { message = "Unauthorized" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            return Ok(new
            {
                user.UserId,
                user.Username,
                user.Email,
                user.FullName,
                user.DateOfBirth,
                user.Phone,
                user.Address,
                user.Gender,
                user.RoleId
            });
        }

        // 4️⃣ Quên mật khẩu - Gửi OTP qua Email
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return BadRequest("Email không tồn tại.");

            user.ResetToken = GenerateRandomToken();
            await _context.SaveChangesAsync();

            SendEmail(user.Email, "Mã đặt lại mật khẩu", $"Mã OTP của bạn là: {user.ResetToken}");

            return Ok(new { message = "OTP đã được gửi qua email!" });
        }

        // 5️⃣ Đặt lại mật khẩu
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == model.Token);
            if (user == null) return BadRequest("Mã OTP không hợp lệ.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.ResetToken = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Mật khẩu đã được cập nhật!" });
        }

        [HttpGet("verify-token")]
        public IActionResult VerifyToken()
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized(new { message = "Invalid or expired token." });
            }

            return Ok(new { message = "Token is valid", userId });
        }

        //cập nhật thông tin cá nhân
        [HttpPut("update-profile/{userId}")]
        public async Task<IActionResult> UpdateProfile(int userId, [FromBody] UpdateProfileModel model)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User không tồn tại." });
            }

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;
            user.DateOfBirth = model.DateOfBirth;
            user.Gender = model.Gender;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thông tin thành công!" });
        }

        private int? GetUserIdFromToken()
        {
            try
            {
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer ")) return null;

                var token = authHeader.Substring("Bearer ".Length);
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes("this_is_a_very_long_and_secure_key_32_characters"); // Giống với key trong JwtService

                var validations = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validations, out var validatedToken);
                var identity = principal.Identity as ClaimsIdentity;
                var claim = identity?.FindFirst(ClaimTypes.NameIdentifier);

                return claim != null ? int.Parse(claim.Value) : (int?)null;
            }
            catch
            {
                return null; // Trả về null nếu token không hợp lệ
            }
        }


        private string GenerateRandomToken()
        {
            using var rng = new RNGCryptoServiceProvider();
            byte[] randomBytes = new byte[6];
            rng.GetBytes(randomBytes);
            return BitConverter.ToString(randomBytes).Replace("-", "").Substring(0, 6);
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            using var smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("thandongdatviet357@gmail.com", "egip gjpu vblm lfyx"),
                EnableSsl = true
            };

            smtp.Send("thandongdatviet357@gmail.com", toEmail, subject, body);
        }
    }

    public class ResetPasswordModel
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }
    public class RegisterModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Gender { get; set; } = "Male";
    }

    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }
    public class UpdateProfileModel
    {
        public string? FullName { get; set; } // Không bắt buộc

        [Phone]
        public string? Phone { get; set; } // Không bắt buộc

        public string? Address { get; set; } // Không bắt buộc

        public DateTime? DateOfBirth { get; set; } // Không bắt buộc

        public string? Gender { get; set; } // Không bắt buộc, "Male", "Female", "Other"
    }
}
