using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Model;

using com.shepherdchurch.ImageCashLetter.Model;
using X937.Records;
using System.Text;
using X937;

namespace com.shepherdchurch.ImageCashLetter.FileFormatTypes
{
    /// <summary>
    /// Defines the basic functionality of any component that will be exporting using the X9.37
    /// DSTU standard.
    /// </summary>
    [Description( "Processes a batch export for Bank of the West." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Bank of the West" )]
    [CodeEditorField( "Deposit Slip Template", "The template for the deposit slip that will be generated. <span class='tip tip-lava'></span>", Rock.Web.UI.Controls.CodeEditorMode.Lava, defaultValue: @"Customer: {{ FileFormat | Attribute:'OriginName' }}
Account: {{ FileFormat | Attribute:'AccountNumber' }}
Amount: {{ Amount }}", order: 20 )]
    public class BankOfTheWest : X937DSTU
    {
        #region System Setting Keys

        /// <summary>
        /// The system setting for the next cash header identifier. These should never be
        /// repeated. Ever.
        /// </summary>
        protected const string SystemSettingNextCashHeaderId = "com.shepherdchurch.ImageCashLetter.BankOfTheWest.NextCashHeaderId";

        /// <summary>
        /// The system setting that contains the last file modifier we used.
        /// </summary>
        protected const string SystemSettingLastFileModifier = "com.shepherdchurch.ImageCashLetter.BankOfTheWest.LastFileModifier";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastItemSequenceNumberKey = "com.shepherdchurch.ImageCashLetter.BankOfTheWest.LastItemSequenceNumber";

        #endregion

        /// <summary>
        /// Gets the next item sequence number.
        /// </summary>
        /// <returns>An integer that identifies the unique item sequence number that can be used.</returns>
        protected int GetNextItemSequenceNumber()
        {
            int lastSequence = Rock.Web.SystemSettings.GetValue( LastItemSequenceNumberKey ).AsIntegerOrNull() ?? 0;
            int nextSequence = lastSequence + 1;

            Rock.Web.SystemSettings.SetValue( LastItemSequenceNumberKey, nextSequence.ToString() );

            return nextSequence;
        }

        /// <summary>
        /// Gets the file header record (type 01).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <returns>
        /// A FileHeader record.
        /// </returns>
        protected override FileHeader GetFileHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            var header = base.GetFileHeaderRecord( fileFormat );

            //
            // The combination of the following fields must be unique:
            // DestinationRoutingNumber + OriginatingRoutingNumber + CreationDateTime + FileIdModifier
            //
            // If the last file we sent has the same routing numbers and creation date time then
            // increment the file id modifier.
            //
            var fileIdModifier = "A";
            var hashText = header.ImmediateDestinationRoutingNumber + header.ImmediateOriginRoutingNumber + header.FileCreationDateTime.ToString( "yyyyMMdd" );
            var hash = HashString( hashText );

            //
            // find the last modifier, if there was one.
            //
            var lastModifier = Rock.Web.SystemSettings.GetValue( SystemSettingLastFileModifier );
            if ( !string.IsNullOrWhiteSpace( lastModifier ) )
            {
                var components = lastModifier.Split( '|' );

                if ( components.Length == 2 )
                {
                    //
                    // If the modifier is for the same file, increment the file modifier.
                    //
                    if ( components[0] == hash )
                    {
                        fileIdModifier = ( ( char ) ( components[1][0] + 1 ) ).ToString();

                        //
                        // If we have done more than 26 files today, assume we are testing and start back at 'A'.
                        //
                        if ( fileIdModifier[0] > 'Z' )
                        {
                            fileIdModifier = "A";
                        }
                    }
                }
            }

            header.FileIdModifier = fileIdModifier;
            Rock.Web.SystemSettings.SetValue( SystemSettingLastFileModifier, string.Join( "|", hash, fileIdModifier ) );

            return header;
        }

        /// <summary>
        /// Gets the cash letter header record (type 10).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <returns>
        /// A CashLetterHeader record.
        /// </returns>
        protected override CashLetterHeader GetCashLetterHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            int cashHeaderId = Rock.Web.SystemSettings.GetValue( SystemSettingNextCashHeaderId ).AsIntegerOrNull() ?? 0;

            var header = base.GetCashLetterHeaderRecord( fileFormat );
            header.ID = cashHeaderId.ToString( "D8" );
            Rock.Web.SystemSettings.SetValue( SystemSettingNextCashHeaderId, ( cashHeaderId + 1 ).ToString() );

            return header;
        }

        /// <summary>
        /// Gets the credit detail deposit record (type 61).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="bundleIndex">Number of existing bundle records in the cash letter.</param>
        /// <param name="transactions">The transactions associated with this deposit.</param>
        /// <returns>A collection of records.</returns>
        protected override List<X937.Record> GetCreditDetailRecords( ImageCashLetterFileFormat fileFormat, int bundleIndex, List<FinancialTransaction> transactions )
        {
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );

            var records = new List<X937.Record>();

            var creditDetail = new CreditDetail
            {
                PayorRoutingNumber = "500100015",
                CreditAccountNumber = accountNumber + "/",
                Amount = transactions.Sum( t => t.TotalAmount ),
                InstitutionItemSequenceNumber = GetNextItemSequenceNumber().ToString( "000000000000000" ),
                DebitCreditIndicator = "2"
            };
            records.Add( creditDetail );

            for ( int i = 0; i < 2; i++ )
            {
                using ( var ms = GetDepositSlipImage(fileFormat, creditDetail, i == 0 ) )
                {
                    //
                    // Get the Image View Detail record (type 50).
                    //
                    var detail = new ImageViewDetail
                    {
                        ImageIndicator = 1,
                        ImageCreatorRoutingNumber = routingNumber,
                        ImageCreatorDate = DateTime.Now,
                        ImageViewFormatIndicator = 0,
                        CompressionAlgorithmIdentifier = 0,
                        SideIndicator = i,
                        ViewDescriptor = 0,
                        DigitalSignatureIndicator = 0
                    };

                    //
                    // Get the Image View Data record (type 52).
                    //
                    var data = new ImageViewData
                    {
                        InstitutionRoutingNumber = routingNumber,
                        BundleBusinessDate = DateTime.Now,
                        ClientInstitutionItemSequenceNumber = creditDetail.InstitutionItemSequenceNumber,
                        ClippingOrigin = 0,
                        ImageData = ms.ReadBytesToEnd()
                    };

                    records.Add( detail );
                    records.Add( data );
                }
            }

            return records;
        }

        /// <summary>
        /// Gets the records that identify a single check being deposited.
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transaction">The transaction to be deposited.</param>
        /// <returns>
        /// A collection of records.
        /// </returns>
        protected override List<Record> GetItemRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction )
        {
            var records = base.GetItemRecords( fileFormat, transaction );
            var sequenceNumber = GetNextItemSequenceNumber();

            //
            // Modify the Check Detail Record and Check Image Data records to have
            // a unique item sequence number.
            //
            var checkDetail = records.Where( r => r.RecordType == 25 ).Cast<dynamic>().FirstOrDefault();
            checkDetail.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );

            foreach ( var imageData in records.Where( r => r.RecordType == 52 ).Cast<dynamic>() )
            {
                imageData.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );
            }

