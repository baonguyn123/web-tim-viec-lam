using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;

using web_jobs.Repository;

namespace web_jobs.ApiControllers.EmployerChatBoxModels
{
    /// <summary>
    /// Request từ client gửi lên cho chatbot nhà tuyển dụng
    /// </summary>
    public class EmployerAskRequest
    {
        public string CurrentMessage { get; set; }
        public List<EmployerHistoryMessage> History { get; set; }

        public Guid? JobId { get; set; }
        public Guid? CandidateProfileId { get; set; }
    }

    /// <summary>
    /// Một tin nhắn trong lịch sử chat Employer
    /// </summary>
    public class EmployerHistoryMessage
    {
        public string Role { get; set; } // "employer" hoặc "model"
        public string Text { get; set; }
    }
}

namespace web_jobs.ApiControllers
{
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using web_jobs.ApiControllers.EmployerChatBoxModels;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]// nếu muốn bắt login thì mở lại
    public class EmployerChatBoxAPIController : ControllerBase
    {
        private readonly IEmployerRepository _employerRepository;
        private readonly IJobRepository _jobRepository;
        private readonly ICandidateProfileRepository _candidateProfileRepository;
        private readonly IConfiguration _config;

        private readonly List<string> _contextKeywords = new()
        {
            "ứng viên", "candidate", "lọc", "gợi ý", "job", "tin tuyển dụng",
            "jd", "mức lương", "đăng tin", "công ty", "nhà tuyển dụng", "employer",
            "tóm tắt", "cv", "hồ sơ", "pending", "approved", "rejected"
        };

        public EmployerChatBoxAPIController(
            IEmployerRepository employerRepository,
            IJobRepository jobRepository,
            ICandidateProfileRepository candidateProfileRepository,
            IConfiguration config)
        {
            _employerRepository = employerRepository;
            _jobRepository = jobRepository;
            _candidateProfileRepository = candidateProfileRepository;
            _config = config;
        }

        // ==========================
        // ✅ Helper: current userId
        // ==========================
        private Guid? GetCurrentUserId()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out var userId))
                    return userId;
            }
            return null;
        }

        private bool ContainsEmployerKeywords(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            var lowerMessage = message.ToLower();
            return _contextKeywords.Any(keyword => lowerMessage.Contains(keyword));
        }

        // ==========================
        // ✅ Helper: rút gọn text
        // ==========================
        private string Shorten(string? text, int maxLen = 200)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        // ==========================
        // ✅ 1) GỌI GEMINI BẰNG REST (ỔN ĐỊNH)
        // ==========================
        private async Task<string> CallGeminiREST(string prompt)
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("Gemini API key chưa cấu hình.");

            // ✅ Dùng endpoint v1 + model đúng
            var url =
   $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";



            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            var res = await client.PostAsync(url,
                new StringContent(json, Encoding.UTF8, "application/json"));

            var raw = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception($"Gemini REST lỗi: {res.StatusCode} - {raw}");

            dynamic data = JsonConvert.DeserializeObject(raw);

            // ✅ lấy text an toàn
            var text = data?.candidates?[0]?.content?.parts?[0]?.text;
            return text != null ? text.ToString() : "Gemini không trả nội dung.";
        }

        // ==========================
        // ✅ TEST GEMINI (không cần login)
        // ==========================
        [AllowAnonymous]
        [HttpGet("test-gemini")]
        public async Task<IActionResult> TestGemini()
        {
            try
            {
                var apiKey = _config["Gemini:ApiKey"];
                Console.WriteLine(">>> apiKey = " + apiKey);

                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { message = "API key đang NULL hoặc rỗng trong config" });

                var ans = await CallGeminiREST("Xin chào");
                return Ok(new { message = ans });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Gemini REST lỗi", detail = ex.Message });
            }
        }


        // ==========================
        // ✅ ASK EMPLOYER CHATBOT
        // ==========================
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] EmployerAskRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CurrentMessage))
                return BadRequest(new { message = "Yêu cầu không hợp lệ." });

            // ✅ lấy userId (dev fix)
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để sử dụng chatbox." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            //}

            // ✅ lấy employer
            var employer = await _employerRepository.GetByUserIdAsync(userId.Value);
            if (employer == null)
                return BadRequest(new { message = "Không tìm thấy hồ sơ nhà tuyển dụng." });

            // ✅ system prompt
            var systemPromptText = @"
