using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KP_InternalSystem.Models
{
    // Mapping ke tabel baru "product_alias"
    [Table("product_alias")]
    public class ProductAlias
    {
        [Key]
        [Column("id_alias")]
        public int AliasId { get; set; }

        [Required]
        [Column("alias_name")]
        public string AliasName { get; set; } = null!;

        [Column("id_product")]
        public int ProductId { get; set; }
        
        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }
}