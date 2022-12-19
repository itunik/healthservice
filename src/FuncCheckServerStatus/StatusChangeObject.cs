using System;
namespace FuncCheckServerStatus
{
	public class StatusChangeObject
	{
		public bool IsChanged { get; set; }
		public ServerStatusItem LatestItem { get; set; }
	}
}

