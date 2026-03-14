using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using web_jobs.Models;
using web_jobs.Dtos;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployerAuthApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmployerRepository _employerRepository;
        public EmployerAuthApiController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IConfiguration configuration,
            RoleManager<IdentityRole> roleManager,
            IEmployerRepository employerRepository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _roleManager = roleManager;
            _employerRepository = employerRepository;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] EmployerRegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Đăng ký thất bại", errors = result.Errors });
            }
            var roleToAssign = string.IsNullOrEmpty(model.Role) ? "Employer" : model.Role;
            var roleExists = await _roleManager.RoleExistsAsync(roleToAssign);
            if (!roleExists)
            {
                return BadRequest(new { message = $"Role '{roleToAssign}' không tồn tại. Vui lòng tạo role trước." });
            }
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
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Email không đúng ." });
            }
            var userPassword = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!userPassword.Succeeded)
            {
                return Unauthorized(new { message = "Mật khẩu không đúng." });
            }
            //Lấy danh sách role của user
            var roles = await _userManager.GetRolesAsync(user);
            //Chọn role đầu tiên hoặc mặc định
            var userRoles = roles.FirstOrDefault() ?? "Employer";
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Role, userRoles),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //  Tạo token
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (Ok(new
            {
                token = tokenString,
                expiration = token.ValidTo,
                userId = user.Id,
                email = user.Email,
                role = userRoles
            }));
        }
        [HttpGet("GetCompanyUserid")]
        public async Task<IActionResult> GetCompanyUserId(Guid userId)
        {
        
            var company = await _employerRepository.GetByUserIdAsync(userId);
            if(company == null)
            {
                return NotFound();
            }
            return Ok(company);
        } 
    }
}
