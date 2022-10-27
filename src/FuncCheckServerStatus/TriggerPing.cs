using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FuncCheckServerStatus
{
    public static class TriggerPing
    {
        private static ILogger _logger;

        [FunctionName("TriggerPing")]
        public static async Task ExecutePing([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log)
        {
            _logger = log;
            await PingServerInternal();
        }
        
        [FunctionName("TriggerPingHttp")]
        public static async Task<IActionResult> ExecutePingHttp(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req, ILogger log)
        {
            _logger = log;
            await PingServerInternal();

            return new OkResult();
        }

        private static async Task PingServerInternal()
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var uriString = Environment.GetEnvironmentVariable("PingUrl");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(20);
            string status;

            try
            {
                _logger.LogInformation("Calling remote server.");
                var response = await httpClient.GetAsync(uriString);

                status = response.IsSuccessStatusCode ? "online" : "offline";
            }
            catch (Exception)
            {
                _logger.LogInformation("Remote server did not respond.");
                status = "offline";
            }

            await RunEmailUpdate(status);
        }

        private static async Task RunEmailUpdate(string serverStatus)
        {
            _logger.LogInformation("Prep for email sending.");
            DateTime reportTime = DateTime.UtcNow;
            bool localTimeZone = false;
            try {
                _logger.LogInformation("Trying to align time zone.");
                var timeZoneId = Environment.GetEnvironmentVariable("TimeZoneId");
                TimeZoneInfo eestZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                reportTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, eestZone);
                localTimeZone = true;
            }
            catch (Exception)
            {
                _logger.LogInformation("Time zone alignment failed.");
                Console.WriteLine("Cannot parse time zone");
            }
            
            
            var apiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(Environment.GetEnvironmentVariable("FromEmail"));
            var subject = $"Server status: {serverStatus}";
            var to = new EmailAddress(Environment.GetEnvironmentVariable("ToEmail"));

            var color = serverStatus == "online"? "#3EB885": "#FC574D";
            var htmlContent = 
                $@"<p>Home server status: " +
                $"<span style=\"color: {color};\"><strong>{serverStatus}</strong></span></p>" + 
                $"<p>{serverStatus} since: <strong> {reportTime} </strong></p>" +
                $"<p> Local time zone: <strong> {localTimeZone}</strong></p>";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
            

            try {
                _logger.LogInformation("Attempt to send email");
                await client.SendEmailAsync(msg);
                _logger.LogInformation("Email send.");
            }
            catch (Exception e) {
                _logger.Log(LogLevel.Error, e, "Error sending email");
            }
        }
    }
}
