﻿using System;
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
using X937;
using System.Text;
using Rock.Security;
using com.shepherdchurch.ImageCashLetter;
using com.shepherdchurch.ImageCashLetter.FileFormatTypes;

namespace com.bemaservices.RemoteCheckDeposit.FileFormatTypes
{
    /// <summary>
    /// Defines the basic functionality of any component that will be exporting using the X9.37
    /// DSTU standard.
    /// </summary>
    [Description( "Processes a batch export for BMO Harris Bank." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "BMO Harris Bank" )]
    [CodeEditorField( "Deposit Slip Template", "The template for the deposit slip that will be generated. <span class='tip tip-lava'></span>", Rock.Web.UI.Controls.CodeEditorMode.Lava, defaultValue: @"Customer: {{ FileFormat | Attribute:'OriginName' }}
Account: {{ FileFormat | Attribute:'AccountNumber' }}
Amount: {{ Amount }}", order: 20 )]
    [EncryptedTextField( "Deposit Account Number", "Optional account number to deposit checks to. (Use only if different from main account). Record 61 Field 5.", false, "", key: "DepositAccountNumber" )]
    [EncryptedTextField( "Image View Routing Number", "Optional routing number used in Image View records. (Use only if different from main account). Record 50 Field 3 and Record 52 Field 2.", false, "", key: "ImageViewRoutingNumber" )]
    [BooleanField( "Add Settlement Date", "Set to True to insert the manually selected date as Record 90 Field 7 Settlement date. Defaults to blank.", order: 21, defaultValue: false)]
    public class BMO : X937DSTU
    {
        #region System Setting Keys

        /// <summary>
        /// The system setting for the next cash header identifier. These should never be
        /// repeated. Ever.
        /// </summary>
        protected const string SystemSettingNextCashHeaderId = "BMO.NextCashHeaderId";

        /// <summary>
        /// The system setting that contains the last file modifier we used.
        /// </summary>
        protected const string SystemSettingLastFileModifier = "BMO.LastFileModifier";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastItemSequenceNumberKey = "BMO.LastItemSequenceNumber";

        #endregion

        /// <summary>
        /// Gets the next item sequence number.
        /// </summary>
        /// <returns>An integer that identifies the unique item sequence number that can be used.</returns>
        protected int GetNextItemSequenceNumber()
        {
            int lastSequence = GetSystemSetting( LastItemSequenceNumberKey ).AsIntegerOrNull() ?? 0;
            int nextSequence = lastSequence + 1;

            SetSystemSetting( LastItemSequenceNumberKey, nextSequence.ToString() );

            return nextSequence;
        }

        /// <summary>
        /// Gets the file header record (type 01).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <returns>
        /// A FileHeader record.
        /// </returns>
        protected override FileHeader GetFileHeaderRecord( ExportOptions options )
        {
            var header = base.GetFileHeaderRecord( options );

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
            var lastModifier = GetSystemSetting( SystemSettingLastFileModifier );
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
            SetSystemSetting( SystemSettingLastFileModifier, string.Join( "|", hash, fileIdModifier ) );

            return header;
        }

        /// <summary>
        /// Gets the cash letter header record (type 10).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <returns>
        /// A CashLetterHeader record.
        /// </returns>
        protected override CashLetterHeader GetCashLetterHeaderRecord( ExportOptions options )
        {
            int cashHeaderId = GetSystemSetting( SystemSettingNextCashHeaderId ).AsIntegerOrNull() ?? 10000001;

            var header = base.GetCashLetterHeaderRecord( options );
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "AccountNumber" ) );

            header.ClientInstitutionRoutingNumber = accountNumber;
            header.ID = cashHeaderId.ToString( "D8" );
            SetSystemSetting( SystemSettingNextCashHeaderId, ( cashHeaderId + 1 ).ToString() );

            return header;
        }

        /// <summary>
        /// Gets the bundle header record (Type 20)
        /// </summary>
        /// <param name="options"></param>
        /// <param name="bundleIndex"></param>
        /// <returns></returns>
        protected override BundleHeader GetBundleHeader( ExportOptions options, int bundleIndex )
        {
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "AccountNumber" ) );
            var bundleHeader = base.GetBundleHeader( options, bundleIndex );
            bundleHeader.ClientInstitutionRoutingNumber = accountNumber;

