using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models
{
    // Mapping ke tabel "product" yang SUDAH ADA di DB
    [Table("product")] 
    public class Product
    {
        [Key]
        [Column("id_product")]
        public int ProductId { get; set; }

        [Required]
        [Column("product_name")]
        public string ProductName { get; set; } = null!;

        [Column("product_code")]
        public string ProductCode { get; set; } = null!;

        // Kolom CreatedAt SUDAH DIHAPUS

        // --- KOLOM BARU (Nanti ditambahkan via Migrasi) ---
       
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Relasi ke Alias
        public List<ProductAlias> ProductAliases { get; set; } = new List<ProductAlias>();
    }
}