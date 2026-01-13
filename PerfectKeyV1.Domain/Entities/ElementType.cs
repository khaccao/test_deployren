using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PerfectKeyV1.Domain.Entities
{
    [Table("ElementTypes")]
    public class ElementType
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Guid")]
        public Guid Guid { get; set; } = Guid.NewGuid();

        [Column("Name")]
        [MaxLength(100)]
        [Required]
        public string Name { get; set; } = string.Empty;

        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("Color")]
        [MaxLength(20)]
        public string? Color { get; set; }

        [Column("Icon")]
        [MaxLength(50)]
        public string? Icon { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreateDate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("LastModify")]
        public DateTime? LastModify { get; set; }

        // Navigation properties
        public virtual ICollection<ElementElementType> ElementElementTypes { get; set; } = new List<ElementElementType>();
    }
}