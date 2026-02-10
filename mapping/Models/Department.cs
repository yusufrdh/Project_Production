using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models;

[Table("department", Schema = "dbo")]
public partial class Department
{
    [Key]
    [Column("id_department")] // Mapping ke DB Baru
    public int DepartmentId { get; set; } // Nama Lama (Biar Controller Seneng)

    [Column("department_name")]
    public string DepartmentName { get; set; } = null!;

    [Column("id_division")] // Mapping ke DB Baru
    public int DivisionId { get; set; } // Nama Lama

    // --- RELASI ---
    [ForeignKey("DivisionId")]
    [InverseProperty("Departments")] 
    public virtual Division Division { get; set; } = null!; // Controller butuh ini

    [InverseProperty("Department")]
    public virtual ICollection<Pit> Pits { get; set; } = new List<Pit>();
}