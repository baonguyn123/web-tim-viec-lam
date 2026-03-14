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
    [Authorize(Roles = "Employer, Admin")]

    public class EmployerController : Controller
    {
        private readonly IEmployerRepository _employerRepository;
        private readonly IWebHostEnvironment _env;
        private readonly ICandidateProfileRepository _candidateRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        public EmployerController(IEmployerRepository employerRepository, IWebHostEnvironment env, ICandidateProfileRepository candidateProfileRepository, ApplicationDbContext context, IEmailSender emailSender)
        {
            _employerRepository = employerRepository;
            _env = env;
            _candidateRepository = candidateProfileRepository;
            _context = context;
            _emailSender = emailSender;
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

        public async Task<IActionResult> ViewApplications(string filter = "all", int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
            {
                return NotFound();
            }
            var jobs = await _employerRepository.GetJobsByEmployerIdAsync(employer.ID);

            // Lấy danh sách ứng tuyển cho các job đó
            var applications = jobs
                .SelectMany(j => j.Applications)
                .ToList();

            ViewBag.ApprovedCount = applications.Count(a => a.Status == "approved");
            ViewBag.PendingCount = applications.Count(a => a.Status == "pending");
            ViewBag.RejectedCount = applications.Count(a => a.Status == "rejected");

            //  Lọc theo filter
            if (!string.IsNullOrEmpty(filter) && filter.ToLower() != "all")
            {
                applications = applications
                    .Where(j => j.Status?.ToLower() == filter.ToLower())
                    .ToList();
                ViewBag.Filter = filter.ToLower();
            }
            else
            {
                ViewBag.Filter = ""; // ← THÊM DÒNG NÀY ĐỂ ĐẢM BẢO ViewBag KHÔNG BỊ LƯU GIÁ TRỊ CŨ
            }
            var totalApplications = applications.Count();
            var totalPages = (int)Math.Ceiling((double)totalApplications / pageSize);
            var application = applications
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            return View(application);
        }

        public async Task<IActionResult> CandidateProfileDetail(Guid id, Guid jobId)
        {
            var profile = await _candidateRepository.GetByIdAsync(id);
            if (profile == null)
            {
                // Trả về view báo lỗi, bạn tạo view Views/Employer/NotFoundProfile.cshtml
                return View("NotFoundProfile");
            }
            ViewBag.JobId = jobId;
            return View(profile);
        }


        public async Task<IActionResult> Index(int page = 1, int pageSize = 5)
        {
            var employers = await _employerRepository.GetAllAsync();
            var totalEmployers = employers.Count();
            var totalPages = (int)Math.Ceiling((double)totalEmployers / pageSize);
            var employer = employers
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(employer);
        }

        public async Task<IActionResult> Detail(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            return View(employer);
        }

        public async Task<IActionResult> Add()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userId, out Guid guid))
            {
                var existing = await _employerRepository.GetByUserIdAsync(guid);
                if (existing != null)
                {
                    // Nếu đã có thông tin công ty → chuyển sang trang sửa
                    return RedirectToAction("Update", new { id = existing.ID });
                }
            }

            return View(new Employer());
        }
        public async Task<IActionResult> MyCompanyStatus()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
            {
                // Chưa có công ty, chuyển sang trang thêm mới
                return RedirectToAction("Add");
            }

            return View(employer);
        }


        [HttpPost]
        public async Task<IActionResult> Add(Employer employer, IFormFile logoFile, IFormFile licenseFile)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid guid))
            {
                ModelState.AddModelError("", "Không lấy được UserID từ đăng nhập.");
                return View(employer);
            }

            else
            {
                employer.UserID = guid;
            }
            employer.Status = "pending";

            if (logoFile == null)
            {
                ModelState.AddModelError("LogoFile", "⚠️ LogoFile = null");
            }
            else if (logoFile.Length == 0)
            {
                ModelState.AddModelError("LogoFile", "⚠️ LogoFile.Length = 0");
            }
            else
            {
                ModelState.AddModelError("LogoFile", $"⚠️ LogoFile OK - Name: {logoFile.FileName}");
            }

            if (licenseFile == null)
            {
                ModelState.AddModelError("LicenseFile", "⚠️ LicenseFile = null");
            }
            else if (licenseFile.Length == 0)
            {
                ModelState.AddModelError("LicenseFile", "⚠️ LicenseFile.Length = 0");
            }
            else
            {
                ModelState.AddModelError("LicenseFile", $"⚠️ LicenseFile OK - Name: {licenseFile.FileName}");
            }

            // Nếu có file mới thì upload
            if (logoFile != null && logoFile.Length > 0)
            {
                //Path.GetExtension(logoFile.FileName) → lấy đuôi file gốc của người dùng upload,
                string logoName = Guid.NewGuid().ToString() + Path.GetExtension(logoFile.FileName);
                string logoPath = Path.Combine(_env.WebRootPath, "images", logoName);
                using (var stream = new FileStream(logoPath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }
                employer.CompanyLogo = logoName;
            }

            if (licenseFile != null && licenseFile.Length > 0)
            {
                string licenseName = Guid.NewGuid().ToString() + Path.GetExtension(licenseFile.FileName);
                string licensePath = Path.Combine(_env.WebRootPath, "licenses", licenseName);
                using (var stream = new FileStream(licensePath, FileMode.Create))
                {
                    await licenseFile.CopyToAsync(stream);
                }
                employer.LicenseDocument = licenseName;
            }

            await _employerRepository.AddAsync(employer);
            return RedirectToAction("MyCompanyStatus");
        }

        public async Task<IActionResult> Update(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            // Lấy userId từ đăng nhập
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null || !Guid.TryParse(userIdString, out var userId) || employer.UserID != userId)
            {
                // Không phải chủ sở hữu, từ chối quyền truy cập
                return View("AccessDenied", "Bạn không có quyền chỉnh sửa hoặc xóa mục này.");
            }
            return View(employer);
        }

        [HttpPost]
        public async Task<IActionResult> Update(Guid id, Employer updatedEmployer, IFormFile logoFile, IFormFile licenseFile)
        {
            if (id != updatedEmployer.ID)
            {
                return NotFound();
            }
            var existingEmployer = await _employerRepository.GetByIdAsync(id);
            if (existingEmployer == null)
            {
                return NotFound();
            }
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null || !Guid.TryParse(userIdString, out var userId) || existingEmployer.UserID != userId)
            {
                // Không phải chủ sở hữu, từ chối quyền truy cập
                return View("AccessDenied", "Bạn không có quyền chỉnh sửa hoặc xóa mục này.");
            }
            if (logoFile != null && logoFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingEmployer.CompanyLogo))
                {
                    var oldLogoPath = Path.Combine(_env.WebRootPath, "images", existingEmployer.CompanyLogo);
                    if (System.IO.File.Exists(oldLogoPath))
                    {
                        System.IO.File.Delete(oldLogoPath);
                    }
                }
                string logoName = Guid.NewGuid().ToString() + Path.GetExtension(logoFile.FileName);
                string logoPath = Path.Combine(_env.WebRootPath, "images", logoName);
                using (var stream = new FileStream(logoPath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }
                // Cập nhật tên logo mới vào đối tượng updatedEmployer
                updatedEmployer.CompanyLogo = logoName;
            }
            else
            {
                // Nếu không có ảnh mới → giữ lại ảnh cũ
                updatedEmployer.CompanyLogo = existingEmployer.CompanyLogo;
            }
            if (licenseFile != null && licenseFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingEmployer.LicenseDocument))
                {
                    var oldLicensePath = Path.Combine(_env.WebRootPath, "licenses", existingEmployer.LicenseDocument);
                    if (System.IO.File.Exists(oldLicensePath))
                    {
                        System.IO.File.Delete(oldLicensePath);
                    }
                }
                string licenseName = Guid.NewGuid().ToString() + Path.GetExtension(licenseFile.FileName);
                string licensePath = Path.Combine(_env.WebRootPath, "licenses", licenseName);
                using (var stream = new FileStream(licensePath, FileMode.Create))
                {
                    await licenseFile.CopyToAsync(stream);
                }
                updatedEmployer.LicenseDocument = licenseName;
            }
            else
            {
                // Nếu không có giấy phép mới → giữ lại giấy phép cũ
                updatedEmployer.LicenseDocument = existingEmployer.LicenseDocument;
            }
            existingEmployer.CompanyName = updatedEmployer.CompanyName;
            existingEmployer.CompanyEmail = updatedEmployer.CompanyEmail;
            existingEmployer.CompanySize = updatedEmployer.CompanySize;
            existingEmployer.CompanyDescription = updatedEmployer.CompanyDescription;
            existingEmployer.LicenseDocument = updatedEmployer.LicenseDocument;
            existingEmployer.CompanyLogo = updatedEmployer.CompanyLogo;
            existingEmployer.Latitude = updatedEmployer.Latitude;
            existingEmployer.Longitude = updatedEmployer.Longitude;
            existingEmployer.CompanyAddress = updatedEmployer.CompanyAddress;
            await _employerRepository.UpdateAsync(existingEmployer);
            // PHẢI CÓ ID EMPLOYER
            return RedirectToAction("CompanyJobs", "JobPublic", new { employerId = existingEmployer.ID });
        }
        public async Task<IActionResult> Delete(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || employer.UserID.ToString() != userId)
            {
                return View("AccessDenied", "Bạn không có quyền chỉnh sửa hoặc xóa mục này.");
            }
            return View(employer);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound();
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null 
                || employer.UserID.ToString() != userId)
            {
                return View("AccessDenied", "Bạn không có quyền chỉnh sửa hoặc xóa mục này.");
            }

            await _employerRepository.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveApplication(Guid jobId, Guid userId)
        {
            if (jobId == Guid.Empty || userId == Guid.Empty)
            {
                return BadRequest("Thông tin không hợp lệ.");
            }

            var app = _context.Applications
                 .Include(a => a.Job)
                 .ThenInclude(a => a.Employer)
                 .Include(a => a.CandidateProfile)
                 .FirstOrDefault(a => a.Job_ID == jobId && a.User_ID == userId);
            if (app == null)
            {
                return NotFound();
            }
            app.Status = "approved";
            _context.Applications.Update(app);
            var jobUrl = Url.Action("Detail", "JobPublic", new { id = app.Job.Id }, protocol: Request.Scheme);
            var notification = new Notification
            {
                Receiver_ID = userId,
                Title = "Thông báo duyệt hồ sơ",
                Message = $@"
        🎉 Chúc mừng bạn! Hồ sơ ứng tuyển của bạn đã được duyệt bởi nhà tuyển dụng.<br/>
        Bạn chính thức được mời tham gia phỏng vấn vào vị trí <strong>{app.Job.JobTitle}</strong> tại công ty <strong>{app.Job.Employer.CompanyName}</strong>.<br/>
        Vui lòng <strong>kiểm tra điện thoại thường xuyên</strong> để nhận thông tin cụ thể về lịch phỏng vấn từ bộ phận nhân sự.
        👉Vui lòng truy cập web hoặc app để xem lại chi tiết công việc <br/>
        Hãy chuẩn bị thật tốt và sẵn sàng cho bước tiếp theo. Chúc bạn thành công!",
                SentDate = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/approved.html");
            htmlContent = htmlContent.Replace("{{JobTitle}}", app.Job.JobTitle)
                                      .Replace("{{CandidateName}}", app.CandidateProfile.UserName)
                                     .Replace("{{CompanyName}}", app.Job.Employer.CompanyName)
                                     .Replace("{{JobUrl}}", jobUrl);
            await _emailSender.SendEmailAsync(app.CandidateProfile.UserEmail, "[Website Tìm Việc] Hồ sơ ứng tuyển của bạn đã được duyệt", htmlContent);
            return RedirectToAction("ViewApplications");


        }
        [Authorize(Roles = "Employer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApplication(Guid jobId, Guid userId)
        {
            var app = _context.Applications
                .Include(a => a.Job)
                .ThenInclude(a => a.Employer)
                .Include(a => a.CandidateProfile)
                .FirstOrDefault(a => a.Job_ID == jobId && a.User_ID == userId);
            if (app == null)
            {
                return NotFound();
            }
            app.Status = "rejected";
            _context.Applications.Update(app);

            // Tạo notification
            var notification = new Notification
            {
                Receiver_ID = userId,
                Title = "Thông báo từ chối hồ sơ",
                Message = $"Rất tiếc! Hồ sơ ứng tuyển của bạn cho vị trí **{app.Job.JobTitle}** tại công ty **{app.Job.Employer.CompanyName}** chưa phù hợp với yêu cầu tuyển dụng hiện tại. Chúng tôi rất cảm ơn bạn đã quan tâm và dành thời gian ứng tuyển. Chúc bạn sớm tìm được công việc phù hợp trong thời gian tới!",
                SentDate = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/rejected.html");
            htmlContent = htmlContent.Replace("{{JobTitle}}", app.Job.JobTitle)
                                      .Replace("{{CandidateName}}", app.CandidateProfile.UserName)
                                      .Replace("{{CompanyName}}", app.Job.Employer.CompanyName);
            await _emailSender.SendEmailAsync(app.CandidateProfile.UserEmail, "[Website Tìm Việc] Hồ sơ ứng tuyển của bạn đã bị từ chối", htmlContent);
            return RedirectToAction("ViewApplications");
        }
        public IActionResult SearchCandidates(string keyword, string location, string skill, string salary, string education, string experienceYear, string desiredJob, int page = 1, int pageSize = 5)
        {
            var candidates = _context.Applications
                      .Include(a => a.Job)
                      .Include(a => a.CandidateProfile)
                      .AsQueryable();
            if (!string.IsNullOrEmpty(keyword))
            {
                candidates = candidates.Where(c =>
                c.CandidateProfile.UserDesiredJob.Contains(keyword) ||
                c.CandidateProfile.CareerObjective.Contains(keyword) ||
                c.CandidateProfile.Experience.Contains(keyword) ||
                c.CandidateProfile.Education.Contains(keyword) ||
                c.CandidateProfile.SoftSkill.Contains(keyword) ||
                c.CandidateProfile.Language.Contains(keyword));
            }
            if (!string.IsNullOrEmpty(location)) { 
                candidates = candidates.Where(c=> c.CandidateProfile.UserAddress.Contains(location));
            }
            if(!string.IsNullOrEmpty(skill))
            {
                candidates = candidates.Where(c=> c.CandidateProfile.SoftSkill.Contains(skill) || c.CandidateProfile.Language.Contains(skill));
            }
            if (!string.IsNullOrEmpty(salary))
            {
                candidates = candidates.Where(c => c.CandidateProfile.DesiredSalary.Contains(salary));
            }
            if (!string.IsNullOrEmpty(education))
            {
                candidates = candidates.Where(c => c.CandidateProfile.Education.Contains(education));
            }
            if (!string.IsNullOrEmpty(experienceYear))
            {
                candidates = candidates.Where(c => c.CandidateProfile.ExperienceYear.Contains(experienceYear));
            }
            if (!string.IsNullOrEmpty(desiredJob))
            {
                candidates = candidates.Where(c => c.CandidateProfile.UserDesiredJob.Contains(desiredJob));
            }
            var totTalCandidates = candidates.Count();
            var totalPages = (int)Math.Ceiling((double)totTalCandidates / pageSize);
            var candidateList = candidates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            return View(candidateList);


        }
    }
}
