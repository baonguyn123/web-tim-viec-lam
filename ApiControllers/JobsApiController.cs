using System.Net.NetworkInformation;
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
    public class JobsApiController : ControllerBase
    {
        private readonly ICandidateProfileRepository _candidateProfileRepositor;
        private readonly ApplicationDbContext _context;
        public JobsApiController(ApplicationDbContext context, ICandidateProfileRepository candidateProfileRepositor)
        {
            _candidateProfileRepositor = candidateProfileRepositor;
            _context = context;
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
        [HttpGet("GetJobsAll")]
        public IActionResult GetJobsAll()
        {
            var categories = _context.Categories.Select(c => new { c.Id, c.Name }).ToList();
            var jobs = _context.Jobs
                .Include(j => j.Category)
                .Include(j => j.Employer)
                .Include(j => j.Applications) // Bao gồm thông tin ứng tuyển
                .Where(j => j.Status == "approved");
         
            var totalJobs = jobs.Count();
            var jobsList = jobs.OrderByDescending(j => j.PostedDate)
                .Select
                (j => new
                {
                    j.Id,
                    j.JobTitle,
                    j.JobDescription,
                    j.Locate,
                    j.Salary,
                    j.PostedDate,
                    j.EmployerID,
                    EmployerName = j.Employer.CompanyName,
                    EmployerLogo = j.Employer.CompanyLogo,
                    j.JobType.JobType_Name,
                    j.CategoryId,
                    CategoryName = j.Category.Name,
                    dayleft = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    isExpired = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                    ApplicationCount = j.Applications.Count
                }).ToList();
            return Ok(new
            {
            
                Jobs = jobsList,
        
            });
        }
        [HttpGet]
        public IActionResult getJobs(int? categorId, int page = 1, int pageSize = 5)
        {
            var categories = _context.Categories.Select(c => new { c.Id, c.Name }).ToList();
            var jobs = _context.Jobs
                .Include(j => j.Category)
                .Include(j => j.Employer)
                .Include(j => j.Applications) // Bao gồm thông tin ứng tuyển
                .Where(j => j.Status == "approved");
            if (categorId.HasValue)
            {
                jobs = jobs.Where(j => j.CategoryId == categorId.Value);
            }
            var totalJobs = jobs.Count();
            var jobsList = jobs.OrderByDescending(j => j.PostedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select
                (j => new
                {
                    j.Id,
                    j.JobTitle,
                    j.JobDescription,
                    j.Locate,
                    j.Salary,
                    j.PostedDate,
                    j.EmployerID,
                    EmployerName = j.Employer.CompanyName,
                    EmployerLogo = j.Employer.CompanyLogo,
                    j.JobType.JobType_Name,
                    j.CategoryId,
                    CategoryName = j.Category.Name,
                    dayleft = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline-DateTime.Now).Days : (int?)null,
                    isExpired = j.ApplicationDeadline != null ?((DateTime)j.ApplicationDeadline-DateTime.Now).Days < 0 : false,
                    ApplicationCount = j.Applications.Count
                }).ToList();
            return Ok(new
            {
                Categories = categories,
                Jobs = jobsList,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalJobs / pageSize)
            });
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> getJob(Guid id)
        {
            var job = _context.Jobs
                .Include(j => j.Category)
                .Include(j => j.Employer)
                .Include(j => j.JobType)
                .FirstOrDefault(j => j.Id == id && j.Status == "approved");

            if (job == null) return NotFound();

            // Lấy profile ứng viên từ token
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                return Unauthorized();
            }

            var candidate =   await _candidateProfileRepositor.GetByUserIdAsync(userID.Value);
            if (candidate == null)
            {
                return NotFound(new { message = "Bạn cần tạo hồ sơ ứng viên trước khi sử dụng tính năng này." });
            }

            // Tính match %
            double matchPercent = CalculateMatchPercent(job, candidate);

            var dayLeft = job.ApplicationDeadline != null ? ((DateTime)job.ApplicationDeadline - DateTime.Now).Days : (int?)null;
            var isExpired = dayLeft < 0;

            return Ok(new
            {
                job.Id,
                job.JobTitle,
                job.JobDescription,
                job.Requirements,
                job.Benefits,
                job.Salary,
                job.Locate,
                job.ApplicationDeadline,
                job.PostedDate,
                Category = job.Category.Name,
                JobType = job.JobType.JobType_Name,
                DaysLeft = dayLeft,
                IsExpired = isExpired,
                MatchPercent = matchPercent, // <-- thêm đây
                Employer = new
                {
                    job.Employer.ID,
                    job.Employer.CompanyName,
                    job.Employer.CompanyEmail,
                    job.Employer.CompanySize,
                    job.Employer.CompanyLogo,
                    job.Employer.CompanyDescription,
                    job.Employer.LicenseDocument,
                    job.Employer.Latitude,
                    job.Employer.Longitude,
                    job.Employer.CompanyAddress,
                }
            });
        }

        [HttpGet("GetBlogPosts")]
        public IActionResult GetBlogPosts(int page = 1, int pageSize = 5)
        {
            var totalBlogs = _context.BlogPosts.Count();
            var blogPosts = _context.BlogPosts
                    .OrderByDescending(b => b.PostedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                     .Select(b => new
                     {
                         b.ID,
                         b.Title,
                         b.Content,
                         b.PostedDate
                     })
                    .ToList();
            return Ok(new
            {   
                BlogPosts = blogPosts,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling((double)totalBlogs / pageSize)
            });
        }
        //[HttpGet("{jobId}/match/{candidateId}")]
        //public IActionResult GetJobMatch(Guid jobId, Guid candidateId)
        //{
        //    // Lấy Job
        //    var job = _context.Jobs
        //        .Include(j => j.JobType)
        //        .Include(j => j.Category)
        //        .FirstOrDefault(j => j.Id == jobId && j.Status == "approved");

        //    if (job == null) return NotFound(new { message = "Job not found" });

        //    // Lấy Candidate
        //    var candidate = _context.CandidateProfiles.FirstOrDefault(c => c.ID == candidateId);
        //    if (candidate == null) return NotFound(new { message = "Candidate not found" });

        //    // Tính match %
        //    double matchPercent = CalculateMatchPercent(job, candidate);

        //    return Ok(new { JobId = jobId, CandidateId = candidateId, MatchPercent = matchPercent });
        //}

        private double CalculateMatchPercent(Job job, CandidateProfile candidate)
        {
            double total = 0;
            double maxTotal = 100;

            // 1. Kinh nghiệm & học vấn (25%)
            double expEduMatch = 0;
            if (!string.IsNullOrEmpty(candidate.ExperienceYear) && int.TryParse(candidate.ExperienceYear, out int expYears))
            {
                int requiredExp = 0;
                var matchExp = System.Text.RegularExpressions.Regex.Match(job.Requirements, @"(\d+)\s*năm");
                if (matchExp.Success) requiredExp = int.Parse(matchExp.Groups[1].Value);

                expEduMatch += requiredExp == 0 ? 15 : (expYears >= requiredExp ? 15 : 15 * ((double)expYears / requiredExp));
            }

            if (!string.IsNullOrEmpty(candidate.Education) && !string.IsNullOrEmpty(job.Requirements))
            {
                if (job.Requirements.Contains(candidate.Education, StringComparison.OrdinalIgnoreCase))
                    expEduMatch += 10;
            }
            total += expEduMatch;

            // 2. Ngôn ngữ & SoftSkill (15%)
            double skillMatch = 0;
            var candidateSkills = new List<string>();
            if (!string.IsNullOrEmpty(candidate.Language)) candidateSkills.AddRange(candidate.Language.Split(',', StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrEmpty(candidate.SoftSkill)) candidateSkills.AddRange(candidate.SoftSkill.Split(',', StringSplitOptions.RemoveEmptyEntries));

            foreach (var skill in candidateSkills)
            {
                if (job.Requirements.Contains(skill.Trim(), StringComparison.OrdinalIgnoreCase))
                    skillMatch += 5;
            }
            if (skillMatch > 15) skillMatch = 15;
            total += skillMatch;

            // 3. Vị trí địa lý (15%)
            double locationMatch = 0;
            if (!string.IsNullOrEmpty(candidate.UserAddress) && !string.IsNullOrEmpty(job.Locate))
            {
                if (candidate.UserAddress.Contains(job.Locate, StringComparison.OrdinalIgnoreCase) ||
                   job.Locate.Contains(candidate.UserAddress, StringComparison.OrdinalIgnoreCase))
                    locationMatch = 15;
            }
            total += locationMatch;

            // 4. Mức lương (15%)
            double salaryMatch = 0;
            if (!string.IsNullOrEmpty(candidate.DesiredSalary) && !string.IsNullOrEmpty(job.Salary))
            {
                if (double.TryParse(candidate.DesiredSalary, out double desiredSalary) &&
                   double.TryParse(job.Salary, out double jobSalary))
                {
                    salaryMatch = desiredSalary <= jobSalary ? 15 : 15 * (jobSalary / desiredSalary);
                }
            }
            total += salaryMatch;

            // 5. Vị trí công việc (30%)
            double jobTitleMatch = 0;
            if (!string.IsNullOrEmpty(candidate.UserDesiredJob) && !string.IsNullOrEmpty(job.JobTitle))
            {
                if (job.JobTitle.Contains(candidate.UserDesiredJob, StringComparison.OrdinalIgnoreCase) ||
                   candidate.UserDesiredJob.Contains(job.JobTitle, StringComparison.OrdinalIgnoreCase))
                    jobTitleMatch = 30;
            }
            total += jobTitleMatch;

            if (total > maxTotal) total = maxTotal;

            return Math.Round(total, 0);
        }


    }
}
