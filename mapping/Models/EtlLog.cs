using System;
using System.Collections.Generic;

namespace KP_InternalSystem.Models;

public partial class EtlLog
{
    public int LogId { get; set; }

    public string? ProcessName { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? Status { get; set; }

    public int? RowsAffected { get; set; }

    public string? ErrorMessage { get; set; }
}
