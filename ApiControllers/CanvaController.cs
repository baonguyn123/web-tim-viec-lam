using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace web_jobs.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CanvaController : ControllerBase
    {
        [HttpGet("open")]
        public IActionResult OpenCanvaCv()
        {
            return Ok(new
            {
                url = "https://www.canva.com/design/DAG8yNy23_c/edit"
            });
        }
    }
}
