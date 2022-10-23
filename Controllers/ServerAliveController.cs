using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace BasicHealthService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ServerAliveController : ControllerBase
    {
        private readonly ILogger _logger;

        public ServerAliveController(ILogger logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult Get()
        {
            return new OkResult();
        }
    }
}