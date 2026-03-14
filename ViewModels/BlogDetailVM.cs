using web_jobs.Models;

namespace web_jobs.ViewModels
{
    public class BlogDetailVM
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string AuthorBy { get; set; }
        public DateTime PostedDate { get; set; }
        public string? ImageUrl { get; set; }
        public BlogPost Blog { get; set; }                // Bài viết đang xem
        public List<BlogPost> RelatedPosts { get; set; }  // 6 bài viết gợi ý
    }
}
