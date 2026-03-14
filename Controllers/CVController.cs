using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Models;

namespace web_jobs.Controllers
{
    public class CVController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CVController(ApplicationDbContext context)
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
        public IActionResult ViewCV(Guid id)
        {
           var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Bạn cần đăng nhập để xem hồ sơ ứng viên.";
                return RedirectToAction("Login", "Account");
            }
            var profile = _context.CandidateProfiles.FirstOrDefault(p => p.UserID == userId.Value);
            if (profile == null)
            {
                TempData["Message"] = "Hồ sơ ứng viên không tồn tại.";
                return RedirectToAction("Index", "Home");
            }
            string viewName = profile.CvLayout switch
            {
                1 => "Classic",
                2 => "Timeline",
                3 => "Creative",
                4 => "Professional",
                5 => "Minimal",
                _ => "Classic" // Mặc định nếu không có layout phù hợp
            };
            return View(viewName, profile);
        }
        public IActionResult ChooseLayout(Guid id)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Bạn cần đăng nhập để xem hồ sơ ứng viên.";
                return RedirectToAction("Login", "Account");
            }
            var profile = _context.CandidateProfiles.FirstOrDefault(p => p.UserID == userId.Value);
            if (profile == null)
            {
                TempData["Message"] = "Hồ sơ ứng viên không tồn tại.";
                return RedirectToAction("Index", "Home");
            }
            return View(profile);
        }
        [HttpPost]
        public IActionResult ChooseLayout(Guid id, int layout)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Message"] = "Bạn cần đăng nhập để xem hồ sơ ứng viên.";
                return RedirectToAction("Login", "Account");
            }
            var profile = _context.CandidateProfiles.FirstOrDefault(p => p.UserID == userId.Value);
            if (profile == null)
            {
                TempData["Message"] = "Hồ sơ ứng viên không tồn tại.";
                return RedirectToAction("Index", "Home");
            }
            profile.CvLayout = layout;
            _context.SaveChanges();
            TempData["Message"] = "Đã cập nhật layout thành công.";
            return RedirectToAction("ViewCV", new { id = profile.ID });
        }
    }
}
