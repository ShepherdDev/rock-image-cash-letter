using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Model;
using Rock.Web.UI.Controls;

using com.shepherdchurch.ImageCashLetter.Model;

namespace com.shepherdchurch.ImageCashLetter.FileFormatTypes
{
    [Description( "Processes a file as the preliminary X9.37-2003 DSTU standard." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "X937 DSTU" )]

    [EncryptedTextField( "Routing Number", "Your bank account routing number.", false, "", order: 0 )]
    [EncryptedTextField( "Account Number", "Your bank account number.", false, "", order: 1 )]
    [EncryptedTextField( "Institution Routing Number", "This is defined by your bank, it is usually either the bank routing number or a customer number.", false, "", order: 2 )]
    [TextField( "Destination Name", "The name of the bank the deposit will be made to.", false, "", order: 3 )]
    [TextField( "Origin Name", "The name of the church.", false, "", order: 4 )]
    [TextField( "Contact Name", "The name of the person the bank will contact if there are issues.", false, "", order: 5 )]
    [TextField( "Contact Phone", "The phone number the bank will call if there are issues.", false, "", order: 6 )]
    [BooleanField( "Generate Record 61", "Record 61 is not part of the X9.37 spec, but some banks have already adopted it. Set to Yes if your bank requires this record.", false, "", order: 7 )]
    [EncryptedTextField( "Record 61 Routing Number", "The routing number to be included with the Record 61.", false, "", order: 8 )]
    [CodeEditorField( "Post Process Template", "Lava template that can be used to post-process the X937 records before they are exported. <span class='tip tip-lava'></span>", CodeEditorMode.Lava, CodeEditorTheme.Rock, 200, false, "", order: 9 )]
    public class X937DSTU : FileFormatTypeComponent
    {
        public virtual int MaxItemsPerBundle
        {
            get
            {
                return 200;
            }
        }

        public override Stream ExportBatches( ImageCashLetterFileFormat fileFormat, List<FinancialBatch> batches, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            int currencyTypeCheckId = Rock.Web.Cache.DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ).Id;
            var transactions = batches.SelectMany( b => b.Transactions ).ToList();

            if ( transactions.Any( t => t.FinancialPaymentDetail.CurrencyTypeValueId != currencyTypeCheckId ) )
            {
                errorMessages.Add( "One or more transactions is not of type 'Check'." );
                return null;
            }

            var records = new List<X937.Record>();

            records.Add( GetFileHeaderRecord( fileFormat ) );
            records.Add( GetCashLetterHeaderRecord( fileFormat ) );
            records.AddRange( GetBundleRecords( fileFormat, transactions ) );
            records.Add( GetCashLetterControlRecord( fileFormat, records ) );
            records.Add( GetFileControlRecord( fileFormat, records ) );

            var stream = new MemoryStream();
            using ( var writer = new BinaryWriter( stream, System.Text.Encoding.UTF8, true ) )
            {
                foreach ( var record in records )
                {
                    record.Encode( writer );
                }
            }

            stream.Position = 0;

            return stream;
        }

        protected virtual List<X937.Record> GetBundleRecords( ImageCashLetterFileFormat fileFormat, List<FinancialTransaction> transactions )
        {
            var records = new List<X937.Record>();

            records.Add( GetBundleHeader( fileFormat, 0 ) );

            if ( GetAttributeValue( fileFormat, "GenerateRecord61" ).AsBoolean() )
            {
                records.AddRange( GetCreditDetailRecords( fileFormat, transactions ) );
            }

            foreach ( var transaction in transactions )
            {
                records.AddRange( GetItemRecords( fileFormat, transaction ) );
            }

            records.Add( GetBundleControl( fileFormat, records ) );

            return records;
        }

