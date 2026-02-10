using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models;

// 1. Arahkan ke Tabel Baru
[Table("pit_official")] 
public partial class Pit 
{
    [Key]
    [Column("id_pit")] // Mapping ke DB Baru
    public int PitId { get; set; } // Nama Property Lama (Biar Controller aman)

    [Column("pit_name_official")]
    public string PitNameOfficial { get; set; } = null!;

    [Column("pit_code")]
    public string? PitCode { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("e_date")] // Di DB namanya 'e_date'
    public DateOnly? EffectiveDate { get; set; } // Di Controller namanya 'EffectiveDate'

    [Column("id_department")]
    public int DepartmentId { get; set; }

    [Column("id_location")]
    public int LocationId { get; set; }

    // --- RELASI KE MASTER DATA (Department, Location, Alias) ---
    
    [ForeignKey("DepartmentId")]
    [InverseProperty("Pits")] 
    public virtual Department Department { get; set; } = null!;

    [ForeignKey("LocationId")]
    [InverseProperty("Pits")]
    public virtual Location Location { get; set; } = null!;

    [InverseProperty("Pit")]
    public virtual ICollection<PitAlias> PitAliases { get; set; } = new List<PitAlias>();

    // --- TAMBAHAN YANG KETINGGALAN (RELASI KE FACT TABLES) ---
    // Ini wajib ada biar Context gak error pas baca .WithMany(p => p.FactActuals)

    
}