using web_jobs.Models;
namespace web_jobs.ViewModels
{
    public class SearchJobViewModel
    {
        public List<Job> Jobs { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }

        public string Keyword { get; set; }
        public string Location { get; set; }
        public string Category { get; set; }
    }
}
