using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_jobs.Models;

namespace web_jobs.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        public NotificationController(ApplicationDbContext context)
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
        // Xem thông báo chưa đọc
        public async Task<IActionResult> Unread()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();


            var notifications = await _context.Notifications
                .Where(n => n.Receiver_ID == userId && !n.IsRead)
                //Sắp xếp theo thời gian gửi (SentDate) giảm dần (mới nhất ở trên).
                .OrderByDescending(n => n.SentDate) // Giả sử bạn có trường CreatedAt để sắp xếp
                .ToListAsync();

            ViewData["Title"] = "Thông báo chưa đọc";
            return View("MyNotifications", notifications);
        }
        // Xem thông báo đã đọc
        public async Task<IActionResult> Read()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Challenge();

         

            var notifications = await _context.Notifications
                .Where(n => n.Receiver_ID == userId && n.IsRead)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();

            ViewData["Title"] = "Thông báo đã đọc";
            return View("MyNotifications",notifications);
        }
        public async Task<IActionResult> Detail(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound();
            }
            // Đánh dấu thông báo là đã đọc
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }
            return View(notification);
        }
        public async Task<IActionResult> MyNotifications(int page = 1, int pageSize = 5)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Bạn cần đăng nhập để xem thông báo.";
                return RedirectToAction("Index", "Home");
            }
            //Lấy tất cả thông báo của người dùng hiện tại, sắp xếp từ mới đến cũ.
            //Dùng ToListAsync() để lấy toàn bộ danh sách trước khi phân trang.
            var allNotifications = await _context.Notifications
                .Where(n => n.Receiver_ID == userId.Value)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();

            var total = allNotifications.Count;
            //Math.Ceiling(...): làm tròn lên, vì dù dư 1 thông báo cũng cần thêm 1 trang.

           // total = 12, pageSize = 5
           //→ 12 / 5 = 2.4 → Math.Ceiling(2.4) = 3 → totalPages = 3
            var totalPages = (int)Math.Ceiling((double)total / pageSize);
               //page: là trang hiện tại (mặc định là 1).
              // Skip(...): bỏ qua số thông báo đã xuất hiện ở những trang trước.
             //Take(pageSize): lấy đúng số lượng thông báo cho trang hiện tại.
            //  page = 2, pageSize = 5 
            //→ Skip((2 - 1) * 5) = Skip(5)
           //→ Tức là bỏ 5 cái đầu, lấy 5 cái tiếp theo → hiện trang 2
            var notifications = allNotifications.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(notifications);
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
