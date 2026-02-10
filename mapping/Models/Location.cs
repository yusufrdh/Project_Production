using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models;

[Table("location", Schema = "dbo")]
public partial class Location
{
    [Key]
    [Column("id_location")] // Mapping ke DB Baru
    public int LocationId { get; set; } // Nama Lama

    [Column("location_name")]
    public string LocationName { get; set; } = null!;

    [InverseProperty("Location")]
    public virtual ICollection<Pit> Pits { get; set; } = new List<Pit>();
}