namespace DRMS_OCRToolkit.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("PageText")]
    public partial class PageText
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public long ID { get; set; }
        [Required]
        public string DocumentID { get; set; }
        [ForeignKey("DocumentID")]
        public virtual Document Document { get; set; }
        public int PageNumber { get; set; }
        [StringLength(100)]
        public string Text { get; set; }        
        //Upper left coordinates:
        public int ULX { get; set; }
        public int ULY { get; set; }
        //Lower right coordinates:
        public int LRX { get; set; }
        public int LRY { get; set; }



        public override bool Equals(object obj)
        {
            return obj != null && obj is PageText text &&
                   DocumentID == text.DocumentID 
                   && PageNumber == text.PageNumber 
                   && Text == text.Text 
                   && (ULX == text.ULX - 1 || ULX == text.ULX || ULX == text.ULX + 1)
                   && (ULY == text.ULY - 1 || ULY == text.ULY || ULY == text.ULY + 1)
                   && (LRX == text.LRX - 1 || LRX == text.LRX || LRX == text.LRX + 1)
                   && (LRY == text.LRY - 1 || LRY == text.LRY || LRY == text.LRY + 1);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DocumentID, PageNumber, Text, ULX, ULY, LRX, LRY);
        }

        public override string ToString()
        {
            return $"{DocumentID}, Pg: {PageNumber}. '{Text}' UL:({ULX} , {ULY})   LR:({LRX} , {LRY})";
        }
    }
}
