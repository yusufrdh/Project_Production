using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models;

[Table("pit_alias", Schema = "dbo")]
public partial class PitAlias
{
    [Key]
    [Column("id_alias")] // Mapping ke DB Baru
    public int AliasId { get; set; } // Nama Lama

    [Column("id_pit")] // Mapping ke DB Baru
    public int PitId { get; set; } // Nama Lama

    [Column("alias_name")]
    public string AliasName { get; set; } = null!;

    [ForeignKey("PitId")]
    [InverseProperty("PitAliases")]
    public virtual Pit Pit { get; set; } = null!;
}