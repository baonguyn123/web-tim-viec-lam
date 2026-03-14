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
    public class NotificationAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public NotificationAPIController(ApplicationDbContext context)
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
        [HttpGet("unread")]
        public async Task<IActionResult> Unread()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem thông báo." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var notification = await _context.Notifications
                .Where(n => n.Receiver_ID == userId && !n.IsRead)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();
            return Ok(new
            {
                Notifications = notification,
                title = "Thông báo chưa đọc",
                count = notification.Count,
            });
        }
        [HttpGet("read")]
        public async Task<IActionResult> Read()

        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem thông báo." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var notification = await _context.Notifications
                .Where(n => n.Receiver_ID == userId && n.IsRead)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();
            return Ok(new
            {
                Notifications = notification,
                title = "Thông báo đã đọc",
                count = notification.Count,
            });
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem thông báo." });
            }
            //if (userId == null)
            //{
            //    userId = Guid.Parse("d0042507-e9bc-4d34-8a1d-22bed891573f"); // chỉ dev
            //}
            var notification = await _context.Notifications.FindAsync(id);
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }
            return Ok(new
            {
                Notification = notification
            });
        }

        [HttpGet("Index")]
        public async Task<IActionResult> MyNotifications(int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem thông báo." });
            //if (userId == null)
            //{
            //    userId = Guid.Parse("6e538194-2e9e-40cf-b7a6-e87474c8878a"); // chỉ dev
            //}
            var notifications = await _context.Notifications.Where(n => n.Receiver_ID == userId)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();
            var totalNotify = notifications.Count();
            var totalPages = (int)Math.Ceiling((double)totalNotify / pageSize);
            var listPage = notifications.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Ok(new
            {
                notifications = listPage,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalNotify = totalNotify

            });

        }
    }
}
