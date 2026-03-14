using Microsoft.AspNetCore.Mvc;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.Controllers
{
    public class JobTypeController : Controller
    {
        private readonly IJobTypeRepository _jobTypeRepository;
        public JobTypeController(IJobTypeRepository jobTypeRepository)
        {
            _jobTypeRepository = jobTypeRepository;
        }
        public async Task<IActionResult> IndexAsync()
        {
            var jobtypes = await _jobTypeRepository.GetAllAsync();
            return View(jobtypes);
        }
        [HttpPost]
        public async Task<IActionResult> Add(JobTypes jobTypes)
        {
            if (ModelState.IsValid)
            {
                await _jobTypeRepository.AddAsync(jobTypes);
                return RedirectToAction("Index");
            }
            return View(jobTypes);
        }
    }
}
