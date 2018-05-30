using Rock.Plugin;

namespace com.shepherdchurch.ImageCashLetter.Migrations.Version_1
{
    [MigrationNumber( 2, "1.6.4" )]
    public class AddPages : Migration
    {
        public override void Up()
        {
            //
            // Add the File Format List block type
            //
            RockMigrationHelper.UpdateBlockType( "File Format List",
                "Lists file formats in the system.",
                "~/Plugins/com_shepherdchurch/ImageCashLetter/FileFormatList.ascx",
                "Shepherd Church > Image Cash Letter",
                SystemGuid.BlockType.FILE_FORMAT_LIST );
            RockMigrationHelper.UpdateBlockTypeAttribute( SystemGuid.BlockType.FILE_FORMAT_LIST,
                Rock.SystemGuid.FieldType.PAGE_REFERENCE,
                "Detail Page",
                "DetailPage",
                string.Empty,
                "The page that allows the user to edit the details of a file format.",
                0,
                string.Empty,
                SystemGuid.Attribute.FILE_FORMAT_LIST_DETAIL_PAGE );

            //
            // Add the File Format Detail block type
            //
            RockMigrationHelper.UpdateBlockType( "File Format Detail",
                "Displays the details for a file format.",
                "~/Plugins/com_shepherdchurch/ImageCashLetter/FileFormatDetail.ascx",
                "Shepherd Church > Image Cash Letter",
                SystemGuid.BlockType.FILE_FORMAT_DETAIL );

            //
            // Add the Remote Deposit Export block type
            //
            RockMigrationHelper.UpdateBlockType( "Remote Deposit Export",
                "Exports batch data for use remote deposit with a bank.",
                "~/Plugins/com_shepherdchurch/ImageCashLetter/RemoteDepositExport.ascx",
                "Shepherd Church > Image Cash Letter",
                SystemGuid.BlockType.REMOTE_DEPOSIT_EXPORT );

            //
            // Add page Installed Plugins > Image Cash Letter
            //
            RockMigrationHelper.AddPage( "5B6DBC42-8B03-4D15-8D92-AAFA28FD8616",
                "D65F783D-87A9-4CC9-8110-E83466A0EADB", // Full-Width, Rock
                "Image Cash Letter",
                "",
                SystemGuid.Page.IMAGE_CASH_LETTER,
                "fa fa-university" );

            RockMigrationHelper.AddBlock( SystemGuid.Page.IMAGE_CASH_LETTER,
                null,
                "CACB9D1A-A820-4587-986A-D66A69EE9948",
                "Page Menu",
                "Main",
                string.Empty,
                string.Empty,
                0,
                SystemGuid.Block.PAGE_MENU );
            RockMigrationHelper.AddBlockAttributeValue( SystemGuid.Block.PAGE_MENU,
                "1322186A-862A-4CF1-B349-28ECB67229BA",
                "{% include '~~/Assets/Lava/PageListAsBlocks.lava' %}" );

            //
            // Add page Installed Plugins > Image Cash Letter > File Types
            //
            RockMigrationHelper.AddPage( SystemGuid.Page.IMAGE_CASH_LETTER,
                "D65F783D-87A9-4CC9-8110-E83466A0EADB", // Full-Width, Rock
                "File Types",
                "",
                SystemGuid.Page.FILE_TYPES,
                "fa fa-files-o" );

            RockMigrationHelper.AddBlock( SystemGuid.Page.FILE_TYPES,
                null,
                SystemGuid.BlockType.FILE_FORMAT_LIST,
                "File Format List",
                "Main",
                string.Empty,
                string.Empty,
                0,
                SystemGuid.Block.FILE_FORMAT_LIST );
            RockMigrationHelper.AddBlockAttributeValue( SystemGuid.Block.FILE_FORMAT_LIST,
                SystemGuid.Attribute.FILE_FORMAT_LIST_DETAIL_PAGE,
                SystemGuid.Page.FILE_TYPE_DETAIL );

            //
            // Add page Installed Plugins > Image Cash Letter > File Types > File Type Detail
            //
            RockMigrationHelper.AddPage( SystemGuid.Page.FILE_TYPES,
                "D65F783D-87A9-4CC9-8110-E83466A0EADB", // Full-Width, Rock
                "File Type Detail",
                "",
                SystemGuid.Page.FILE_TYPE_DETAIL );
            RockMigrationHelper.AddBlock( SystemGuid.Page.FILE_TYPE_DETAIL,
                null,
                SystemGuid.BlockType.FILE_FORMAT_DETAIL,
                "File Format Detail",
                "Main",
                string.Empty,
                string.Empty,
                0,
                SystemGuid.Block.FILE_FORMAT_DETAIL );

            //
            // Add page Installed Plugins > Image Cash Letter > Remote Deposit Export
            //
            RockMigrationHelper.AddPage( SystemGuid.Page.IMAGE_CASH_LETTER,
                "D65F783D-87A9-4CC9-8110-E83466A0EADB", // Full-Width, Rock
                "Remote Deposit",
                "",
                SystemGuid.Page.REMOTE_DEPOSIT_EXPORT,
                "fa fa-money" );
            RockMigrationHelper.AddBlock( SystemGuid.Page.REMOTE_DEPOSIT_EXPORT,
                null,
                SystemGuid.BlockType.REMOTE_DEPOSIT_EXPORT,
                "Remote Deposit Export",
                "Main",
                string.Empty,
                string.Empty,
                0,
                SystemGuid.Block.REMOTE_DEPOSIT_EXPORT );
        }

        public override void Down()
        {
            RockMigrationHelper.DeleteBlock( SystemGuid.Block.REMOTE_DEPOSIT_EXPORT );
            RockMigrationHelper.DeletePage( SystemGuid.Page.REMOTE_DEPOSIT_EXPORT );

            RockMigrationHelper.DeleteBlock( SystemGuid.Block.FILE_FORMAT_DETAIL );
            RockMigrationHelper.DeletePage( SystemGuid.Page.FILE_TYPE_DETAIL );

            RockMigrationHelper.DeleteBlock( SystemGuid.Block.FILE_FORMAT_LIST );
            RockMigrationHelper.DeletePage( SystemGuid.Page.FILE_TYPES );

            RockMigrationHelper.DeleteBlock( SystemGuid.Block.PAGE_MENU );
            RockMigrationHelper.DeletePage( SystemGuid.Page.IMAGE_CASH_LETTER );

            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.REMOTE_DEPOSIT_EXPORT );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.FILE_FORMAT_DETAIL );
            RockMigrationHelper.DeleteBlockType( SystemGuid.BlockType.FILE_FORMAT_LIST );
        }
    }
}
