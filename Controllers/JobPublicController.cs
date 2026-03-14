using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_jobs.Models;
using web_jobs.Repository;
using web_jobs.ViewModels;

namespace web_jobs.Controllers
{
    [Authorize]
    public class JobPublicController : Controller
    {
        private readonly IJobRepository _jobRepository;
        private readonly ISavedJobRepository _savedJobRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IEmployerRepository _employerRepository;
        public JobPublicController(IJobRepository jobRepository, ISavedJobRepository savedJobRepository, ApplicationDbContext context, IEmailSender emailSender, IEmployerRepository employerRepository)
        {
            _jobRepository = jobRepository;
            _savedJobRepository = savedJobRepository;
            _context = context;
            _emailSender = emailSender;
            _employerRepository = employerRepository;
        }
        public async Task<IActionResult> Detail(Guid id)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if (job == null || job.Status != "approved") // Chỉ hiển thị job đã duyệt
            {
                return NotFound();
            }
            string? applicationStatus = null;
            var userId = GetCurrentUserId();
            if(userId!=null)
            {
                //Dữ liệu có sẵn (List, mảng) → FirstOrDefault()
                //Dữ liệu từ database (EF Core) → FirstOrDefaultAsync() + await
                var application = await _context.Applications.FirstOrDefaultAsync
                    (a => a.Job_ID == id && a.User_ID == userId);
                if(application !=null)
                {
                    applicationStatus = application.Status;
                }

            }
            ViewBag.ApplicationStatus = applicationStatus;

            return View(job);
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
        [HttpGet]
        public async Task<IActionResult> Delete(int savedJobId)
        {
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                return RedirectToAction("Index", "Home");
            }
            var saveJob = await _savedJobRepository.GetSavedJobsByUserIdAsync(userID.Value);
            return View(saveJob); // chỉ định rõ tên view
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSavedJob(int savedJobId)
        {
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                return RedirectToAction("Index", "Home");
            }
            var result = await _savedJobRepository.DeleteSavedJobAsync(savedJobId, userID.Value);
            if (result)
            {
                TempData["Message"] = "Xóa công việc đã lưu thành công.";
            }
            else
            {
                TempData["Message"] = "Không tìm thấy công việc đã lưu hoặc không thuộc về bạn.";
            }
            return RedirectToAction("Index","Home");
        }

        [HttpGet]
        public async Task<IActionResult> SavedJobs(int page=1,int pageSize = 5)
        {
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                TempData["Message"] = "Vui lòng đăng nhập để xem công việc được lưu.";
                return RedirectToAction("Index", "Home");
            }

