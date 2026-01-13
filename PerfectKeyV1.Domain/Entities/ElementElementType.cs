using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PerfectKeyV1.Domain.Entities
{
    [Table("ElementElementTypes")]
    public class ElementElementType
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("ElementId")]
        public int ElementId { get; set; }

        [Column("ElementTypeId")]
        public int ElementTypeId { get; set; }

        [Column("CreateDate")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ElementId")]
        public virtual Element Element { get; set; } = null!;

        [ForeignKey("ElementTypeId")]
        public virtual ElementType ElementType { get; set; } = null!;
    }
}