using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_jobs.Models;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ApplyApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public ApplyApiController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyJob([FromQuery] Guid jobId)
        { 
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để ứng tuyển công việc." });
            var job = await _context.Jobs 
                .Include(j => j.Employer)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.Status == "approved");
            if (job == null)
                return NotFound(new { message = "Công việc không tồn tại hoặc chưa được duyệt." });
            var existingApplication = await _context.Applications .FirstOrDefaultAsync(a => a.Job_ID == jobId && a.User_ID == userId);
            if (existingApplication != null)
                return BadRequest(new { message = "Bạn đã ứng tuyển công việc này trước đó." });
            var candidateProfile = await _context.CandidateProfiles.FirstOrDefaultAsync(c=>c.UserID == userId.Value);
            if (candidateProfile == null)
                return BadRequest(new { message = "Bạn cần tạo hồ sơ ứng viên trước khi ứng tuyển." });
            var application = new Application
            {
                User_ID = userId.Value,
                Job_ID = jobId,
                ApplyDate = DateTime.Now,
                Status = "pending",
                SaveStatus = "applied",
                CandidateProfile = candidateProfile

            };
            _context.Applications.Add(application);
            await _context.SaveChangesAsync();
            if (job.Employer.UserID !=null)
            {
                var notification = new Notification
                {
                    Receiver_ID = job.Employer.UserID.Value,
                    Title = "Thông báo ứng tuyển mới",
                    Message = $"Ứng viên {candidateProfile.UserName} đã ứng tuyển vào công việc '{job.JobTitle}'.",
                    SentDate = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            }
            if (!string.IsNullOrEmpty(candidateProfile.UserEmail))
            {
                var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/JobApplied.html");
                var jobUrl = Url.Action("Detail", "JobPublic", new { id = jobId }, Request.Scheme);
                var logoUrl = "https://192.168.1.5:7044/images/job3.jpg";
                htmlContent = htmlContent
                    .Replace("{{UserName}}", candidateProfile.UserName)
                    .Replace("{{JobTitle}}", job.JobTitle)
                    .Replace("{{JobUrl}}", jobUrl)
                    .Replace("{{LogoUrl}}", logoUrl)
                    .Replace("{{CurrentYear}}", DateTime.Now.Year.ToString());

                await _emailSender.SendEmailAsync(candidateProfile.UserEmail, "Thông báo ứng tuyển thành công", htmlContent);
            }

            return Ok(new { message = "Ứng tuyển thành công!" });

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
    }
}
