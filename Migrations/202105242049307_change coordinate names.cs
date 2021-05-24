namespace DRMS_OCRToolkit.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class changecoordinatenames : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PageText", "Left", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "Top", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "Right", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "Bottom", c => c.Int(nullable: false));
            DropColumn("dbo.PageText", "ULX");
            DropColumn("dbo.PageText", "ULY");
            DropColumn("dbo.PageText", "LRX");
            DropColumn("dbo.PageText", "LRY");
        }
        
        public override void Down()
        {
            AddColumn("dbo.PageText", "LRY", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "LRX", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "ULY", c => c.Int(nullable: false));
            AddColumn("dbo.PageText", "ULX", c => c.Int(nullable: false));
            DropColumn("dbo.PageText", "Bottom");
            DropColumn("dbo.PageText", "Right");
            DropColumn("dbo.PageText", "Top");
            DropColumn("dbo.PageText", "Left");
        }
    }
}
