using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Models;
using web_jobs.ViewModels;

namespace web_jobs.Controllers
{
  
    public class BlogPostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public BlogPostController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        [Authorize(Roles = "Admin")]
        public IActionResult Index(int page =1, int pageSize =5)
        {
            var blogs = _context.BlogPosts.OrderByDescending(b => b.PostedDate).ToList();
            var totalBlogs = blogs.Count();
            var totalPages = (int)Math.Ceiling((double)totalBlogs / pageSize);
            var blog = blogs.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            return View(blog);
        }
        public IActionResult Detail(int id)
        {
            var blog = _context.BlogPosts.FirstOrDefault(b => b.ID == id);
            if (blog == null)
            {
                return NotFound();
            }
            var relatedPosts = _context.BlogPosts
                .Where(b => b.ID != id)
                .OrderBy(r => Guid.NewGuid())
                .Take(6).ToList();// Randomly select related posts
            var viewModel = new BlogDetailVM
            {
                Blog = blog,
                RelatedPosts = relatedPosts
            };
            return View(viewModel);
        }
        [Authorize(Roles = "Admin")]
        public IActionResult Add()
        {
            return View();
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(BlogPost blogPost)
        {
            if (!ModelState.IsValid)
            {
                return View(blogPost);
            }

            blogPost.PostedDate = DateTime.Now;
            _context.BlogPosts.Add(blogPost);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult UploadImage(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(_env.WebRootPath, "images", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }
                var imageUrl = Url.Content("~/images/" + fileName);

                //Dòng return Json(new { location = imageUrl });
                //là để Summernote biết chèn ảnh vào đâu sau khi ảnh đã upload thành công.
                return Json(new { location = imageUrl });
            }

            return Json(new { error = "Upload failed" });
        }
        [Authorize(Roles = "Admin")]
        public IActionResult Update(int id)
        {
            var blog = _context.BlogPosts.FirstOrDefault(b => b.ID == id);
            if (blog == null)
            {
                return NotFound();
            }
            return View(blog);
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(BlogPost blogPost)
        {
            if (!ModelState.IsValid)
            {
                return View(blogPost);
            }
            var existingBlog = _context.BlogPosts.FirstOrDefault(b => b.ID == blogPost.ID);
            if (existingBlog == null)
            {
                return NotFound();
            }
            existingBlog.Title = blogPost.Title;
            existingBlog.Content = blogPost.Content;
            existingBlog.AuthorBy = blogPost.AuthorBy;

            _context.SaveChanges();
            return RedirectToAction("Index");
        }
        [Authorize(Roles = "Admin")]    
        public IActionResult Delete(int id)
        {
            var post = _context.BlogPosts.FirstOrDefault(p => p.ID == id);
            if (post == null) return NotFound();

            _context.BlogPosts.Remove(post);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var blog = _context.BlogPosts.FirstOrDefault(b => b.ID == id);
            if (blog == null)
            {
                return NotFound();
            }
            _context.BlogPosts.Remove(blog);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }
    }

}

