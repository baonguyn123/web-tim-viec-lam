using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Models;
using web_jobs.Repository;
using web_jobs.ViewModels;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CandidateProfileAPIController : ControllerBase
    {
        private readonly ICandidateProfileRepository _candidateProfileRepositor;
        private readonly IWebHostEnvironment _env;
        public CandidateProfileAPIController(ICandidateProfileRepository candidateProfileRepositor, IWebHostEnvironment env)
        {
            _candidateProfileRepositor = candidateProfileRepositor;
            _env = env;
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
        [HttpGet("my-profile")]

        public async Task<IActionResult> Index()
        {
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để truy cập hồ sơ ứng viên." });
            }
            //if (userID == null)
            //{
            //    userID = Guid.Parse("725e0dcc-5e28-4e2b-9748-269e054344f5"); // chỉ dev
            //}
            var profile = await _candidateProfileRepositor.GetByUserIdAsync(userID.Value);
            if (profile == null)
            {
                return NotFound(new { message = "Bạn cần tạo hồ sơ ứng viên trước khi sử dụng tính năng này." });
            }
            var completion = CalculateProfileCompletion(profile);

            return Ok(new
            {
                profile,
                completion
            });
        }
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromForm] CandidateProfile candidateProfile, IFormFile avatarFile)
        {
            var userID = GetCurrentUserId();
            if (userID == null)
            {
                return Unauthorized(new { message = "Bạn cần đăng nhập để tạo hồ sơ ứng viên." });
            }
            //if (userID == null)
            //{
            //    userID = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            candidateProfile.UserID = userID.Value;
            if (avatarFile == null || avatarFile.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ảnh đại diện." });
            }
            //Guid.NewGuid().ToString() → tạo ra một chuỗi ID ngẫu nhiên duy nhất
            // Path.GetExtension(avatarFile.FileName)lấy phần đuôi mở rộng của file (ví dụ .jpg, .png, .jpeg).:

            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
            //_env.WebRootPath là đường dẫn thư mục wwwroot trong dự án ASP.NET Core.
            //"images" là tên thư mục con bạn muốn lưu ảnh trong đó.
            //→ Đường dẫn kết hợp sẽ thành:
            // D:\Project\MyApp\wwwroot\images\f47ac10b - 58cc - 4372 - a567 - 0e02b2c3d479.jpg
            //Path.Combine() giúp nối đường dẫn an toàn, tự thêm \ đúng cách cho hệ điều hành.
            string filePath = Path.Combine(_env.WebRootPath, "images", fileName);
            //FileMode.Create có nghĩa là:

            // Nếu file chưa tồn tại → tạo mới.

            //Nếu đã tồn tại → ghi đè.
            //using mở một luồng ghi file (FileStream) đến đường dẫn filePath —
            //thường là chỗ bạn muốn lưu ảnh, ví dụ: wwwroot/uploads/avatars/abc.jpg.
            //FileMode.Create có nghĩa là:
            //Nếu file chưa tồn tại → tạo mới.
            //Nếu đã tồn tại → ghi đè.
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                //await avatarFile.CopyToAsync(stream)
                //là chép nội dung file upload (do người dùng chọn từ form, kiểu IFormFile)
                //vào file mới tạo trong thư mục của bạn
                await avatarFile.CopyToAsync(stream);
            }
            var completion = CalculateProfileCompletion(candidateProfile);
            candidateProfile.UserAvatar = fileName;
            await _candidateProfileRepositor.AddAsync(candidateProfile);

            return Ok(new { message = "Thêm hồ sơ thành công", data = candidateProfile });
        }
        private ProfileCompletionDTO CalculateProfileCompletion(CandidateProfile p)
        {
            int percent = 0;
            var missing = new List<string>();

            bool HasValue(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return false;
                var invalid = new[] { "chưa có", "không có", "none", "n/a" };
                return !invalid.Contains(value.Trim().ToLower());
            }

            // 1. Thông tin cá nhân (20%)
            if (HasValue(p.UserName)
                && HasValue(p.UserEmail)
                && HasValue(p.UserPhone)
                && HasValue(p.UserAvatar))
                percent += 20;
            else
                missing.Add("Thông tin cá nhân");

            // 2. Mục tiêu (15%)
            if (HasValue(p.UserPosition)
                && HasValue(p.CareerObjective)
                && HasValue(p.UserDesiredJob))
                percent += 15;
            else
                missing.Add("Mục tiêu nghề nghiệp");

            // 3. Học vấn (15%)
            if (HasValue(p.Education) && HasValue(p.EducationYear))
                percent += 15;
            else
                missing.Add("Học vấn");

            // 4. Kinh nghiệm (15%)
            if (HasValue(p.Experience) && HasValue(p.ExperienceYear))
                percent += 15;
            else
                missing.Add("Kinh nghiệm");

            // 5. Kỹ năng & ngôn ngữ (15%)
            if (HasValue(p.SoftSkill) && HasValue(p.Language))
                percent += 15;
            else
                missing.Add("Kỹ năng và ngôn ngữ");

            // 6. Chứng chỉ / giải thưởng (10%)
            if (HasValue(p.CertificateName) && HasValue(p.CertificateYear))
                percent += 10;
            else
                missing.Add("Chứng chỉ ");
            if (HasValue(p.PrizeDesc) && HasValue(p.PrizeYear))
                percent += 10;
            else
                missing.Add("Giải thưởng");

            return new ProfileCompletionDTO
            {
                Percent = percent,
                MissingItems = missing
            };
        }


        [HttpPost("update")]
        public async Task<IActionResult> Update(Guid id, [FromForm] CandidateProfile candidateProfile, IFormFile? avatarFile)
        {
            var existingProfile = await _candidateProfileRepositor.GetByIdAsync(id);
            if (existingProfile == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng viên." });

            var userID = GetCurrentUserId();
            if (userID == null || userID != existingProfile.UserID)
                return Unauthorized(new { message = "Bạn không có quyền cập nhật hồ sơ này." });

            // Xử lý avatar mới
            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(existingProfile.UserAvatar))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images", existingProfile.UserAvatar);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                string filePath = Path.Combine(_env.WebRootPath, "images", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await avatarFile.CopyToAsync(stream);

                existingProfile.UserAvatar = fileName;
            }

            // Chỉ cập nhật những trường có dữ liệu mới
            if (!string.IsNullOrEmpty(candidateProfile.UserName))
                existingProfile.UserName = candidateProfile.UserName;

            if (!string.IsNullOrEmpty(candidateProfile.UserEmail))
                existingProfile.UserEmail = candidateProfile.UserEmail;

            if (!string.IsNullOrEmpty(candidateProfile.UserPhone))
                existingProfile.UserPhone = candidateProfile.UserPhone;

            if (!string.IsNullOrEmpty(candidateProfile.UserAddress))
                existingProfile.UserAddress = candidateProfile.UserAddress;

            if (!string.IsNullOrEmpty(candidateProfile.UserPosition))
                existingProfile.UserPosition = candidateProfile.UserPosition;

            if (candidateProfile.UserBirthDate.HasValue)
                existingProfile.UserBirthDate = candidateProfile.UserBirthDate;

            if (!string.IsNullOrEmpty(candidateProfile.UserFacebook))
                existingProfile.UserFacebook = candidateProfile.UserFacebook;

            if (!string.IsNullOrEmpty(candidateProfile.UserDesiredJob))
                existingProfile.UserDesiredJob = candidateProfile.UserDesiredJob;

            if (!string.IsNullOrEmpty(candidateProfile.DesiredSalary))
                existingProfile.DesiredSalary = candidateProfile.DesiredSalary;

            if (!string.IsNullOrEmpty(candidateProfile.ExperienceYear))
                existingProfile.ExperienceYear = candidateProfile.ExperienceYear;

            if (!string.IsNullOrEmpty(candidateProfile.Experience))
                existingProfile.Experience = candidateProfile.Experience;

            if (!string.IsNullOrEmpty(candidateProfile.CertificateYear))
                existingProfile.CertificateYear = candidateProfile.CertificateYear;

            if (!string.IsNullOrEmpty(candidateProfile.CertificateName))
                existingProfile.CertificateName = candidateProfile.CertificateName;

            if (!string.IsNullOrEmpty(candidateProfile.PrizeYear))
                existingProfile.PrizeYear = candidateProfile.PrizeYear;

            if (!string.IsNullOrEmpty(candidateProfile.PrizeDesc))
                existingProfile.PrizeDesc = candidateProfile.PrizeDesc;

            if (!string.IsNullOrEmpty(candidateProfile.Language))
                existingProfile.Language = candidateProfile.Language;

            if (!string.IsNullOrEmpty(candidateProfile.SoftSkill))
                existingProfile.SoftSkill = candidateProfile.SoftSkill;

            if (!string.IsNullOrEmpty(candidateProfile.Interest))
                existingProfile.Interest = candidateProfile.Interest;

            if (!string.IsNullOrEmpty(candidateProfile.CareerObjective))
                existingProfile.CareerObjective = candidateProfile.CareerObjective;

            if (!string.IsNullOrEmpty(candidateProfile.Education))
                existingProfile.Education = candidateProfile.Education;

            if (!string.IsNullOrEmpty(candidateProfile.EducationYear))
                existingProfile.EducationYear = candidateProfile.EducationYear;

            await _candidateProfileRepositor.UpdateAsync(existingProfile);

            return Ok(new { message = "Cập nhật hồ sơ thành công", data = existingProfile });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var profile = await _candidateProfileRepositor.GetByIdAsync(id);
            if (profile == null)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ ứng viên." });
            }
            var userID = GetCurrentUserId();
            if (userID == null || userID != profile.UserID)
            {
                return Unauthorized(new { message = "Bạn không có quyền xóa hồ sơ này." });
            }
            await _candidateProfileRepositor.DeleteAsync(id);
            return Ok(new { message = "Xóa hồ sơ thành công." });
        }

        [HttpPost("upload-cv")]
        public async Task<IActionResult> UploadCv(IFormFile cvFile)
        {
            var userID = GetCurrentUserId();
            if (userID == null)
                return Unauthorized();
            //if (userID == null)
            //{
            //    userID = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}

            if (cvFile == null || cvFile.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file CV" });

            // chỉ cho PDF
            if (!cvFile.FileName.EndsWith(".pdf"))
                return BadRequest(new { message = "Chỉ chấp nhận file PDF" });

            var fileName = $"{userID}_{Guid.NewGuid()}.pdf";
            var folderPath = Path.Combine(_env.WebRootPath, "cvs");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await cvFile.CopyToAsync(stream);
            }

            // cập nhật DB
            var profile = await _candidateProfileRepositor.GetByUserIdAsync(userID.Value);
            if (profile == null)
                return NotFound("Không tìm thấy hồ sơ");

            profile.CvFile = fileName;
            await _candidateProfileRepositor.UpdateAsync(profile);

            return Ok(new
            {
                message = "Upload CV thành công",
                file = fileName
            });
        }

        [HttpGet("cv")]
        public async Task<IActionResult> GetMyCv()
        {
            var userID = GetCurrentUserId();
            if (userID == null)
                return Unauthorized();

            var profile = await _candidateProfileRepositor.GetByUserIdAsync(userID.Value);
            if (profile == null || string.IsNullOrEmpty(profile.CvFile))
                return NotFound("Chưa có CV");

            var path = Path.Combine(_env.WebRootPath, "cvs", profile.CvFile);

            if (!System.IO.File.Exists(path))
                return NotFound("File không tồn tại");

            return PhysicalFile(path, "application/pdf", "CV.pdf");
        }


    }
}

