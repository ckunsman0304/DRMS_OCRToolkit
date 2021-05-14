namespace DRMS_OCRToolkit.Models
{
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
    }
}
