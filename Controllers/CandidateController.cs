using Microsoft.AspNetCore.Mvc;
using web_jobs.Repository;
using web_jobs.ViewModels;

namespace web_jobs.Controllers
{
    public class CandidateController : Controller
    {
        private readonly IJobRepository _jobRepository;
        public CandidateController(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
        }
        [HttpGet]
        public async Task<IActionResult> SearchJobs(string keyword, string location, string category, int page=1 , int pageSize=5)
        {
            var alljobs = await _jobRepository.SearchJobsAsync(keyword, location, category);
            int totalJobs = alljobs.Count();
            int totalPages = (int)Math.Ceiling((double)totalJobs / pageSize);
            var jobs = alljobs.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var viewModel = new SearchJobViewModel
            {
                Jobs = jobs,
                TotalPages = totalPages,
                CurrentPage = page,
                Keyword = keyword,
                Location = location,
                Category = category
            };

            return View(viewModel);
        }
        
    }
}