Bạn là EmployerAssistant AI — trợ lý tuyển dụng thông minh cho nhà tuyển dụng.

========================
QUY TẮC BẮT BUỘC
========================
1) Dữ liệu hệ thống được cung cấp trong phần BỐI CẢNH (context).
   Bạn PHẢI sử dụng dữ liệu đó để phân tích và trả lời.

2) Bạn CÓ QUYỀN đọc và sử dụng dữ liệu trong context.
   Tuyệt đối KHÔNG được nói:
   - 'tôi không có quyền truy cập'
   - 'tôi không đọc được hồ sơ'
   - 'tôi không có dữ liệu'
   nếu dữ liệu đã được cung cấp trong context.

3) Định nghĩa CV:
   Nếu trong context có các field như:
   UserPosition, CareerObjective, Education, Experience, SoftSkill, Language, DesiredSalary
   => ĐÓ CHÍNH LÀ CV.
   Bạn PHẢI tóm tắt dựa trên các field đó.

4) Nếu thiếu CV chi tiết (context chỉ có UserName/Status/ApplyDate...) thì phải trả lời:
   'Context chưa có CV chi tiết, vui lòng chọn JobId hoặc CandidateProfileId để xem đầy đủ.'

5) Tuyệt đối KHÔNG hiển thị GUID / JobId / CandidateId dưới bất kỳ hình thức nào.
   Khi cần tham chiếu:
   - Job: dùng JobTitle
   - Ứng viên: dùng UserName hoặc mã UV-001, UV-002 do bạn tự tạo.

6) Không được bịa dữ liệu ngoài context.

========================
NHIỆM VỤ
========================
1) Gợi ý ứng viên phù hợp nhất cho job (nêu rõ lý do).
2) Tóm tắt và đánh giá CV ứng viên.
3) Hỗ trợ tối ưu JD và gợi ý mức lương theo thị trường VN (VND).

========================
FORMAT TRẢ LỜI BẮT BUỘC
========================
- Nếu user hỏi tóm tắt CV, bạn phải trả về:
  1) Tổng quan
  2) Kinh nghiệm
  3) Học vấn
  4) Kỹ năng mềm / Ngoại ngữ
  5) Mức phù hợp với job (0-10)
  6) Kết luận + gợi ý bước tiếp theo
7) Nếu user yêu cầu tóm tắt CV của một ứng viên theo tên,
   nhưng context có nhiều ứng viên,
   bạn phải:
   - liệt kê danh sách ứng viên có trong context (UserName + JobTitle + ApplyDate)
   - yêu cầu user chọn đúng ứng viên
   - KHÔNG được tự đoán.

