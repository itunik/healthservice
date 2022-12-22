using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
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
			var status = await PingServerInternal();

			var statusChangeObj = await IsStatusChangedAsync(status);

            if (statusChangeObj.IsChanged)
			{
                await UpdateServerStatus(status);
                await SendChangedStatusEmailUpdate(statusChangeObj.LatestItem?.Status, status, statusChangeObj.LatestItem?.StatusUpdate);
            }
        }

		//[FunctionName("FirstPingOfTheDay")]
		public static async Task ExecuteFirstPingOfTheDay([TimerTrigger("0 1 00 * * *")] TimerInfo myTimer, ILogger log)
		{
			_logger = log;
			var status = await PingServerInternal(false);
			await UpdateServerStatus(status);
		}
		
		//[FunctionName("LastPingOfTheDay")]
		public static async Task ExecuteFirstPing([TimerTrigger("0 58 23 * * *")] TimerInfo myTimer, ILogger log)
		{
			_logger = log;
			var status = await PingServerInternal(false);
            await UpdateServerStatus(status);
			//await SendEmailDailyReport();
        }
				
		private static async Task<string> PingServerInternal(bool shouldGenerateReport = true)
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

			return status;
		}

		private static async Task UpdateServerStatus(string status)
		{
			var serverStatusItem = new ServerStatusItem()
			{
				Status = status,
				RowKey = (DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks).ToString(),
				StatusUpdate = DateTime.UtcNow
			};

            TableServiceClient tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var tableClient = tableServiceClient.GetTableClient(tableName: Environment.GetEnvironmentVariable("TableName"));

            await tableClient.AddEntityAsync(serverStatusItem);							
		}

		private static async Task<StatusChangeObject> IsStatusChangedAsync(string status)
		{
			// New instance of the TableClient class
			TableServiceClient tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

			var tableClient = tableServiceClient.GetTableClient(tableName: Environment.GetEnvironmentVariable("TableName"));

			var latest = await tableClient.QueryAsync<ServerStatusItem>(maxPerPage: 1).FirstOrDefaultAsync();

			return new StatusChangeObject
			{
				IsChanged = (latest == null) || (latest.Status != status),
				LatestItem = latest
            };			
		}

		private static async Task SendChangedStatusEmailUpdate(string previousStatus, string currentServerStatus, DateTime? lastEventTime)
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

		[FunctionName("TriggerPingHttp")]
		public static async Task<IActionResult> SendEmailDailyReport(
						[HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
					HttpRequest req, ILogger log)

		{
			_logger = log;

            //TableServiceClient tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            //var tableClient = tableServiceClient.GetTableClient(tableName: Environment.GetEnvironmentVariable("TableName"));

            //var reportTime = DateTime.UtcNow;
            //var localTimeZone = false;

            //try
            //{
            //	_logger.LogInformation("Trying to align time zone.");
            //	var timeZoneId = Environment.GetEnvironmentVariable("TimeZoneId");
            //	if (timeZoneId != null)
            //	{
            //		var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            //		reportTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            //		localTimeZone = true;
            //	}
            //}
            //catch (Exception)
            //{
            //	_logger.LogInformation("Time zone alignment failed.");
            //	Console.WriteLine("Cannot parse time zone");
            //}

            //var queryResult = await tableClient.QueryAsync<ServerStatusItem>().OrderBy(ssi => ssi.StatusUpdate).ToListAsync();

            //var l = new List<ServerStatusItem>();

            //foreach (var item in queryResult)
            //{
            //	if (item.StatusUpdate.Date == reportTime.Date)
            //		l.Add(item);
            //}

            var status = await PingServerInternal(false);
            await UpdateServerStatus(status);

            
			return new OkObjectResult(status);
			//query all status within the day
			//calculate amount online
			//calculate amount offline
			//createhtml
			//send html
		}
	}
}
