namespace DRMS_OCRToolkit.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class pointcoordinates : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PageText", "ULX", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "ULY", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "LRX", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "LRY", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PageText", "LRY");
            DropColumn("dbo.PageText", "LRX");
            DropColumn("dbo.PageText", "ULY");
            DropColumn("dbo.PageText", "ULX");
        }
    }
}