        protected virtual List<X937.Record> GetItemRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction )
        {
            var records = new List<X937.Record>();

            var micr = new X937.Micr( Rock.Security.Encryption.DecryptString( transaction.CheckMicrEncrypted ) );
            var routingNumber = micr.GetField( 5 );

            var detail = new X937.Records.CheckDetail();
            detail.PayorBankRoutingNumber = routingNumber.Substring( 1, 8 );
            detail.PayorBankRoutingNumberCheckDigit = routingNumber.Substring( 9, 1 );
            detail.OnUs = micr.GetField( 3 ).Replace( 'c', '/' ) + micr.GetField( 2 );
            detail.ExternalProcessingCode = micr.GetField( 6 );
            detail.AuxiliaryOnUs = micr.GetField( 7 );
            detail.ItemAmount = transaction.TotalAmount;
            detail.ClientInstitutionItemSequenceNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            detail.DocumentationTypeIndicator = "G";
            detail.BankOfFirstDepositIndicator = "Y";
            detail.CheckDetailRecordAddendumCount = 1;
            records.Add( detail );

            var detailA = new X937.Records.CheckDetailAddendumA();
            detailA.RecordNumber = 1;
            detailA.BankOfFirstDepositRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            detailA.BankOfFirstDepositBusinessDate = DateTime.Now;
            detailA.TruncationIndicator = "N";
            detailA.BankOfFirstDepositConversionIndicator = "2";
            detailA.BankOfFirstDepositCorrectionIndicator = "0";
            records.Add( detailA );

            records.AddRange( GetImageRecords( fileFormat, transaction, transaction.Images.Take( 1 ).First(), true ) );
            records.AddRange( GetImageRecords( fileFormat, transaction, transaction.Images.Skip( 1 ).Take( 1 ).First(), true ) );

            return records;
        }

        protected virtual List<X937.Record> GetImageRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction, FinancialTransactionImage image, bool isFront )
        {
            var records = new List<X937.Record>();

            var detail = new X937.Records.ImageViewDetail();
            detail.ImageIndicator = 1;
            detail.ImageCreatorRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            detail.ImageCreatorDate = image.CreatedDateTime ?? DateTime.Now;
            detail.ImageViewFormatIndicator = 0;
            detail.CompressionAlgorithmIdentifier = 0;
            detail.SideIndicator = isFront ? 0 : 1;
            detail.ViewDescriptor = 0;
            detail.DigitalSignatureIndicator = 0;
            records.Add( detail );

            var data = new X937.Records.ImageViewData();
            data.InstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            data.BundleBusinessDate = DateTime.Now;
            data.ClientInstitutionItemSequenceNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            data.ClippingOrigin = 0;
            data.ImageData = image.BinaryFile.ContentStream.ReadBytesToEnd();
            records.Add( data );

            return records;
        }

        protected virtual List<X937.Record> GetCreditDetailRecords( ImageCashLetterFileFormat fileFormat, List<FinancialTransaction> transactions )
        {
            var records = new List<X937.Record>();
            var credit = new X937.Records.CreditDetail();

            credit.PayorRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "Record61RoutingNumber" ) );
            credit.CreditAccountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            credit.Amount = transactions.Sum( t => t.TotalAmount );
            credit.InstitutionItemSequenceNumber = RockDateTime.Now.ToString( "yyyyMMddHHmmss" );
            credit.DebitCreditIndicator = "2";
            records.Add( credit );

            for ( int i = 0; i < 2; i++ )
            {
                using ( var ms = new MemoryStream() )
                {
                    var bitmap = new System.Drawing.Bitmap( 1200, 550 );
                    var g = System.Drawing.Graphics.FromImage( bitmap );
                    g.FillRectangle( System.Drawing.Brushes.White, new System.Drawing.Rectangle( 0, 0, 1200, 550 ) );
                    if ( i == 0 )
                    {
                        g.DrawString( string.Format( "Customer: {0}", GetAttributeValue( fileFormat, "OriginName" ) ),
                            new System.Drawing.Font( "Tahoma", 30 ),
                            System.Drawing.Brushes.Black,
                            new System.Drawing.PointF( 200, 200 ) );

                        g.DrawString( string.Format( "Amount: {0}", credit.Amount.ToString( "C" ) ),
                            new System.Drawing.Font( "Tahoma", 30 ),
                            System.Drawing.Brushes.Black,
                            new System.Drawing.PointF( 200, 250 ) );
                    }
                    g.Flush();

                    var codecInfo = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders().Where( c => c.MimeType == "image/tiff" ).First();
                    var parameters = new System.Drawing.Imaging.EncoderParameters( 1 );
                    parameters.Param[0] = new System.Drawing.Imaging.EncoderParameter( System.Drawing.Imaging.Encoder.Compression, ( long ) System.Drawing.Imaging.EncoderValue.CompressionCCITT4 );

                    bitmap.Save( ms, codecInfo, parameters );
                    ms.Position = 0;

                    var detail = new X937.Records.ImageViewDetail();
                    detail.ImageIndicator = 1;
                    detail.ImageCreatorRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
                    detail.ImageCreatorDate = DateTime.Now;
                    detail.ImageViewFormatIndicator = 0;
                    detail.CompressionAlgorithmIdentifier = 0;
                    detail.SideIndicator = i;
                    detail.ViewDescriptor = 0;
                    detail.DigitalSignatureIndicator = 0;
                    records.Add( detail );

                    var data = new X937.Records.ImageViewData();
                    data.InstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
                    data.BundleBusinessDate = DateTime.Now;
                    data.ClientInstitutionItemSequenceNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
                    data.ClippingOrigin = 0;
                    data.ImageData = ms.ReadBytesToEnd();
                    records.Add( data );
                }
            }


            return records;
        }

        protected virtual X937.Records.FileHeader GetFileHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            var header = new X937.Records.FileHeader();

            header.FileTypeIndicator = "T";
            header.ImmediateDestinationRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            header.ImmediateOriginRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            header.FileCreationDateTime = DateTime.Now;
            header.ResendIndicator = "N";
            header.ImmediateDestinationName = GetAttributeValue( fileFormat, "DestinationName" );
            header.ImmediateOriginName = GetAttributeValue( fileFormat, "OriginName" );
            header.FileIdModifier = "1";
            header.CountryCode = "US";
            header.UserField = string.Empty;

            return header;
        }

        protected virtual X937.Records.FileControl GetFileControlRecord( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var control = new X937.Records.FileControl();

            control.CashLetterCount = records.Count( r => r.GetType() == typeof( X937.Records.CashLetterHeader ) );
            control.TotalRecordCount = records.Count + 1;
            control.TotalItemCount = records.Count( r => r.GetType() == typeof( X937.Records.CheckDetail ) );
            control.TotalAmount = records.Where( r => r.GetType() == typeof( X937.Records.CheckDetail ) ).Cast<X937.Records.CheckDetail>().Sum( c => c.ItemAmount );
            control.ImmediateOriginContactName = GetAttributeValue( fileFormat, "ContactName" );
            control.ImmediateOriginContactPhoneNumber = GetAttributeValue( fileFormat, "ContactPhone" );

            return control;
        }

        protected virtual X937.Records.CashLetterHeader GetCashLetterHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            var header = new X937.Records.CashLetterHeader();

            header.CollectionTypeIndicator = 1;
            header.DestinationRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            header.ClientInstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            header.BusinessDate = DateTime.Now;
            header.CreationDateTime = DateTime.Now;
            header.RecordTypeIndicator = "I";
            header.DocumentationTypeIndicator = "G";
            header.ID = "TODO";
            header.OriginatorContactName = GetAttributeValue( fileFormat, "ContactName" );
            header.OriginatorContactPhoneNumber = GetAttributeValue( fileFormat, "ContactPhone" );

            return header;
        }

        protected virtual X937.Records.CashLetterControl GetCashLetterControlRecord( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var control = new X937.Records.CashLetterControl();

            control.BundleCount = records.Count( r => r.GetType() == typeof( X937.Records.BundleHeader ) );
            control.ItemCount = records.Count( r => r.GetType() == typeof( X937.Records.CheckDetail ) );
            control.TotalAmount = records.Where( r => r.GetType() == typeof( X937.Records.CheckDetail ) ).Cast<X937.Records.CheckDetail>().Sum( c => c.ItemAmount );
            control.ImageCount = records.Count( r => r.GetType() == typeof( X937.Records.ImageViewDetail ) || r.GetType() == typeof( X937.Records.ImageViewData ) );
            control.ECEInstitutionName = Rock.Web.Cache.GlobalAttributesCache.Value( "OrganizationName" );

            return control;
        }

        protected virtual X937.Records.BundleHeader GetBundleHeader( ImageCashLetterFileFormat fileFormat, int bundleIndex )
        {
            var header = new X937.Records.BundleHeader();

            header.CollectionTypeIndicator = 1;
            header.DestinationRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            header.ClientInstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            header.BusinessDate = DateTime.Now;
            header.CreationDate = DateTime.Now;
            header.ID = string.Empty;
            header.SequenceNumber = ( bundleIndex + 1 ).ToString();
            header.CycleNumber = string.Empty;
            header.ReturnLocationRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );

            return header;
        }

        protected virtual X937.Records.BundleControl GetBundleControl( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var control = new X937.Records.BundleControl();

            control.ItemCount = records.Count( r => r.GetType() == typeof( X937.Records.CheckDetail ) );
            control.TotalAmount = records.Where( r => r.GetType() == typeof( X937.Records.CheckDetail ) ).Cast<X937.Records.CheckDetail>().Sum( r => r.ItemAmount );
            control.MICRValidTotalAmount = records.Where( r => r.GetType() == typeof( X937.Records.CheckDetail ) ).Cast<X937.Records.CheckDetail>().Sum( r => r.ItemAmount );
            control.ImageCount = records.Count( r => r.GetType() == typeof( X937.Records.ImageViewDetail ) || r.GetType() == typeof( X937.Records.ImageViewData ) );

            return control;
        }
    }
}
