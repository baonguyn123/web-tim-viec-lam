using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Dtos;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
   [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployerAPIController : ControllerBase
    {
        private readonly IJobRepository _jobRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IJobTypeRepository _jobTypeRepository;
        private readonly IEmployerRepository _employerRepository;

        public EmployerAPIController(IJobRepository jobRepository, ICategoryRepository categoryRepository, IJobTypeRepository jobTypeRepository, IEmployerRepository employerRepository)
        {
            _jobRepository = jobRepository;
            _categoryRepository = categoryRepository;
            _jobTypeRepository = jobTypeRepository;
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
        [HttpGet("index")]
        public async Task<IActionResult> index(string filter = "all")
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
            var jobs = (await _jobRepository.GetAllAsync())
                  .Where(j => j.Employer.UserID == userId)
                  .ToList();
            var start = new
            {
                approved = jobs.Count(j => j.Status == "approved" ),
                pending = jobs.Count(j => j.Status == "pending" ),
                rejected = jobs.Count(j => j.Status == "rejected" ),
                exprired = jobs.Count(j=>j.ApplicationDeadline<DateTime.Now),
                total = jobs.Count
            };
            if (!string.IsNullOrEmpty(filter) && filter.ToLower() != "all")
            {
                if (filter.ToLower() == "exprired")
                {
                    // chỉ filter job hết hạn, không cần so status == "exprired"
                    jobs = jobs.Where(j => j.ApplicationDeadline < DateTime.Now).ToList();
                }
                else
                {
                    // filter theo status
                    var statusFilter = filter.ToLower();
                    jobs = jobs.Where(j => j.Status.ToLower() == statusFilter).ToList();
                }
            }
            var result = jobs.Select(j => new
            {
                j.Id,
                j.JobTitle,
                j.JobDescription,
                j.Requirements,
                j.Salary,
                j.Locate,
                j.ApplicationDeadline,
                j.JobType.JobType_Name,
                j.Category.Name,
                j.Status,
                j.Applications.Count,
                dayleft = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                isExpired = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days < 0 : false,
            }).ToList();
            return Ok(new
            {
                jobs = result,
                start
            });
        }
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody]JobAddDTO jobaddDTO)
        {   
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // dev fallback
            //}
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if(employer ==null)
            {
                return NotFound(new { message = "Không tìm thấy nhà tuyển dụng tương ứng." });
            }
            if(employer.Status == "rejected")
            {
                return BadRequest(new { message = "Công ty của bạn đã bị từ chối, không thể đăng tin tuyển dụng." });
            }
            if (employer.Status != "approved")
            {
                return BadRequest(new
                {
                    message = "Công ty của bạn chưa được duyệt, không thể đăng tin "
                });
            }
            var job = new Job
            {
                EmployerID = employer.ID,
                JobTitle = jobaddDTO.JobTitle,
                JobDescription = jobaddDTO.JobDescription,
                Requirements = jobaddDTO.Requirements,
                Salary = jobaddDTO.Salary,
                Benefits = jobaddDTO.Benefits,
                JobTypeId = jobaddDTO.JobTypeId,
                PostedDate = DateTime.Now,
                ApplicationDeadline = jobaddDTO.ApplicationDeadline,
                Locate = jobaddDTO.Locate,
                CategoryId = jobaddDTO.CategoryId,
                Status = "pending" // Mặc định là pending khi thêm mới
            };
            await _jobRepository.AddAsync(job);
            return Ok(new
            {
                message = "Đăng tin tuyển dụng thành công, vui lòng chờ duyệt.",
                job
            });

        }
        [HttpGet("id")]
        public async Task<IActionResult>Detail (Guid id)
        {
            var jobs = await _jobRepository.GetByIdAsync(id);
            if(jobs==null)
            {
                return NotFound(new { message = "Không tìm thấy công việc." });
            }
            return Ok(jobs);

        }
        [HttpPost("{jobId}")]
        public async Task<IActionResult> Update(Guid jobId, [FromForm] JobUpdateDTO jobupdateDTO)
        {
            // 1 Kiểm tra ID
            if (jobId != jobupdateDTO.Id)
            {
                return NotFound(new { message = "ID công việc không khớp." });
            }

            // 2️⃣ Lấy UserID hiện tại
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Người dùng chưa đăng nhập" });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // dev fallback
            //}
            // Lấy employer theo userId
         

            // 4️⃣ Lấy job từ DB (AsNoTracking để tránh proxy object)
            var jobFromDb = await _jobRepository.GetByIdAsync(jobId);
            if (jobFromDb == null)
            {
                return NotFound(new { message = "Không tìm thấy công việc." });
            }
            if(!string.IsNullOrEmpty(jobupdateDTO.JobTitle))
                jobFromDb.JobTitle = jobupdateDTO.JobTitle;
            if (!string.IsNullOrEmpty(jobupdateDTO.JobDescription))
                jobFromDb.JobDescription = jobupdateDTO.JobDescription;
            if (!string.IsNullOrEmpty(jobupdateDTO.Requirements))
                jobFromDb.Requirements = jobupdateDTO.Requirements;
            if (!string.IsNullOrEmpty(jobupdateDTO.Salary))
                jobFromDb.Salary = jobupdateDTO.Salary;
            if (!string.IsNullOrEmpty(jobupdateDTO.Benefits))
                jobFromDb.Benefits = jobupdateDTO.Benefits;
            if(!string.IsNullOrEmpty(jobupdateDTO.Salary))
                jobFromDb.Salary = jobupdateDTO.Salary;
            if (!string.IsNullOrEmpty(jobupdateDTO.Locate))
                jobFromDb.Locate = jobupdateDTO.Locate;
            if (jobupdateDTO.ApplicationDeadline != null)
                jobFromDb.ApplicationDeadline = jobupdateDTO.ApplicationDeadline;
            if (jobupdateDTO.JobTypeId != 0)
                jobFromDb.JobTypeId = jobupdateDTO.JobTypeId;
            if (jobupdateDTO.CategoryId != 0)
                jobFromDb.CategoryId = jobupdateDTO.CategoryId;
            // 6️⃣ Lưu thay đổi
            await _jobRepository.UpdateAsync(jobFromDb);
            // 7️⃣ Load lại dữ liệu đầy đủ (có Category, JobType)
          
            // 8️⃣ Tạo object rút gọn để trả về
            var result = new
            {
                jobFromDb.Id,
                jobFromDb.EmployerID,
                jobFromDb.CategoryId,
                jobFromDb.JobTypeId,
                jobFromDb.JobTitle,
                jobFromDb.JobDescription,
                jobFromDb.Requirements,
                jobFromDb.Salary,
                jobFromDb.Locate,
                jobFromDb.ApplicationDeadline,
                jobFromDb.PostedDate,
                jobFromDb.Benefits,
                JobTypeName = jobFromDb.JobType?.JobType_Name,
                CategoryName = jobFromDb.Category?.Name,
                jobFromDb.Status
            };
            return Ok(new
            {
                message = "Cập nhật công việc thành công.",
                updatedJob = result
            });

        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var job = await _jobRepository.GetByIdAsync(id);
            if(job==null)
            {
                return NotFound(new
                {
                    message = "Không tìm thấy công việc cần xóa"
                });
            }
            var userId = GetCurrentUserId();
            if(userId == null)
            {
                return Unauthorized(new
                {
                    message = "Bạn cần đăng nhập để sử dụng tính năng này"
                });
            }
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if(employer == null || job.EmployerID !=  employer.ID)
            {
                return Forbid("Bạn không có quyền xóa công việc này");
            }
            await _jobRepository.DeleteAsync(id);
            return Ok(new { message = "Xóa công việc thành công." });

        }


    }
}
