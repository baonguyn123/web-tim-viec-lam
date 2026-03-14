using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_jobs.Models;
using web_jobs.Repository;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SearchAPI : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJobRepository _jobRepository;
        public SearchAPI(ApplicationDbContext context, IJobRepository jobRepository)
        {
            _context = context;
            _jobRepository = jobRepository;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchJobs(string keyword=null, string location = null, string category = null, int page = 1, int pageSize = 5 ,
                double? userLat = null,
                double? userLng = null,
                double maxDistanceKm = 10)
        {
            // Await the Task<IEnumerable<Job>> to get the actual data
            var jobs = await _jobRepository.SearchJobsAsync(keyword, location, category);

            // Apply the LINQ Where filter on the resolved IEnumerable<Job>
            var filteredJobs = jobs.Where(j => j.Status == "approved").ToList();
            if(!string.IsNullOrEmpty(keyword))
            {
                var kw = keyword.Trim().ToLower();
                filteredJobs = filteredJobs.Where(j => j.JobTitle != null && j.JobTitle.Trim().ToLower().Contains(kw)).ToList();
            }
            if(!string.IsNullOrEmpty(category))
            {
                var cat = category.Trim().ToLower();
                filteredJobs = filteredJobs.Where(j=>j.Category !=null && j.Category.Name.Trim().ToLower().Contains(cat)).ToList();
            }
            if(!string.IsNullOrEmpty(location))
            {
                var lc = location.Trim().ToLower();
                filteredJobs = filteredJobs.Where(j => j.Locate != null && j.Locate.Trim().ToLower().Contains(lc)).ToList();
            }
            if (userLat.HasValue && userLng.HasValue)
            {
                filteredJobs = filteredJobs
                    .Where(j =>
                    {
                        if (!j.Employer.Latitude.HasValue || !j.Employer.Longitude.HasValue) return false;
                        if (j.Employer.Latitude.Value == 0 || j.Employer.Longitude.Value == 0) return false;

                        double lat = j.Employer.Latitude.Value / 10000000;  
                        double lng = j.Employer.Longitude.Value / 1000000;

                        double distance = GetDistance(userLat.Value, userLng.Value, lat, lng);
                        return distance <= maxDistanceKm;
                    })
                    .ToList();
            }

            int totalJobs = filteredJobs.Count();
            int totalPages = (int)Math.Ceiling((double)totalJobs / pageSize);
            // Implement pagination
            var paginatedJobs = filteredJobs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(j => new
                {
                    j.Status,
                    j.Id,
                    j.JobTitle,
                    j.Locate,
                    j.Salary,
                    j.Employer.CompanyLogo,
                    j.Employer.CompanyAddress,
                    j.Employer.CompanyName,
                    j.Employer.Latitude,
                    j.Employer.Longitude,
                    j.JobType.JobType_Name,
                    DaysLeft = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days : (int?)null,
                    IsExpired = j.ApplicationDeadline != null ? ((DateTime)j.ApplicationDeadline - DateTime.Now).Days < 0 : false,
                }
                )
                .ToList();
            return Ok(new
            {
                Job = paginatedJobs,
                CurrentPage = page,
                TotalPages = totalPages,
                 Keyword = keyword,
                Location = location,
                Category = category
            });
        }
        private double GetDistance(double lat1, double lng1, double lat2, double lng2)
        {
            double R = 6371; 
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        [HttpGet("{id}")]
        public IActionResult getJobDetail(Guid id)
        {
            var job = _context.Jobs
                .Include(j => j.Category)
                .Include(j => j.Employer)
                  .Include(j => j.JobType)
                .FirstOrDefault(j => j.Id == id && j.Status == "approved");

            if (job == null) return NotFound();
            var dayLeft = job.ApplicationDeadline != null ? ((DateTime)job.ApplicationDeadline - DateTime.Now).Days : (int?)null;
            var isExpired = dayLeft < 0;
            return Ok(new
            {
                job.Id,
                job.JobTitle,
                job.JobDescription,
                job.Requirements,
                job.Benefits,
                job.Salary,
                job.Locate,
                job.ApplicationDeadline,
                job.PostedDate,
                Category = job.Category.Name,
                JobType = job.JobType.JobType_Name,
                DaysLeft = dayLeft,
                IsExpired = isExpired,

                Employer = new
                {
                    job.Employer.CompanyName,
                    job.Employer.CompanyEmail,
                    job.Employer.CompanySize,
                    job.Employer.CompanyLogo,
                    job.Employer.CompanyDescription,
                    job.Employer.LicenseDocument,
                    job.Employer.Latitude,
                    job.Employer.Longitude
                }

            });
        }
    }
}
