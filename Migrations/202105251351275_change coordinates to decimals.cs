namespace DRMS_OCRToolkit.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class changecoordinatestodecimals : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.PageText", "Left", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AlterColumn("dbo.PageText", "Top", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AlterColumn("dbo.PageText", "Right", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AlterColumn("dbo.PageText", "Bottom", c => c.Decimal(nullable: false, precision: 18, scale: 2));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.PageText", "Bottom", c => c.Int(nullable: false));
            AlterColumn("dbo.PageText", "Right", c => c.Int(nullable: false));
            AlterColumn("dbo.PageText", "Top", c => c.Int(nullable: false));
            AlterColumn("dbo.PageText", "Left", c => c.Int(nullable: false));
        }
    }
}
