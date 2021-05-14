using System.Data.Entity;

namespace DRMS_OCRToolkit.Models
{
    public partial class DataModel : DbContext
    {
        public DataModel()
            : base("name=DevConnString")
        {
        }

        public DataModel(string connString)
        {
            Database.Connection.ConnectionString = connString;
        }

        public virtual DbSet<Document> Documents { get; set; }
        public virtual DbSet<PageText> PageText { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {

        }
    }
}
