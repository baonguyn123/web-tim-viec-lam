using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployerJobAPIController : ControllerBase
    {
        private readonly IEmployerRepository _employerRepository;
        private readonly IWebHostEnvironment _env;
        private readonly ICandidateProfileRepository _candidateRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IJobRepository _jobRepository;
        public EmployerJobAPIController(IJobRepository jobRepository,IEmployerRepository employerRepository, IWebHostEnvironment env, ICandidateProfileRepository candidateProfileRepository, ApplicationDbContext context, IEmailSender emailSender)
        {
            _employerRepository = employerRepository;
            _env = env;
            _candidateRepository = candidateProfileRepository;
            _context = context;
            _emailSender = emailSender;
            _jobRepository = jobRepository;
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
        [HttpGet("jobapplications")]
        public async Task<IActionResult> ViewJobApplications(Guid jobId, string filter = "all")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            //}
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
            {
                return NotFound(new { message = "Nhà tuyển dụng không tồn tại" });
            }
            var jobs = await _jobRepository.GetJobByIdAsync(jobId);
            var applications = jobs.Applications.ToList();
            var start = new
            {
                approved = applications.Count(a => a.Status == "approved"),
                pending = applications.Count(a => a.Status == "pending"),
                rejected = applications.Count(a => a.Status == "rejected"),
                total = applications.Count
            };
            if (!string.IsNullOrEmpty(filter) && filter.ToLower() != "all")
            {
                applications = applications.Where
                    (a => a.Status.ToLower() == filter.ToLower()).ToList();
            }
            var result = applications.Select(a => new
            {
                a.Job_ID,
                a.User_ID,
                a.Job.JobTitle,
                a.ApplyDate,
                a.CandidateProfile.UserName,
                a.Status,
                a.CandidateProfile.ID
            }
            ).ToList();
            return Ok(new
            {
                jobs = result,
                start
            });
        }

        [HttpGet("applications")]
        public async Task<IActionResult> ViewApplications(string filter = "all")
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            //}
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
            {
                return NotFound(new { message = "Nhà tuyển dụng không tồn tại" });
            }
            var jobs = await _employerRepository.GetJobsByEmployerIdAsync(employer.ID);
            var applications = jobs.SelectMany
                (j => j.Applications).ToList();
            var start = new
            {
                approved = applications.Count(a => a.Status == "approved"),
                pending = applications.Count(a => a.Status == "pending"),
                rejected = applications.Count(a => a.Status == "rejected")
            };
            if (!string.IsNullOrEmpty(filter) && filter.ToLower() != "all")
            {
                applications = applications.Where
                    (a => a.Status.ToLower() == filter.ToLower()).ToList();
            }
            var result = applications.Select(a => new
            {
                a.Job_ID,
                a.User_ID,
                a.Job.JobTitle,
                a.ApplyDate,
                a.CandidateProfile.UserName,
                a.Status,
                a.CandidateProfile.ID
            }
            ).ToList();
            return Ok(new
            {
                jobs = result,
                start
            });
        }
        [HttpGet("CandidateProfile")]
        public async Task<IActionResult> GetCandidateProfile([FromQuery] Guid jobId, [FromQuery] Guid id)
        {
            var profile = await _candidateRepository
                .GetByIdAsync(id);
            if (profile == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng viên" });
            }
            return Ok(new
            {
                jobId = jobId,
                profile.ID,
                profile.UserID,
                profile.UserName,
                profile.UserEmail,
                profile.UserPhone,
                profile.UserPosition,
                profile.UserAddress,
                profile.UserBirthDate,
                profile.UserFacebook,
                profile.UserAvatar,
                profile.CareerObjective,
                profile.EducationYear,
                profile.Education,
                profile.Experience,
                profile.ExperienceYear,
                profile.DesiredSalary,
                profile.UserDesiredJob,
                profile.CertificateName,
                profile.CertificateYear,
                profile.PrizeDesc,
                profile.PrizeYear,
                profile.Language,
                profile.SoftSkill,
                profile.Interest

            });
        }
        [HttpGet("MyCompanyStatus")]
        public async Task<IActionResult> MyCompanyStatus()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            //}
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
            {
                return NotFound(new { message = "Nhà tuyển dụng không tồn tại" });
            }
            return Ok(new
            {
                employer.ID,
                employer.CompanyName,
                employer.CompanyEmail,
                employer.CompanyAddress,
                employer.CompanySize,
                employer.CompanyDescription,
                employer.Longitude,
                employer.Latitude,
                employer.CompanyLogo,
                employer.LicenseDocument,
                employer.Status
            });
        }
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromForm] Employer employer, IFormFile logoFile, IFormFile licenseFile)
        {
            var userId = GetCurrentUserId();
            //if (userId == null)
            //{
            //    return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            //}
            if (userId == null)
            {
                userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            }
            employer.UserID = userId;
            employer.Status = "pending";
            if (logoFile == null)
            {
                return BadRequest(new { message = "Chưa có logo" });
            }
            else if (logoFile.Length == 0)
            {
                return BadRequest(new { message = "Người dùng chưa chọn logo" });
            }
            if (licenseFile == null)
            {
                return BadRequest(new { message = "Chưa có giấy phép" });
            }
            else if (licenseFile.Length == 0)
            {
                return BadRequest(new { message = "Người dùng chưa thêm giấy phép" });

            }
            if (logoFile != null && logoFile.Length > 0)
            {
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

            return Ok(new { message = "Thêm công ty thành công", data = employer });
        }
        [HttpPost("Update")]
        public async Task<IActionResult> Update([FromQuery] Guid id, [FromForm] Employer updatedEmployer, IFormFile? logoFile, IFormFile? licenseFile)
        {

            if (id != updatedEmployer.ID)
            {
                return BadRequest(new { message = "ID không khớp" });
            }
            var existingEmployer = await _employerRepository.GetByIdAsync(id);
            if (existingEmployer == null)
            {
                return NotFound(new { message = "Công ty không tồn tại" });
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
                //Nếu file đã tồn tại, thì xóa toàn bộ nội dung cũ và ghi đè bằng file mới.
                using (var stream = new FileStream(logoPath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(stream);
                }
                updatedEmployer.CompanyLogo = logoName;
            }
            else
            {
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
                updatedEmployer.LicenseDocument = existingEmployer.LicenseDocument;
            }
            if (!string.IsNullOrEmpty(updatedEmployer.CompanyName))
                existingEmployer.CompanyName = updatedEmployer.CompanyName;
            if (!string.IsNullOrEmpty(updatedEmployer.CompanyEmail))
                existingEmployer.CompanyEmail = updatedEmployer.CompanyEmail;
            if (!string.IsNullOrEmpty(updatedEmployer.CompanySize))
                existingEmployer.CompanySize = updatedEmployer.CompanySize;

            if (!string.IsNullOrEmpty(updatedEmployer.CompanyDescription))
                existingEmployer.CompanyDescription = updatedEmployer.CompanyDescription;

            if (!string.IsNullOrEmpty(updatedEmployer.CompanyAddress))
                existingEmployer.CompanyAddress = updatedEmployer.CompanyAddress;
            if (updatedEmployer.Latitude != 0)
            {
                existingEmployer.Latitude = updatedEmployer.Latitude;
            }
            if (updatedEmployer.Longitude != 0)
            {
                existingEmployer.Longitude = updatedEmployer.Longitude;
            }
            await _employerRepository.UpdateAsync(existingEmployer);
            return Ok(new { message = "Cập nhật công ty thành công", data = existingEmployer });
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var employer = await _employerRepository.GetByIdAsync(id);
            if (employer == null)
            {
                return NotFound(new { message = "Không tìm thấy nhà tuyển dụng." });
            }
            await _employerRepository.DeleteAsync(id);
            return Ok(new { message = "Xóa nhà tuyển dụng thành công." });

        }
        [HttpPost("Approve")]
        public async Task<IActionResult> ApproveApplication([FromQuery] Guid jobId, [FromQuery] Guid userId)
        {
            if(jobId == Guid.Empty || userId == Guid.Empty)
            {
                return BadRequest(new { message = "JobId hoặc UserId không hợp lệ." });
            }
            var application = await _context.Applications
                .Include(a=>a.Job)
                .ThenInclude(a => a.Employer)
                .Include(a=>a.CandidateProfile)
                .FirstOrDefaultAsync(a => a.Job_ID == jobId && a.User_ID == userId);
            if(application == null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy đơn ứng tuyển."
                });
            }
            application.Status = "approved";
            _context.Applications.Update(application);
            var employerUserId = application.Job.Employer.UserID.Value;

            var chatRoomExists = await _context.ChatRooms.AnyAsync(r =>
                r.Job_ID == jobId &&
                r.CandidateUser_ID == userId &&
                r.EmployerUser_ID == employerUserId
            );

            if (!chatRoomExists)
            {
                var chatRoom = new ChatRoom
                {
                    Job_ID = jobId, 
                    CandidateUser_ID = userId,
                    EmployerUser_ID = employerUserId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.ChatRooms.Add(chatRoom);
            }
            var notification = new Notification
            {
                Receiver_ID = userId,
                Title = "Thông báo duyệt hồ sơ",
                Message = $@"
                  Chúc mừng bạn! Hồ sơ ứng tuyển của bạn đã được duyệt bởi nhà tuyển dụng.<br/>
                  Bạn chính thức được mời tham gia phỏng vấn vào vị trí <strong>{application.Job.JobTitle}</strong> tại công ty <strong>{application.Job.Employer.CompanyName}</strong>.<br/>
                  Vui lòng <strong>kiểm tra điện thoại thường xuyên</strong> để nhận thông tin cụ thể về lịch phỏng vấn từ bộ phận nhân sự.
                  Vui lòng truy cập web hoặc app để xem lại chi tiết công việc
                  Bây giờ bạn có thể chat với nhà tuyển dụng<br/>
                  Hãy chuẩn bị thật tốt và sẵn sàng cho bước tiếp theo. Chúc bạn thành công!",
                SentDate = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            var htmlContent = System.IO.File.ReadAllText("wwwroot/email_templates/approved.html");
            htmlContent = htmlContent.Replace("{{JobTitle}}", application.Job.JobTitle)
                                      .Replace("{{CandidateName}}", application.CandidateProfile.UserName)
                                     .Replace("{{CompanyName}}", application.Job.Employer.CompanyName);                      
            await _emailSender.SendEmailAsync(application.CandidateProfile.UserEmail, "[Website Tìm Việc] Hồ sơ ứng tuyển của bạn đã được duyệt", htmlContent);
            return Ok(new { message = "Duyệt hồ sơ và gửi thông báo thành công." });
        }
        [HttpPost("Reject")]
        public async Task<IActionResult> RejectApplication([FromQuery] Guid jobId, [FromQuery] Guid userId)
        {
            if(jobId == Guid.Empty || userId == Guid.Empty)
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
                return NotFound(new
                {
                    message = "Không tìm thấy hồ sơ ứng tuyển"
                });
            }
            app.Status = "rejected";
            _context.Applications.Update(app);
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
            return Ok(new { message = "Từ chối hồ sơ và gửi thông báo thành công." });
        }
        [HttpGet("search")]
        public async Task<IActionResult> SearchCandidates(
            Guid jobId,
        string? keyword,
        string? location,
        string? skill,
        string? salary,
        string? education,
        string? experienceYear,
        string? experience,
        string? desiredJob)
        {
            var candidates = _context.Applications
                .Include(a => a.Job)
                .Include(a => a.CandidateProfile)
                .AsQueryable();
            candidates = candidates.Where(c => c.Job_ID == jobId);

            if (!string.IsNullOrEmpty(keyword))
            {
                candidates= candidates.Where(
                    c =>
                    c.CandidateProfile.UserDesiredJob.Contains(keyword) ||
                    c.CandidateProfile.CareerObjective.Contains(keyword) ||
                    c.CandidateProfile.Experience.Contains(keyword) ||
                    c.CandidateProfile.Education.Contains(keyword) ||
                    c.CandidateProfile.SoftSkill.Contains(keyword) ||
                    c.CandidateProfile.Language.Contains(keyword)
                );
                
            }   
            if (!string.IsNullOrEmpty(location))
            {
                candidates = candidates.Where(c => c.CandidateProfile.UserAddress.Contains(location));
            }

            if (!string.IsNullOrEmpty(skill))
            {
                candidates = candidates.Where(c =>
                    c.CandidateProfile.SoftSkill.Contains(skill) ||
                    c.CandidateProfile.Language.Contains(skill));
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
            if (!string.IsNullOrEmpty(experience))
            {
                candidates = candidates.Where(c => c.CandidateProfile.Experience.Contains(experience));
            }
            if (!string.IsNullOrEmpty(desiredJob))
            {
                candidates = candidates.Where(c => c.CandidateProfile.UserDesiredJob.Contains(desiredJob));
            }
            var candidateList = candidates.Select(a => new
            {
                a.Job_ID,
                a.CandidateProfile.ID,
                a.CandidateProfile.UserName,
                a.Job.JobTitle,
                a.CandidateProfile.UserDesiredJob,
                a.CandidateProfile.Education,
                a.CandidateProfile.ExperienceYear,
                a.CandidateProfile.Experience,
                a.CandidateProfile.SoftSkill,
                a.CandidateProfile.Language,
                a.CandidateProfile.UserAddress,
                a.CandidateProfile.DesiredSalary,
                a.CandidateProfile.UserAvatar,
                a.Status
            });
            return Ok(new
            {
                Data = candidateList
            });
        }
    }
}
  