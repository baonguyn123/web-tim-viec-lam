using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminJobController : Controller
    {
        
        private readonly IJobRepository _jobRepository;
        private readonly IEmployerRepository _employerRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ICandidateProfileRepository _candidateProfileRepositor;
     
        public AdminJobController(IJobRepository jobRepository, IEmployerRepository employerRepository, ApplicationDbContext context, IEmailSender emailSender, ICandidateProfileRepository candidateProfileRepositor)
        {
            _jobRepository = jobRepository;
            _employerRepository = employerRepository;
            _context = context;
            _emailSender = emailSender;
            _candidateProfileRepositor = candidateProfileRepositor;
        }
        public async Task<IActionResult> Pending(int page=1, int pageSize =5)
        {
            var jobs = (await _jobRepository.GetAllAsync()).Where(j =>j.Status =="pending").ToList();
            var totaJobs = jobs.Count;
            var totPages = (int)Math.Ceiling((double)totaJobs / pageSize);
            var job = jobs.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totPages;
            return View(job);
        }
        [HttpPost]
        public async Task<IActionResult> Approve(Guid id)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }
            job.Status = "approved";
            await _jobRepository.UpdateAsync(job);
            var employer = await _employerRepository.GetByIdAsync(job.EmployerID.Value);
            if(employer != null)
            {
                var notification = new Notification
                {
                    Receiver_ID = employer.UserID.Value,
                    Title = "Thông báo duyệt công việc",
                    Message = $"Thông báo: Công việc \"{job.JobTitle}\" mà Quý công ty đăng tuyển đã được hệ thống phê duyệt thành công.",
                    SentDate =DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/approvedjob.html");
                htmlContent = htmlContent.Replace("{{CompanyName}}", employer.CompanyName)
                                         .Replace("{{JobTitle}}", job.JobTitle);
                await _emailSender.SendEmailAsync(employer.CompanyEmail, "Thông báo từ hệ thống", htmlContent);
            }
            await NotifyCandidatesNewJob(job);
            return RedirectToAction("Pending");
        }
        private async Task NotifyCandidatesNewJob(Job job)
        {
            var candidates = await _context.CandidateProfiles
                .Where(c =>
                    c.UserID != null &&
                    (
                        (c.UserDesiredJob != null && job.JobTitle.Contains(c.UserDesiredJob)) ||
                        (c.SoftSkill != null && job.Requirements.Contains(c.SoftSkill)) ||
                        (c.Language != null && job.Requirements.Contains(c.Language)) ||
                        (c.DesiredSalary != null && job.Salary.Contains(c.DesiredSalary))
                    )
                )
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.UserEmail)) continue;

                var notification = new Notification
                {
                    Receiver_ID = candidate.UserID.Value,
                    Title = "Cơ hội việc làm mới",
                    Message = $@"
                    Công việc <strong>{job.JobTitle}</strong> tại 
                    <strong>{job.Employer.CompanyName}</strong> 
                    vừa được đăng và phù hợp với hồ sơ của bạn.",
                    SentDate = DateTime.Now,
                    IsRead = false
                };

                _context.Notifications.Add(notification);

                var html = System.IO.File.ReadAllText("wwwroot/email_templates/newjob.html")
                    .Replace("{{CandidateName}}", candidate.UserName ?? "Bạn")
                    .Replace("{{JobTitle}}", job.JobTitle)
                    .Replace("{{CompanyName}}", job.Employer.CompanyName);

                await _emailSender.SendEmailAsync(
                    candidate.UserEmail,
                    "Việc làm mới phù hợp với bạn",
                    html
                );
            }

            await _context.SaveChangesAsync();
        }

        [HttpPost]
        public async Task<IActionResult> Reject(Guid id)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if (job == null)
            {
                return NotFound();
            }
            job.Status = "rejected";
            await _jobRepository.UpdateAsync(job);
            var employer = await _employerRepository.GetByIdAsync(job.EmployerID.Value);
            if (employer != null) {
                var notification = new Notification
                {
                    Receiver_ID = employer.UserID.Value,
                    Title = "Thông báo từ hệ thống",
                    Message = $"Thông báo: Công việc \"{job.JobTitle}\" mà Quý công ty đăng tuyển đã bị từ chối.",
                    SentDate = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/rejectedjob.html");
                htmlContent = htmlContent.Replace("{{CompanyName}}", employer.CompanyName)
                                         .Replace("{{JobTitle}}", job.JobTitle);
                await _emailSender.SendEmailAsync(employer.CompanyEmail, "Thông báo từ hệ thống", htmlContent);
            }
            return RedirectToAction("Pending");
        }
        public async Task<IActionResult> ApprovedAsync()
        {
            var jobs = (await _jobRepository.GetAllAsync())
               .Where(j => j.Status == "approved")
               .ToList();
            return View(jobs);
        }
        public async Task<IActionResult> Rejected()
        {
            var jobs = (await _jobRepository.GetAllAsync())
                        .Where(j => j.Status == "rejected")
                        .ToList();
            return View(jobs);
        }
        public async Task<IActionResult> PendingEmployers()
        {
            var employers = (await _employerRepository.GetAllAsync())
                            .Where(e => e.Status == "pending")
                            .ToList();
            return View(employers);
        }
        [HttpPost]
        public async Task<IActionResult> ApproveEmployer(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            employer.Status = "approved";
            await _employerRepository.UpdateAsync(employer);
            var notification = new Notification
            {
                Receiver_ID = employer.UserID.Value,
                Title = "Thông báo từ hệ thống\"",
                Message = $"Thông báo: Công ty \"{employer.CompanyName}\" đã được hệ thống phê duyệt thành công.",
                SentDate = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/approvedemployer.html");
            htmlContent = htmlContent.Replace("{{CompanyName}}", employer.CompanyName);
            await _emailSender.SendEmailAsync(employer.CompanyEmail, "Thông báo từ hệ thống", htmlContent);
            return RedirectToAction("PendingEmployers");
        }

        [HttpPost]
        public async Task<IActionResult> RejectEmployer(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            employer.Status = "rejected";
            await _employerRepository.UpdateAsync(employer);
            var notification = new Notification
            {
                Receiver_ID = employer.UserID.Value,
                Title = "Thông báo từ hệ thống",
                Message = $"Thông báo: Công ty \"{employer.CompanyName}\" đã bị từ chối.",
                SentDate = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/rejectedemployer.html");
            htmlContent = htmlContent.Replace("{{CompanyName}}",employer.CompanyName);
            await _emailSender.SendEmailAsync(employer.CompanyEmail, "Thông báo từ hệ thống", htmlContent);
            return RedirectToAction("PendingEmployers");
        }

        public async Task<IActionResult> EmployerDetails(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            return View(employer);
        }
        public async Task<IActionResult> AllEmployers(string filter = "all", int page=1, int pageSize=5)
        {
            var allEmployers = (await _employerRepository.GetAllAsync())
                                .Where(e => !string.IsNullOrEmpty(e.Status))
                                .ToList();

            // Đếm theo trạng thái
            ViewBag.ApprovedCount = allEmployers.Count(e => e.Status.ToLower() == "approved");
            ViewBag.PendingCount = allEmployers.Count(e => e.Status.ToLower() == "pending");
            ViewBag.RejectedCount = allEmployers.Count(e => e.Status.ToLower() == "rejected");

            // Lọc theo filter (nếu có)
            if (!string.IsNullOrEmpty(filter) && filter.ToLower() != "all")
            {
                allEmployers = allEmployers
                    .Where(e => e.Status?.ToLower() == filter.ToLower())
                    .ToList();
                ViewBag.Filter = filter.ToLower();
            }
            else
            {
                ViewBag.Filter = "";
            }
            var totalEmployer = allEmployers.Count;
            var totPages = (int)Math.Ceiling((double)totalEmployer / pageSize);
            var employer = allEmployers.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totPages;

            return View(employer);
        }


    }
}
