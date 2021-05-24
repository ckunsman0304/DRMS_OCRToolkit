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
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }



        public override bool Equals(object obj)
        {
            return obj != null && obj is PageText text &&
                   DocumentID == text.DocumentID 
                   && PageNumber == text.PageNumber 
                   && Text == text.Text 
                   && (Left == text.Left - 1 || Left == text.Left || Left == text.Left + 1)
                   && (Top == text.Top - 1 || Top == text.Top || Top == text.Top + 1)
                   && (Right == text.Right - 1 || Right == text.Right || Right == text.Right + 1)
                   && (Bottom == text.Bottom - 1 || Bottom == text.Bottom || Bottom == text.Bottom + 1);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DocumentID, PageNumber, Text, Left, Top, Right, Bottom);
        }

        public override string ToString()
        {
            return $"{DocumentID}, Pg: {PageNumber}. '{Text}' UL:({Left} , {Top})   LR:({Right} , {Bottom})";
        }
    }
}