            return records;
        }

        /// <summary>
        /// Gets the credit detail deposit record (type 61).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transactions">The transactions associated with this deposit.</param>
        /// <param name="isFrontSide">True if the image to be retrieved is the front image.</param>
        /// <returns>A stream that contains the image data in TIFF 6.0 CCITT Group 4 format.</returns>
        protected virtual Stream GetDepositSlipImage( ImageCashLetterFileFormat fileFormat, CreditDetail creditDetail, bool isFrontSide )
        {
            var bitmap = new System.Drawing.Bitmap( 1200, 550 );
            var g = System.Drawing.Graphics.FromImage( bitmap );

            var depositSlipTemplate = GetAttributeValue( fileFormat, "DepositSlipTemplate" );
            var mergeFields = new Dictionary<string, object>
            {
                { "FileFormat", fileFormat },
                { "Amount", creditDetail.Amount.ToString( "C" ) }
            };
            var depositSlipText = depositSlipTemplate.ResolveMergeFields( mergeFields, null );

            //
            // Ensure we are opague with white.
            //
            g.FillRectangle( System.Drawing.Brushes.White, new System.Drawing.Rectangle( 0, 0, 1200, 550 ) );

            if ( isFrontSide )
            {
                g.DrawString( depositSlipText,
                    new System.Drawing.Font( "Tahoma", 30 ),
                    System.Drawing.Brushes.Black,
                    new System.Drawing.PointF( 50, 50 ) );
            }

            g.Flush();

            //
            // Ensure the DPI is correct.
            //
            bitmap.SetResolution( 200, 200 );

            //
            // Compress using TIFF, CCITT Group 4 format.
            //
            var codecInfo = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                .Where( c => c.MimeType == "image/tiff" )
                .First();
            var parameters = new System.Drawing.Imaging.EncoderParameters( 1 );
            parameters.Param[0] = new System.Drawing.Imaging.EncoderParameter( System.Drawing.Imaging.Encoder.Compression, ( long ) System.Drawing.Imaging.EncoderValue.CompressionCCITT4 );

            var ms = new MemoryStream();
            bitmap.Save( ms, codecInfo, parameters );
            ms.Position = 0;

            return ms;
        }

        /// <summary>
        /// Hashes the string with SHA256.
        /// </summary>
        /// <param name="contents">The contents to be hashed.</param>
        /// <returns>A hex representation of the hash.</returns>
        protected string HashString( string contents )
        {
            byte[] byteContents = Encoding.Unicode.GetBytes( contents );

            var hash = new System.Security.Cryptography.SHA256CryptoServiceProvider().ComputeHash( byteContents );

            return string.Join( "", hash.Select( b => b.ToString( "x2" ) ).ToArray() );
        }
    }
}
