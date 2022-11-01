using System;
using Azure;
using Azure.Data.Tables;

namespace FuncCheckServerStatus;

public record ServerStatusItem : ITableEntity
{
    public string RowKey { get; set; } = default!;

    public string PartitionKey { get; set; } = "ServerStatusItem";
    
    public string Status { get; set; }

    public ETag ETag { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; } = default!;
}