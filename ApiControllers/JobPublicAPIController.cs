using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class JobPublicAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmployerRepository _employerRepository;

        public JobPublicAPIController(ApplicationDbContext context, IEmployerRepository employerRepository)
        {
            _context = context;
            _employerRepository = employerRepository;
        }
        private Guid? GetCurrentUserId()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out var userId))
                {
                    return userId;
                }

            }
            return null;
        }

        [HttpGet("AllMyApplications")]
        public async Task<IActionResult> AllMyApplications()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem công việc đã ứng tuyển." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var applications = await _context.Applications
                .Include(a => a.Job)
                .ThenInclude(j => j.Employer)
                .Include(a => a.Job.JobType)
                .Where(a => a.User_ID == userId.Value)
                .OrderByDescending(a => a.ApplyDate)
                .ToListAsync();
            var listPage = applications
                .Select(a => new
                {
                    a.Job.Id,
                    a.Job.JobTitle,
                    a.Job.Locate,
                    a.Job.Salary,
                    a.Job.JobType.JobType_Name,
                    a.Job.Employer.CompanyLogo,
                    a.Job.Employer.CompanyName,
                    a.ApplyDate,
                    StatusText = a.Status.ToLower() switch
                    {
                        "approved" => "🟢 Đã duyệt",
                        "pending" => "🟡 Đang chờ",
                        "rejected" => "🔴 Bị từ chối",
                        _ => "❔ Không xác định"
                    },
                    dayleft = a.Job.ApplicationDeadline != null ? ((DateTime)a.Job.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    isExpired = a.Job.ApplicationDeadline != null ? ((DateTime)a.Job.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                }).ToList();
            return Ok(new
            {
                Applications = listPage,
            });
        }
        [HttpGet("MyApplications")]
        public async Task<IActionResult> MyApplications( int page = 1, int pageSize = 10)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem công việc đã ứng tuyển." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var applications = await _context.Applications
                .Include(a => a.Job)
                .ThenInclude(j => j.Employer)
                .Include(a => a.Job.JobType)
                .Where(a => a.User_ID == userId.Value)
                .OrderByDescending(a => a.ApplyDate)
                .ToListAsync();
            var totalApplications = applications.Count();
            var totalPages = (int)Math.Ceiling((double)totalApplications / pageSize);
            var listPage = applications.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Job.Id,
                    a.Job.JobTitle,
                    a.Job.Locate,
                    a.Job.Salary,
                    a.Job.JobType.JobType_Name,
                    a.Job.Employer.CompanyLogo,
                    a.Job.Employer.CompanyName,
                    a.ApplyDate,
                    StatusText = a.Status.ToLower() switch
                    {
                        "approved" => "🟢 Đã duyệt",
                        "pending" => "🟡 Đang chờ",
                        "rejected" => "🔴 Bị từ chối",
                        _ => "❔ Không xác định"
                    },
                    dayleft = a.Job.ApplicationDeadline != null ? ((DateTime)a.Job.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    isExpired = a.Job.ApplicationDeadline != null ? ((DateTime)a.Job.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                }).ToList();
            return Ok(new
            {
                Applications = listPage,
                CurrentPage = page,
                TotalPages = totalPages
            });
        }
        [HttpGet("{employerId}")]
        public async Task<IActionResult> GetCompanyJobs(Guid employerId)
        {
            var employer = await _employerRepository.GetByIdAsync(employerId);
            if (employer == null)
            {
                return NotFound(new { message = "Không tìm thấy nhà tuyển dụng" });
            }

            var jobs = await _context.Jobs
                .Include(j => j.Employer)
                .Include(j => j.Applications)
                .Where(j => j.EmployerID == employerId && j.Status == "approved")
                .OrderByDescending(j => j.PostedDate)
                .ToListAsync();

            var pageJob = jobs.Select(j => new
            {
                j.Id,
                j.JobTitle,
                j.JobDescription,
                j.Salary,
                j.PostedDate,
                j.ApplicationDeadline,
                j.Locate,
                j.EmployerID,
                EmployerAddress = j.Employer.CompanyAddress,
                EmployerDescription = j.Employer.CompanyDescription,
                EmployerSize = j.Employer.CompanySize,
                EmployerLatitude = j.Employer.Latitude / 10000000.0,
                EmployerLongitude = j.Employer.Longitude / 1000000,
                EmployerEmail = j.Employer.CompanyEmail,
                EmployerName = j.Employer?.CompanyName,
                EmployerLogo = j.Employer?.CompanyLogo,
                dayleft = j.ApplicationDeadline !=null ?((DateTime) j.ApplicationDeadline -DateTime.Now).Days : (int?)null,
                isExpired = j.ApplicationDeadline !=null ?((DateTime) j.ApplicationDeadline -DateTime.Now).Days<0 : false,
            }).ToList();

            return Ok(new
            {
                jobs = pageJob,
            });
        }
    }
}
