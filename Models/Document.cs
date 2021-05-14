namespace DRMS_OCRToolkit.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class Document
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Key, Required]
        public string FileName { get; set; }
        public virtual ICollection<PageText> Pages { get; set; }
    }
}
