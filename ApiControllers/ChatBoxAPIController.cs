using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using web_jobs.Models;
using web_jobs.Repository;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

using System.Net.Http;          // ✅ thêm
using System.Text;              // ✅ thêm
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

namespace web_jobs.ApiControllers.ChatBoxModels
{
    public class AskRequest
    {
        public string CurrentMessage { get; set; }
        public List<HistoryMessage> History { get; set; }
    }

    public class HistoryMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
    }
}

namespace web_jobs.ApiControllers
{
    using web_jobs.ApiControllers.ChatBoxModels;

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChatBoxAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJobRepository _jobRepository;
        private readonly ICandidateProfileRepository _candidateProfileRepository;
        private readonly IEmployerRepository _employerRepository;
        private readonly IConfiguration _config;

        private readonly List<string> _contextKeywords = new()
        {
            "tìm việc", "gợi ý", "công việc", "việc làm",
            "hồ sơ", "profile", "cv",
            "công ty", "nhà tuyển dụng", "employer"
        };

        public ChatBoxAPIController(
            ApplicationDbContext context,
            IJobRepository jobRepository,
            ICandidateProfileRepository candidateProfileRepository,
            IEmployerRepository employerRepository,
            IConfiguration config)
        {
            _context = context;
            _jobRepository = jobRepository;
            _candidateProfileRepository = candidateProfileRepository;
            _employerRepository = employerRepository;
            _config = config;
        }

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

        private bool ContainsJobKeywords(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            var lowerMessage = message.ToLower();
            return _contextKeywords.Any(keyword => lowerMessage.Contains(keyword));
        }

        // ✅ gọi Gemini REST
        private async Task<string> CallGeminiREST(string prompt)
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("Gemini API key chưa cấu hình.");

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
            var text = data?.candidates?[0]?.content?.parts?[0]?.text;
            return text != null ? text.ToString() : "Gemini không trả nội dung.";
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CurrentMessage))
                return BadRequest(new { message = "Yêu cầu không hợp lệ." });

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để sử dụng chatbox." });

            var systemPromptText = @"
Bạn là JobAssistant AI — một trợ lý nghề nghiệp thông minh.
Nhiệm vụ của bạn:
- Phân tích hồ sơ ứng viên và danh sách công việc khi được cung cấp.
- Gợi ý công việc phù hợp nhất.
- Trả lời các câu hỏi chung và duy trì cuộc trò chuyện thân thiện.
- Luôn luôn trả lời bằng tiếng Việt.
- Tuyệt đối KHÔNG hiển thị các ID nội bộ (GUID).
";

            // ✅ Inject context
            string contextToInject = "";
            bool isFirstMessage = request.History == null || request.History.Count == 0;
            bool needsContext = ContainsJobKeywords(request.CurrentMessage);

            if (isFirstMessage || needsContext)
            {
                var profile = await _candidateProfileRepository.GetByUserIdAsync(userId.Value);
                if (profile == null)
                    return BadRequest(new { message = "Không tìm thấy hồ sơ ứng viên." });

                var listEmployer = await _employerRepository.GetAllEmployersAsync();
                var listJob = await _jobRepository.GetApprovedJobsAsync();

                contextToInject = $@"
--- BỐI CẢNH (Chỉ dùng cho câu hỏi này) ---
Hồ sơ ứng viên:
{JsonConvert.SerializeObject(profile, Formatting.Indented)}

Danh sách nhà tuyển dụng (10 mẫu):
{JsonConvert.SerializeObject(listEmployer.Take(10), Formatting.Indented)}

Danh sách công việc hiện có:
{JsonConvert.SerializeObject(listJob.Take(20), Formatting.Indented)}
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
