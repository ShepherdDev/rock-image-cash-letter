using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.UI;

using com.shepherdchurch.ImageCashLetter;
using com.shepherdchurch.ImageCashLetter.Model;
using System.ComponentModel;

namespace RockWeb.Plugins.com_shepherdchurch.ImageCashLetter
{
    [DisplayName( "Remote Deposit Export" )]
    [Category( "Shepherd Church > Image Cash Letter" )]
    [Description( "Exports batch data for use remote deposit with a bank." )]
    public partial class RemoteDepositExport : RockBlock
    {
        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            if ( !IsPostBack )
            {
                var fileFormatService = new ImageCashLetterFileFormatService( new RockContext() );
                var fileFormats = fileFormatService.Queryable().Where( f => f.IsActive == true );

                ddlFileFormat.Items.Clear();
                ddlFileFormat.Items.Add( new ListItem() );
                foreach ( var fileFormat in fileFormats )
                {
                    ddlFileFormat.Items.Add( new ListItem( fileFormat.Name, fileFormat.Id.ToString() ) );
                }
            }
        }

        protected void lbExport_Click( object sender, EventArgs e )
        {
            using ( var rockContext = new RockContext() )
            {
                var batchId = tbBatchId.Text.AsInteger();
                var batches = new FinancialBatchService( rockContext ).Queryable().Where( b => b.Id == batchId ).ToList();
                var fileFormat = new ImageCashLetterFileFormatService( rockContext ).Get( ddlFileFormat.SelectedValue.AsInteger() );
                var component = FileFormatTypeContainer.GetComponent( fileFormat.EntityType.Name );
                List<string> errorMessages;

                fileFormat.LoadAttributes( rockContext );

                var mergeFields = new Dictionary<string, object>
                {
                    {  "FileFormat", fileFormat }
                };
                var filename = fileFormat.FileNameTemplate.ResolveMergeFields( mergeFields );

                var stream = component.ExportBatches( fileFormat, batches, out errorMessages );

                var binaryFileService = new BinaryFileService( rockContext );
                var binaryFileTypeService = new BinaryFileTypeService( rockContext );
                var binaryFile = new BinaryFile
                {
                    BinaryFileTypeId = binaryFileTypeService.Get( Rock.SystemGuid.BinaryFiletype.DEFAULT.AsGuid() ).Id,
                    IsTemporary = true,
                    FileName = filename,
                    MimeType = "octet/stream",
                    ContentStream = stream
                };

                binaryFileService.Add( binaryFile );
                rockContext.SaveChanges();

                pnlSuccess.Visible = true;
                hlDownload.NavigateUrl = ResolveUrl( string.Format( "~/GetFile.ashx?Id={0}&attachment=True", binaryFile.Id ) );
            }
        }
    }
}
