using web_jobs.Models;

namespace web_jobs.ViewModels
{
    public class CompanyJobsViewModel
    {
        public Employer employer { get; set; } // Thông tin nhà tuyển dụng
        public List<Job> Jobs { get; set; } // Danh sách công việc của nhà tuyển dụng
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }
}
