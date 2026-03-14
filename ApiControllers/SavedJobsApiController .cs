using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SavedJobsApiController : ControllerBase
    {
        private readonly ISavedJobRepository _savedJobRepository;
        public SavedJobsApiController(ISavedJobRepository savedJobRepository)
        {
            _savedJobRepository = savedJobRepository;
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
        [HttpGet("SavedJobsAll")]
        public async Task<IActionResult> GetAllSavedJobs()
        {
            var userId = GetCurrentUserId();
            //if (userId == null)
            //    return Unauthorized(new { message = "Vui lòng đăng nhập để xem công việc đã lưu." });
            if (userId == null)
            {
                userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            }
            var saveJobs = await _savedJobRepository.GetSavedJobsByUserIdAsync(userId.Value);
            var listPage = saveJobs
                .Select(j => new
                {
                    SaveJobId = j.Id,   // id bản ghi save job
                    JobId = j.Job.Id,   // id thực của job (UUID)
                    j.Job.JobTitle,
                    j.Job.Locate,
                    j.Job.Salary,
                    JobType = j.Job.JobType != null ? j.Job.JobType.JobType_Name : null,
                    j.Job.Employer.CompanyLogo,
                    j.Job.Employer.CompanyName,
                    dayleft = j.Job.ApplicationDeadline != null ? ((DateTime)j.Job.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    isExpired = j.Job.ApplicationDeadline != null ? ((DateTime)j.Job.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                }).ToList();
            return Ok(new
            {
                SavedJobs = listPage,
                
            });

        }
        [HttpGet("JobSaved")]
        public async Task<IActionResult> JobSaved(int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            //if (userId == null)
            //    return Unauthorized(new { message = "Vui lòng đăng nhập để xem công việc đã lưu." });
            if (userId == null)
            {
                userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            }
            var saveJobs = await _savedJobRepository.GetSavedJobsByUserIdAsync(userId.Value);
            var totalJobs = saveJobs.Count();
            var totalPages = (int)Math.Ceiling((double)totalJobs / pageSize);
            var listPage = saveJobs.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new
                {
                    SaveJobId = j.Id,   // id bản ghi save job
                    JobId = j.Job.Id,   // id thực của job (UUID)
                    j.Job.JobTitle,
                    j.Job.Locate,
                    j.Job.Salary,
                    JobType = j.Job.JobType != null ? j.Job.JobType.JobType_Name : null,
                    j.Job.Employer.CompanyLogo,
                    j.Job.Employer.CompanyName, 
                    dayleft = j.Job.ApplicationDeadline != null ? ((DateTime)j.Job.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    isExpired = j.Job.ApplicationDeadline != null ? ((DateTime)j.Job.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                }).ToList();
            return Ok(new
            {
                SavedJobs = listPage,
                CurrentPage = page,
                TotalPages = totalPages
            });

        }
        [HttpPost("SaveJob")]
        public async Task<IActionResult> SabeJob([FromQuery] Guid jobId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để lưu công việc." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var saved = await _savedJobRepository.SaveJobAsync(userId.Value, jobId);
            if (saved==null)
                return BadRequest(new { message = "Lưu công việc không thành công. Vui lòng thử lại." });
            return Ok(new { message = "Lưu công việc thành công.",
                saveJobId = saved.Id
            });
        }
    
    [HttpDelete("DeleteSavedJob")]
        public async Task<IActionResult> Delete([FromQuery] int saveJobId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xóa công việc đã lưu." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var deleted = await _savedJobRepository.DeleteSavedJobAsync(saveJobId, userId.Value);
            if (!deleted)
                return BadRequest(new { message = "Xóa công việc đã lưu không thành công. Vui lòng thử lại." });
            return Ok(new { message = "Xóa công việc đã lưu thành công." });
        }
    }
}
