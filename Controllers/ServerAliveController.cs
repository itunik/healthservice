using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BasicHealthService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ServerAliveController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public ServerAliveController(ILogger logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        public ActionResult Get()
        {
            _logger.Information("Triggered GET method");
            var elasticUri = _configuration["ElasticConfiguration:URI"];
            
            return new OkObjectResult($"I'm alive. Elastic URI: {elasticUri}");
        }
    }
}