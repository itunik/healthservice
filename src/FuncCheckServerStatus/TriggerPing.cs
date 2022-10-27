using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FuncCheckServerStatus
{
    public static class TriggerPing
    {
        [FunctionName("TriggerPing")]
        public static async Task ExecutePing([TimerTrigger("* * */1 * * *")]TimerInfo myTimer, ILogger log)
        {
            await PingServerInternal(log);
        }

        private static async Task PingServerInternal(ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var uriString = Environment.GetEnvironmentVariable("PingUrl");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(20);
            string status;

            try
            {
                var response = await httpClient.GetAsync(uriString);

                status = response.IsSuccessStatusCode ? "online" : "offline";
            }
            catch (Exception)
            {
                status = "offline";
            }

            await RunEmailUpdate(status);
        }

        private static async Task RunEmailUpdate(string serverStatus)
        {
            DateTime reportTime = DateTime.UtcNow;
            bool localTimeZone = false;
            try {
                var timeZoneId = Environment.GetEnvironmentVariable("TimeZoneId");
                TimeZoneInfo eestZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                reportTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, eestZone);
                localTimeZone = true;
            }
            catch (Exception)
            {
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
            await client.SendEmailAsync(msg);
        }
    }
}
