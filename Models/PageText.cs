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

        /// <summary>
        /// Left-most X coordinate as a percentage of the page width
        /// </summary>
        public float Left { get; set; }

        /// <summary>
        /// Top-most Y coordinate as a percentage of the page height (Y origin is at top of page)
        /// </summary>
        public float Top { get; set; }

        /// <summary>
        /// Right-most X coordinate as a percentage of the page width
        /// </summary>
        public float Right { get; set; }

        /// <summary>
        /// Bottom-most Y coordinate as a percentage of the page width (Y origin is at top of page)
        /// </summary>
        public float Bottom { get; set; }

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
            return $"{DocumentID}, Pg: {PageNumber}. '{Text}' Left:{Left.ToString("0.00")}; Top:{Top.ToString("0.00")}; Right:{Right.ToString("0.00")}; Bottom:{Bottom.ToString("0.00")}";
        }
    }
}