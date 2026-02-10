using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models;

[Table("division", Schema = "dbo")]
public partial class Division
{
    [Key]
    [Column("id_division")] // Mapping ke DB Baru
    public int DivisionId { get; set; } // Nama Lama

    [Column("division_name")]
    public string DivisionName { get; set; } = null!;

    [InverseProperty("Division")]
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
}