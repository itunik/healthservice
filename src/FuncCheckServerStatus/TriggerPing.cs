using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Data.Tables;
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
        public static async Task ExecutePing([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            _logger = log;
            await PingServerInternal(true);
        }

        [FunctionName("FirstPingOfTheDay")]
        public static async Task ExecuteFirstPingOfTheDay([TimerTrigger("0 1 00 * * *")] TimerInfo myTimer, ILogger log)
        {
            _logger = log;
            await PingServerInternal();
        }
        
        [FunctionName("LastPingOfTheDay")]
        public static async Task ExecuteFirstPing([TimerTrigger("0 58 23 * * *")] TimerInfo myTimer, ILogger log)
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
            await PingServerInternal(true);

            return new OkResult();
        }

        private static async Task PingServerInternal(bool shouldGenerateReport = false)
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

            if (!shouldGenerateReport)
                await UpdateServerStatus(status);
        }

        private static async Task UpdateServerStatus(string status)
        {
            // New instance of the TableClient class
            TableServiceClient tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var tableClient = tableServiceClient.GetTableClient(tableName: Environment.GetEnvironmentVariable("TableName"));
            
            var latest = await tableClient.QueryAsync<ServerStatusItem>(maxPerPage:1).FirstOrDefaultAsync();

            if ((latest == null) || (latest.Status != status))
            {
                var serverStatusItem = new ServerStatusItem()
                {
                    Status = status,
                    RowKey = (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString(),
                    StatusUpdate = DateTime.UtcNow
                };
                    
                await tableClient.AddEntityAsync(serverStatusItem);
                    
                await RunEmailUpdate(latest?.Status, status, latest?.StatusUpdate);
            }
        }

        private static async Task RunEmailUpdate(string previousStatus, string currentServerStatus, DateTime? lastEventTime)
        {
            _logger.LogInformation("Prep for email sending.");
            var reportTime = DateTime.UtcNow;
            var localTimeZone = false;

            try {
                _logger.LogInformation("Trying to align time zone.");
                var timeZoneId = Environment.GetEnvironmentVariable("TimeZoneId");
                if (timeZoneId != null)
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    reportTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
                    localTimeZone = true;
                }
            }
            catch (Exception)
            {
                _logger.LogInformation("Time zone alignment failed.");
                Console.WriteLine("Cannot parse time zone");
            }
            
            var apiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(Environment.GetEnvironmentVariable("FromEmail"));
            var subject = $"Server status: {currentServerStatus} @ {reportTime.ToShortTimeString()}";
            var to = new EmailAddress(Environment.GetEnvironmentVariable("ToEmail"));

            var color = currentServerStatus == "online"? "#3EB885": "#FC574D";
            var htmlContent =
                $@"<p>Home server status: " +
                $"<span style=\"color: {color};\"><strong>{currentServerStatus.ToUpper()}</strong></span></p>" +
                $"<p>{currentServerStatus.ToUpper()} since: <strong> {reportTime} </strong>";


            if (lastEventTime != null && !string.IsNullOrWhiteSpace(previousStatus))
            {
                var timeInStatus = reportTime - lastEventTime;
                htmlContent +=
                    $"<p><strong>Time spent {previousStatus.ToUpper()}: {timeInStatus.Value.Days} days {timeInStatus.Value.Hours} hours {timeInStatus.Value.Minutes} minutes</strong></p>";
            }
            
            htmlContent += $"<p> Local time zone: <strong> {localTimeZone}</strong></p>";
            
            var fetchTimeZones = bool.Parse(Environment.GetEnvironmentVariable("ShouldReadTimezones") ?? "false");
            
            if (fetchTimeZones)
            {
                var timeZonesHtml = $"<tr><td>Id</td><td>Display Name</td></tr>";
            
                foreach(var tz in TimeZoneInfo.GetSystemTimeZones())
                {
                    timeZonesHtml += $"\n\r<tr><td>{tz.Id}</td><td>{tz.DisplayName}</td></tr>";
                }
                htmlContent += $"<p><strong>Supported Time Zones</strong></p>";
                htmlContent += $"<table>{timeZonesHtml}</table>";
            }
            
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
