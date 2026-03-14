using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using web_jobs.Models;
using web_jobs.Dtos;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthApiController(
       UserManager<AppUser> userManager,
       SignInManager<AppUser> signInManager,
       IConfiguration configuration,
       RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _roleManager = roleManager;
        }
        // ✅ Đăng ký
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Tạo user mới
            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            // Tạo user trong DB
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Đăng ký thất bại", errors = result.Errors });

            // Xác định role cần gán, mặc định "Candidate"
            //model.Role → role mà client có thể gửi lên khi đăng ký
            var roleToAssign = string.IsNullOrEmpty(model.Role) ? "Candidate" : model.Role;

            // Kiểm tra role tồn tại trong DB
            var roleExists = await _roleManager.RoleExistsAsync(roleToAssign);
            if (!roleExists)
            {
                return BadRequest(new { message = $"Role '{roleToAssign}' không tồn tại. Vui lòng tạo role trước." });
            }

            // Gán role cho user
            var roleResult = await _userManager.AddToRoleAsync(user, roleToAssign);
            if (!roleResult.Succeeded)
            {
                return BadRequest(new { message = "Gán role thất bại", errors = roleResult.Errors });
            }

            return Ok(new
            {
                message = "Đăng ký thành công",
                userId = user.Id,
                email = user.Email,
                role = roleToAssign
            });
        }
        // ✅ Đăng nhập (trả JWT)
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu" });

            // Lấy role user
            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? "Candidate";

            // 🔑 Tạo claims cho JWT
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Role, userRole),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // 🔒 Khóa ký token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 📅 Tạo token
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Đăng nhập thành công",
                token = tokenString,
                expiration = token.ValidTo,
                userId = user.Id,
                email = user.Email,
                role = userRole
            });
        }
    }
}
