    using System.Security.Claims;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using web_jobs.Models;

    namespace web_jobs.ApiControllers
    {
        [Route("api/[controller]")]
        [ApiController]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public class MessageAPIController : ControllerBase
        {
            private readonly ApplicationDbContext _context;
            public MessageAPIController(ApplicationDbContext context)
            {
                _context = context;
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
            [HttpPost("send")]
            public async Task<IActionResult> SendMessage(
                [FromQuery] Guid chatRoomId,
                [FromBody] string message)
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();
                //if (userId == null)
                //{
                //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
                //}

                var room = await _context.ChatRooms.FindAsync(chatRoomId);
                if (room == null || !room.IsActive)
                    return BadRequest("Phòng chat không tồn tại hoặc chưa được mở.");

                if (userId != room.CandidateUser_ID && userId != room.EmployerUser_ID)
                    return Forbid();

                var chat = new Chat
                {
                    ChatRoomId = chatRoomId,
                    SenderUser_ID = userId.Value,
                    MessageText = message
                };

                _context.Chats.Add(chat);

                room.LastMessage = message;
                room.LastMessageAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Gửi tin nhắn thành công." });
            }
        [HttpGet("messages")]
        public async Task<IActionResult> GetMessages([FromQuery] Guid chatRoomId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var room = await _context.ChatRooms.FindAsync(chatRoomId);
            if (room == null)
                return NotFound();

            var messages = await _context.Chats
                .Where(c => c.ChatRoomId == chatRoomId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.MessageText,
                    c.CreatedAt,
                    IsMine = c.SenderUser_ID == userId
                })
                .ToListAsync();

            return Ok(messages);
        }


        [HttpGet("inbox")]
        public async Task<IActionResult> GetInbox()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var rooms = await _context.ChatRooms
                .Where(r => r.CandidateUser_ID == userId || r.EmployerUser_ID == userId)
                .OrderByDescending(r => r.LastMessageAt)
                .Select(r => new
                {
                    r.Id,
                    r.LastMessage,
                    r.LastMessageAt,

                    IsEmployer = r.EmployerUser_ID == userId,

                    // Nếu user hiện tại là employer → lấy info candidate
                    OtherName = r.EmployerUser_ID == userId
                        ? _context.CandidateProfiles
                            .Where(c => c.UserID == r.CandidateUser_ID)
                            .Select(c => c.UserName)
                            .FirstOrDefault()
                        : _context.Employers
                            .Where(e => e.UserID == r.EmployerUser_ID)
                            .Select(e => e.CompanyName)
                            .FirstOrDefault(),

                    OtherAvatar = r.EmployerUser_ID == userId
                        ? _context.CandidateProfiles
                            .Where(c => c.UserID == r.CandidateUser_ID)
                            .Select(c => c.UserAvatar)
                            .FirstOrDefault()
                        : _context.Employers
                            .Where(e => e.UserID == r.EmployerUser_ID)
                            .Select(e => e.CompanyLogo)
                            .FirstOrDefault(),
                })
                .ToListAsync();

            return Ok(rooms);
        }

    }
}



