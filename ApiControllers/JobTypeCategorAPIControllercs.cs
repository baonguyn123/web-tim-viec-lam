using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class JobTypeCategorAPIControllercs : ControllerBase
    {
        private readonly IJobTypeRepository _jobTypeRepository;
        private readonly ICategoryRepository _categoryRepository;

        public JobTypeCategorAPIControllercs(IJobTypeRepository jobTypeRepository, ICategoryRepository categoryRepository)
        {
            _jobTypeRepository = jobTypeRepository;
            _categoryRepository = categoryRepository;
        }
        [HttpGet("jobtypes")]
        public async Task<IActionResult> GetAllJobTypes()
        {
            var jobTypes = await _jobTypeRepository.GetAllAsync();
            return Ok(jobTypes);
        }
        [HttpGet("categories")]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return Ok(categories);
        }
    }
}
