namespace DRMS_OCRToolkit.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class init : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Documents",
                c => new
                {
                    FileName = c.String(nullable: false, maxLength: 128),
                })
                .PrimaryKey(t => t.FileName);

            CreateTable(
                "dbo.PageText",
                c => new
                {
                    ID = c.Long(nullable: false, identity: true),
                    DocumentID = c.String(nullable: false, maxLength: 128),
                    PageNumber = c.Int(nullable: false),
                    Text = c.String(maxLength: 100),
                    Left = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Top = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Right = c.Decimal(nullable: false, precision: 18, scale: 2),
                    Bottom = c.Decimal(nullable: false, precision: 18, scale: 2),
                })
                .PrimaryKey(t => t.ID)
                .ForeignKey("dbo.Documents", t => t.DocumentID, cascadeDelete: true)
                .Index(t => t.DocumentID);
        }

        public override void Down()
        {
            DropForeignKey("dbo.PageText", "DocumentID", "dbo.Documents");
            DropIndex("dbo.PageText", new[] { "DocumentID" });
            DropTable("dbo.PageText");
            DropTable("dbo.Documents");
        }
    }
}