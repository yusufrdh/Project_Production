using System;
using System.Collections.Generic;

namespace KP_InternalSystem.Models;

public partial class VwCleanPitMapping
{
    public string? Location { get; set; }

    public string? Division { get; set; }

    public string? Department { get; set; }

    public string? PitNameOfficial { get; set; }

    public string? PitNameAlias { get; set; }

    public string? Code { get; set; }

    public string? Status { get; set; }

    public DateOnly? EffectiveDate { get; set; }
}
