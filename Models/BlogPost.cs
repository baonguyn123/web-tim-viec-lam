namespace web_jobs.Models
{
    public class BlogPost
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime PostedDate { get; set; } = DateTime.Now;
        public string AuthorBy { get; set; }
        public string? ImageUrl { get; set; }
    }
}