Luôn trả lời bằng tiếng Việt, chuyên nghiệp nhưng thân thiện.
";

            // ==========================
            // ✅ Inject context khi cần
            // ==========================
            string contextToInject = "";

            bool needsContext =
                ContainsEmployerKeywords(request.CurrentMessage)
                || request.JobId != null
                || request.CandidateProfileId != null
                || (request.History == null || request.History.Count == 0);

            if (needsContext)
            {
                var jobs = await _employerRepository.GetJobsByEmployerIdAsync(employer.ID);

                // ✅ Job summary để tránh self-loop
                var jobsSummary = jobs.Select(j => new
                {
                    j.JobTitle,
                    j.Status,
                    ApplicationsCount = j.Applications != null ? j.Applications.Count : 0
                }).ToList();

                object candidatesContext = null;

                // 1) Nếu có CandidateProfileId → ưu tiên lấy CV 1 ứng viên
                if (request.CandidateProfileId != null)
                {
                    var profile = await _candidateProfileRepository.GetByIdAsync(request.CandidateProfileId.Value);

                    if (profile != null)
                    {
                        candidatesContext = new
                        {
                            profile.UserName,
                            profile.UserPosition,
                            CareerObjective = Shorten(profile.CareerObjective, 200),

                            profile.EducationYear,
                            Education = Shorten(profile.Education, 200),

                            profile.ExperienceYear,
                            Experience = Shorten(profile.Experience, 300),

                            profile.DesiredSalary,
                            profile.UserDesiredJob,

                            profile.Language,
                            SoftSkill = Shorten(profile.SoftSkill, 150),
                        };
                    }
                }
                //  2) Nếu không có CandidateProfileId mà có JobId → lấy list ứng viên apply job
                else if (request.JobId != null)
                {
                    var jobDetail = await _jobRepository.GetJobByIdAsync(request.JobId.Value);

                    if (jobDetail != null)
                    {
                        candidatesContext = jobDetail.Applications?
                            .Where(a => a.CandidateProfile != null)
                            .Select(a => new
                            {
                                a.CandidateProfile.UserName,
                                a.CandidateProfile.UserPosition,
                                CareerObjective = Shorten(a.CandidateProfile.CareerObjective, 200),

                                EducationYear = a.CandidateProfile.EducationYear,
                                Education = Shorten(a.CandidateProfile.Education, 200),

                                ExperienceYear = a.CandidateProfile.ExperienceYear,
                                Experience = Shorten(a.CandidateProfile.Experience, 300),

                                DesiredSalary = a.CandidateProfile.DesiredSalary,
                                DesiredJob = a.CandidateProfile.UserDesiredJob,

                                Language = a.CandidateProfile.Language,
                                SoftSkill = Shorten(a.CandidateProfile.SoftSkill, 150),

                                a.Status,
                                a.ApplyDate
                            })
                            .Take(5)
                            .ToList();
                    }
                }
                //  3) Nếu không có gì hết → lấy ứng viên apply gần nhất
                else
                {
                    var allApplications = jobs
                        .Where(j => j.Applications != null)
                        .SelectMany(j => j.Applications)
                        .ToList();

                                candidatesContext = allApplications
                  .Where(a => a.CandidateProfile != null)
                  .OrderByDescending(a => a.ApplyDate)
                  .Select(a => new
                  {
                      a.CandidateProfile.UserName,
                      a.CandidateProfile.UserPosition,
                      CareerObjective = Shorten(a.CandidateProfile.CareerObjective, 200),

                      EducationYear = a.CandidateProfile.EducationYear,
                      Education = Shorten(a.CandidateProfile.Education, 200),

                      ExperienceYear = a.CandidateProfile.ExperienceYear,
                      Experience = Shorten(a.CandidateProfile.Experience, 300),

                      DesiredSalary = a.CandidateProfile.DesiredSalary,
                      DesiredJob = a.CandidateProfile.UserDesiredJob,

                      Language = a.CandidateProfile.Language,
                      SoftSkill = Shorten(a.CandidateProfile.SoftSkill, 150),

                      a.Status,
                      a.ApplyDate,
                      JobTitle = a.Job != null ? a.Job.JobTitle : ""
      })
      .Take(5)
      .ToList();
                }

                contextToInject = $@"
--- BỐI CẢNH (Chỉ dùng cho câu hỏi này) ---
Bạn PHẢI dùng dữ liệu dưới đây để trả lời. 
Nếu phần ứng viên có các trường (UserPosition, CareerObjective, Education, Experience, SoftSkill, Language...) 
=> ĐÓ CHÍNH LÀ CV và bạn phải tóm tắt dựa trên đó.
Nếu phần ứng viên chỉ có UserName/Status/ApplyDate 
=> Đây chỉ là danh sách ứng viên và chưa có CV chi tiết.

Thông tin nhà tuyển dụng:
Thông tin nhà tuyển dụng:
{JsonConvert.SerializeObject(new { employer.CompanyName }, Formatting.Indented)}

Danh sách job của nhà tuyển dụng (tóm tắt):
{JsonConvert.SerializeObject(jobsSummary, Formatting.Indented)}

Danh sách ứng viên (mẫu):
{JsonConvert.SerializeObject(candidatesContext, Formatting.Indented)}
--- KẾT THÚC BỐI CẢNH ---
";
            }

            // ✅ final prompt
            var finalPrompt = $@"
SYSTEM_PROMPT:
{systemPromptText}

{contextToInject}

USER_QUESTION:
{request.CurrentMessage}
";

            // ✅ Call Gemini REST
            try
            {
                Console.WriteLine(">>> BEFORE Gemini REST");
                var answer = await CallGeminiREST(finalPrompt);
                Console.WriteLine(">>> AFTER Gemini REST");

                return Ok(new { message = answer });
            }
            catch (Exception ex)
            {
                Console.WriteLine(">>> Gemini REST ERROR: " + ex.Message);
                return StatusCode(500, new { message = "Gemini REST lỗi", detail = ex.Message });
            }
        }
    }
}
