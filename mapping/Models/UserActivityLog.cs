using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models
{
    [Table("UserActivityLog")]
    public class UserActivityLog
    {
        [Key]
        public int LogId { get; set; }

        public string? ActionType { get; set; } // Kasih ? biar gak warning

        public string? Message { get; set; }    // Kasih ? biar gak warning

        public DateTime CreatedAt { get; set; }
    }
}