            return bundleHeader;
        }

        /// <summary>
        /// Gets the credit detail deposit record (type 61).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="bundleIndex">Number of existing bundle records in the cash letter.</param>
        /// <param name="transactions">The transactions associated with this deposit.</param>
        /// <returns>
        /// A collection of records.
        /// </returns>
        protected override List<Record> GetCreditDetailRecords( ExportOptions options, int bundleIndex, List<FinancialTransaction> transactions )
        {
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "AccountNumber" ) );
            var depositAccountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "DepositAccountNumber" ) );
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "RoutingNumber" ) );
            var imageViewRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "ImageViewRoutingNumber" ) );

            //if deposit account number is given, override accountNumber
            if ( depositAccountNumber.IsNotNullOrWhiteSpace() )
            {
                accountNumber = depositAccountNumber;
            }

            if ( imageViewRoutingNumber.IsNotNullOrWhiteSpace() )
            {
                routingNumber = imageViewRoutingNumber;
            }

            var records = new List<Record>();

            var creditDetail = new CreditDetail
            {
                PayorRoutingNumber = "572100001",
                CreditAccountNumber = accountNumber + "/",
                Amount = transactions.Sum( t => t.TotalAmount ),
                InstitutionItemSequenceNumber = GetNextItemSequenceNumber().ToString( "000000000000000" ),
                DebitCreditIndicator = "2"
            };
            records.Add( creditDetail );

            for ( int i = 0; i < 2; i++ )
            {
                using ( var ms = GetDepositSlipImage(options, creditDetail, i == 0 ) )
                {
                    //
                    // Get the Image View Detail record (type 50).
                    //
                    var detail = new ImageViewDetail
                    {
                        ImageIndicator = 1,
                        ImageCreatorRoutingNumber = routingNumber,
                        ImageCreatorDate = options.ExportDateTime,
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
                        BundleBusinessDate = options.BusinessDateTime,
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
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transaction">The transaction to be deposited.</param>
        /// <returns>
        /// A collection of records.
        /// </returns>
        protected override List<Record> GetItemRecords( ExportOptions options, FinancialTransaction transaction )
        {
            var records = base.GetItemRecords( options, transaction );
            records = records.Where( r => r.RecordType != 26 ).ToList();

            var sequenceNumber = GetNextItemSequenceNumber();

            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "RoutingNumber" ) );
            var imageViewRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "ImageViewRoutingNumber" ) );

            if ( imageViewRoutingNumber.IsNotNullOrWhiteSpace() )
            {
                routingNumber = imageViewRoutingNumber;
            }

            //
            // Modify the Check Detail Record and Check Image Data records to have
            // a unique item sequence number.
            //
            var checkDetail = records.Where( r => r.RecordType == 25 ).Cast<dynamic>().FirstOrDefault();
            checkDetail.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );

            foreach ( var imageData in records.Where( r => r.RecordType == 52 ).Cast<dynamic>() )
            {
                imageData.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );
                imageData.InstitutionRoutingNumber = routingNumber;
            }

            foreach ( var imageDetail in records.Where( r => r.RecordType == 50 ).Cast<dynamic>() )
            {
                imageDetail.ImageCreatorRoutingNumber = routingNumber;
            }

            //Personal Checks VS Bank Checks: On-Us vs Auxiliary
            string micr = Encryption.DecryptString( transaction.CheckMicrEncrypted ).Trim();
            int startC = micr.IndexOf( 'c' );
            if( startC == 0 && checkDetail != null )
            {
                int endC = micr.Substring( startC + 1 ).IndexOf( 'c' );
                int length = endC - startC;
                string possibleBankCheckAux = micr.Substring( startC + 1, length );
                if( !possibleBankCheckAux.ToCharArray().Any( c => c == 'c' || c == 'd' ) ) // check for any breaks or interruptions
                {
                    // bank check found; set aux and on us per Bank of the West doc
                    checkDetail.AuxiliaryOnUs = possibleBankCheckAux;
                    checkDetail.OnUs = checkDetail.OnUs.Substring( 0, checkDetail.OnUs.IndexOf( '/' ) + 1 );
                }
            }
            return records;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        protected override CashLetterControl GetCashLetterControlRecord( ExportOptions options, List<Record> records )
        {
            var controlRecord = base.GetCashLetterControlRecord( options, records );
            //count deposit records as well as items
            controlRecord.ItemCount = records.Where( r => r.RecordType == 25 || r.RecordType == 61 ).Count();

            //Bank of the West: Modified 1/18/2021. SC.
            controlRecord.SettlementDate = GetAttributeValue( options.FileFormat, "AddSettlementDate" ).AsBoolean() ? controlRecord.SettlementDate : null;

            return controlRecord;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        protected override FileControl GetFileControlRecord( ExportOptions options, List<Record> records )
        {
            var controlRecord = base.GetFileControlRecord( options, records );
            //count deposit records as well as items
            controlRecord.TotalItemCount = records.Where( r => r.RecordType == 25 || r.RecordType == 61 ).Count();

            return controlRecord;
        }

        /// <summary>
        /// Gets the credit detail deposit record (type 61).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transactions">The transactions associated with this deposit.</param>
        /// <param name="isFrontSide">True if the image to be retrieved is the front image.</param>
        /// <returns>A stream that contains the image data in TIFF 6.0 CCITT Group 4 format.</returns>
        protected virtual Stream GetDepositSlipImage( ExportOptions options, CreditDetail creditDetail, bool isFrontSide )
        {
            var bitmap = new System.Drawing.Bitmap( 1200, 550 );
            var g = System.Drawing.Graphics.FromImage( bitmap );

            var depositSlipTemplate = GetAttributeValue( options.FileFormat, "DepositSlipTemplate" );
            var mergeFields = new Dictionary<string, object>
            {
                { "FileFormat", options.FileFormat },
                { "Amount", creditDetail.Amount.ToString( "C" ) }
            };
            var depositSlipText = depositSlipTemplate.ResolveMergeFields( mergeFields, null );

            //
            // Ensure we are opague with white.
            //
            g.FillRectangle( System.Drawing.Brushes.White, new System.Drawing.Rectangle( 0, 0, 1200, 550 ) );

            //if ( isFrontSide )
            //{
                g.DrawString( depositSlipText,
                    new System.Drawing.Font( "Tahoma", 30 ),
                    System.Drawing.Brushes.Black,
                    new System.Drawing.PointF( 50, 50 ) );
            //}

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