            var savedJobs = await _savedJobRepository.GetSavedJobsByUserIdAsync(userID.Value);
            var totalSavedJobs = savedJobs.Count();
            var totalPages = (int)Math.Ceiling((double)totalSavedJobs / pageSize);
            var pageJobs = savedJobs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            return View(pageJobs); // chỉ định rõ tên view
        }

        [HttpPost]
            public async Task<IActionResult> SaveJob(Guid jobId)
            {
                var userID = GetCurrentUserId();
                if (userID == null)
                {
                    TempData["Message"] = "Vui lòng đăng nhập để lưu công việc.";
                    return RedirectToAction("Index", "Home");
                }

            var saved = await _savedJobRepository.SaveJobAsync(userID.Value, jobId);
            if (saved!=null)
                {
                    TempData["Message"] = "Lưu công việc thành công.";
                }
                else
                {
                    TempData["Message"] = "Công việc đã được lưu trước đó hoặc không tồn tại.";
                }
                return RedirectToAction("SavedJobs");
            }
        public IActionResult AllJobs(int? categoryId, int page = 1, int pageSize = 6)
        {
            var categories = _context.Categories.ToList();
            var jobs = _context.Jobs
                .Include(j => j.Category)
                .Include(j => j.Employer)
                .Where(j => j.Status == "approved") // Chỉ lấy các công việc đã được phê duyệt
                .ToList();
            if (categoryId.HasValue)
            {
                jobs = jobs.Where(j => j.CategoryId == categoryId.Value).ToList();
            }
            var totalJobs = jobs.Count;
            var totalPages = (int)Math.Ceiling((double)totalJobs / pageSize);
            var job = jobs.OrderByDescending(j => j.PostedDate) // Sắp xếp theo ngày đăng mới nhất
               . Skip((page-1) * pageSize)
                .Take(pageSize)
                .ToList();
            var viewModel = new HomeViewModel
            {
                Categories = categories,
                Jobs = job,
                SelectedCategoryId = categoryId,
                CurrentPage = page,
                TotalPages = totalPages
            };
            return View(viewModel);
        }
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Vui lòng đăng nhập để xem công việc.";
                return RedirectToAction("Index", "Home");
            }
            var applications = await _context.Applications
                .Include(a => a.Job)
                .Where(a => a.User_ID == userId.Value && a.Status == "pending")
                .ToListAsync();
            return View(applications); // View hiện danh sách ứng tuyển
        }
            [HttpGet]
        [ActionName("ApplyJob")]
        public async Task<IActionResult> ApplyJobGet(Guid jobId)
        {
            var userID = GetCurrentUserId();
            if ((userID == null))
            {
                TempData["Message"] = "Vui lòng đăng nhập để ứng tuyển công việc.";
                return RedirectToAction("Index", "Home");
            }
            var job = await _jobRepository.GetByIdAsync(jobId);
            if (job == null || job.Status != "approved") // Chỉ cho phép ứng tuyển công việc đã duyệt
            {
                TempData["Message"] = "Công việc không tồn tại hoặc chưa được phê duyệt.";
                return RedirectToAction("AllJobs");
            }
            return View(job);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
            public async Task<IActionResult> ApplyJob(Guid jobId)
            {
                var userID = GetCurrentUserId();
                if (userID == null)
                {
                    TempData["Message"] = "Vui lòng đăng nhập để ứng tuyển công việc.";
                    return RedirectToAction("Index", "Home");
                }
                Console.WriteLine("UserID đang dùng để apply: " + userID.Value);
                var existingApplication = await _context.Applications
            .FirstOrDefaultAsync(a => a.Job_ID == jobId && a.User_ID == userID.Value);
                if (existingApplication != null)
                {
                    TempData["Message"] = "Bạn đã ứng tuyển công việc này trước đó.";
                    //return RedirectToAction("Detail", new { id = jobId });
                    return RedirectToAction("MyApplications", "JobPublic");
                }
                var candidateProfile = await _context.CandidateProfiles .FirstOrDefaultAsync(p => p.UserID == userID.Value);
           

                if (candidateProfile == null)
                {
                    TempData["Message"] = "Bạn cần tạo hồ sơ ứng viên trước khi ứng tuyển.";
                    return RedirectToAction("Add", "CandidateProfile");
                }
                var application = new Application
                {
                    User_ID = userID.Value,
                    Job_ID = jobId,
                    ApplyDate = DateTime.Now,
                    Status = "pending" ,// Trạng thái ban đầu là pending
                    SaveStatus = "applied",
                    CandidateProfile = candidateProfile // Gắn hồ sơ vào đây!
                };
                Console.WriteLine("UserID gán cho Application: " + application.User_ID);
                _context.Applications.Add(application);
                await _context.SaveChangesAsync();
                // Gửi email thông báo ứng tuyển thành công 
                var job = await _context.Jobs.Include(j => j.Employer)
                    .FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null)
            {
                return NotFound("Công việc không tồn tại.");
            }
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
            // Gửi email thông báo cho ứng viên
            var user = await _context.Applications
                    .Include(a => a.CandidateProfile)
                    .FirstOrDefaultAsync(a=>a.Job_ID == jobId && a.User_ID == userID.Value);
                if(string.IsNullOrEmpty(user?.CandidateProfile.UserEmail))
                {
                    TempData["Message"] = "Không thể gửi email vì thiếu địa chỉ email.";
                    return RedirectToAction("MyApplications", "JobPublic");
                }
                var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/JobApplied.html");
                var jobUrl =  Url.Action("Detail" , "JobPublic", new { id = jobId }, Request.Scheme);
                var logoUrl = Url.Content($"{Request.Scheme}://{Request.Host}/images/job3.jpg");
                htmlContent = htmlContent
                    .Replace("{{UserName}}", user.CandidateProfile.UserName)
                    .Replace("{{JobTitle}}", job.JobTitle)
                    .Replace("{{JobUrl}}", jobUrl)
                    .Replace("{{LogoUrl}}", logoUrl)
                    .Replace("{{CurrentYear}}", DateTime.Now.Year.ToString());

                await _emailSender.SendEmailAsync(user.CandidateProfile.UserEmail, "Thông báo ứng tuyển thành công",
                    htmlContent);

                TempData["Message"] = "Ứng tuyển thành công!";
                //return RedirectToAction("Detail", new { id = jobId });
                return RedirectToAction("MyApplications", "JobPublic");
            }
        [HttpGet]
        public async Task<IActionResult> UpdateApplication(Guid jobId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Job_ID == jobId && a.User_ID == userId.Value);

            if (application == null)
            {
                return NotFound();
            }

            return View(application); // View chứa form chỉnh sửa application
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateApplicationPost(Guid jobId, string? note)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.Job_ID == jobId && a.User_ID == userId.Value);

            if (application == null)
            {
                return NotFound();
            }

            application.Note = note; // Chỉ cho phép chỉnh Note
            await _context.SaveChangesAsync();

            TempData["Message"] = "Cập nhật đơn ứng tuyển thành công.";
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteApplication(Guid jobId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var application = await _context.Applications.FindAsync(jobId, userId.Value);
            if (application == null)
            {
                TempData["Message"] = "Không tìm thấy đơn ứng tuyển.";
                return RedirectToAction("MyApplications");
            }

            _context.Applications.Remove(application);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Đã hủy ứng tuyển thành công.";
            return RedirectToAction("MyApplications");
        }
        public async Task<IActionResult> MyApplications(int page=1, int pageSize=4)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Vui lòng đăng nhập để xem ứng tuyển của bạn.";
                return RedirectToAction("Index", "Home");
            }

            var allApplications = await _context.Applications
                .Include(a => a.Job)
                .ThenInclude(j => j.Employer) // Bao gồm thông tin nhà tuyển dụng
                .Where(a => a.User_ID == userId.Value)
                .OrderByDescending(a=>a.ApplyDate)
                .ToListAsync();
            var total = allApplications.Count;
            var totalPages = (int)Math.Ceiling((double)total/pageSize);
            var applications = allApplications.Skip((page - 1) * pageSize).Take(pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            return View(applications);
        }
        public async Task<IActionResult> CompanyJobs ( Guid employerId, int page=1, int pageSize=4)
        {
            var employer =  await _employerRepository.GetByIdAsync(employerId);
            if (employer == null)
            {
                return NotFound(); // hoặc xử lý khi không tìm thấy
            }
            var jobs =  await _context.Jobs
                .Include(j => j.Employer)
                .Where(j => j.EmployerID == employerId && j.Status == "approved") // Chỉ lấy công việc đã duyệt
                .ToListAsync();
            var totalJobs = jobs.Count();

            int totalPages = (int)Math.Ceiling((double)totalJobs / pageSize);
            var pageJobs = jobs.OrderByDescending(j => j.PostedDate) // Sắp xếp theo ngày đăng mới nhất
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            var model = new CompanyJobsViewModel
            {
                employer = employer,
                Jobs = pageJobs,
                TotalPages = totalPages,
                CurrentPage = page

            };
            return View(model);
        }

    }
}
