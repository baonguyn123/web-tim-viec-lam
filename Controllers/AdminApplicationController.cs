using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Models; // Model Application nếu có

namespace web_jobs.Controllers
{
    public class AdminApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public AdminApplicationController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        public IActionResult Index()
        {
            // Sau này bạn load danh sách đơn ở đây
            return View();
        }
    }
}
