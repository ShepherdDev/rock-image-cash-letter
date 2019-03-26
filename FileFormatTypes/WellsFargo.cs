using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

using Rock;
//using Rock.Attribute;
using Rock.Model;

using X937;
using X937.Attributes;
using X937.Records;

using BitMiracle.LibTiff.Classic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace com.shepherdchurch.ImageCashLetter.FileFormatTypes
{
    /// <summary>
    /// Wells Fargo export file format
    /// </summary>
    [Description( "Processes a batch export for Wells Fargo." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Wells Fargo" )]
    [Rock.Attribute.TextField( "Bank Location Number", "A 3 Digit Bank Location Number", true, "031", "", 8 )]
    [Rock.Attribute.TextField( "Remote ID (RID)", "Remote ID provided by Wells Fargo", false, "", "", 9, "RID" )]
    [Rock.Attribute.TextField( "Batch ID (BID)", "Batch ID provided by Wells Fargo", false, "", "", 10, "BID" )]
    public class WellsFargo : X937DSTU
    {

        #region Fields

        protected int _cashHeaderId = 1;

        #endregion

        #region System Setting Keys

        /// <summary>
        /// The system setting that contains the last file modifier we used.
        /// </summary>
        protected const string SystemSettingLastFileModifier = "WellsFarge.LastFileModifier";

        /// <summary>
        /// The last item sequence number used for items.
        /// </summary>
        protected const string LastItemSequenceNumberKey = "WellsFargo.LastItemSequenceNumber";

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

        public override int MaxItemsPerBundle => 299;

        /// <summary>
        /// Write's the $$ADD record prior to encoded contents.
        /// </summary>
        protected override void WritePreContent( ExportOptions options, MemoryStream stream )
        {
            string rid = GetAttributeValue( options.FileFormat, "RID" );
            string bid = GetAttributeValue( options.FileFormat, "BID" );
            string addLine = $"$$ADD ID={rid} BID='{bid}'{Environment.NewLine}";
            using ( var writer = new BinaryWriter( stream, System.Text.Encoding.UTF8, true ) )
            {
                writer.Write( Encoding.UTF8.GetBytes( addLine ) );
            }
        }

        /// <summary>
        /// Write's a record withouth the record length
        /// </summary>
        protected override void WriteRecord( Record record, BinaryWriter writer )
        {
            record.Encode( writer, false );
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
            header.ImmediateOriginRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "InstitutionRoutingNumber" ) );
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
                        fileIdModifier = ( (char)( components[1][0] + 1 ) ).ToString();

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
            int bankLocationNumber = GetAttributeValue( options.FileFormat, "BankLocationNumber" ).AsIntegerOrNull() ?? 31;
            int cashHeaderId = _cashHeaderId++;

            var header = base.GetCashLetterHeaderRecord( options );
            header.ID = "2" + bankLocationNumber.ToString( "D3" ) + cashHeaderId.ToString( "D4" );
            header.ClientInstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "InstitutionRoutingNumber" ) );

            header.WorkType = "C";

            return header;
        }

        protected override BundleHeader GetBundleHeader( ExportOptions options, int bundleIndex )
        {
            var header = base.GetBundleHeader( options, bundleIndex );
            header.CycleNumber = "01";
            header.ClientInstitutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "InstitutionRoutingNumber" ) );
            return header;
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
        protected override List<X937.Record> GetCreditDetailRecords( ExportOptions options, int bundleIndex, List<FinancialTransaction> transactions )
        {
            var records = new List<X937.Record>();

            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "AccountNumber" ) );
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "RoutingNumber" ) );

            var creditDetail = new WellsFargoCreditDetail
            {
                Amount = transactions.Sum( t => t.TotalAmount ),
                CreditAccountNumber = accountNumber,
                ProcessControl = "  6586",
                PayorRoutingNumber = "500000377",
                InstitutionItemSequenceNumber = GetNextItemSequenceNumber().ToString( "000000000000000" ),
                SourceOfWorkCode = "3"
            };
            records.Add( creditDetail );

            return records;
        }

        protected override List<Record> GetItemDetailRecords( ExportOptions options, FinancialTransaction transaction )
        {
            var records = base.GetItemDetailRecords( options, transaction );
            records.RemoveAll( r => r.RecordType == 26 );
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
            var sequenceNumber = GetNextItemSequenceNumber();

            //
            // Modify the Check Detail Record and Check Image Data records to have
            // a unique item sequence number.
            //
            var checkDetail = records.Where( r => r.RecordType == 25 ).Cast<dynamic>().FirstOrDefault();
            checkDetail.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );
            checkDetail.ExternalProcessingCode = " ";
            checkDetail.BankOfFirstDepositIndicator = "U";
            checkDetail.CheckDetailRecordAddendumCount = 0;
            checkDetail.ArchiveTypeIndicator = "B";

            var micr = GetMicrInstance( transaction.CheckMicrEncrypted );
            checkDetail.OnUs = micr.GetOnUs().Replace( "c", "/" );

            var institutionRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( options.FileFormat, "InstitutionRoutingNumber" ) );

            foreach ( var imageDetail in records.Where( r => r.RecordType == 50 ).Cast<dynamic>() )
            {
                imageDetail.ImageCreatorRoutingNumber = institutionRoutingNumber;
                imageDetail.ImageRecreateIndicator = 0;
            }

            foreach ( var imageData in records.Where( r => r.RecordType == 52 ).Cast<dynamic>() )
            {
                imageData.InstitutionRoutingNumber = institutionRoutingNumber;
                imageData.CycleNumber = "01";
                imageData.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );
            }

            return records;
        }

        /// <summary>
        /// Gets the image record for a specific transaction image (type 50 and 52).
        /// </summary>
        /// <param name="options">Export options to be used by the component.</param>
        /// <param name="transaction">The transaction being deposited.</param>
        /// <param name="image">The check image scanned by the scanning application.</param>
        /// <param name="isFront">if set to <c>true</c> [is front].</param>
        /// <returns>A collection of records.</returns>
        protected override List<Record> GetImageRecords( ExportOptions options, FinancialTransaction transaction, FinancialTransactionImage image, bool isFront )
        {
            var records = base.GetImageRecords( options, transaction, image, isFront );

            var detail = records.Where( r => r.RecordType == 50 ).Cast<dynamic>().FirstOrDefault();
            var data = records.Where( r => r.RecordType == 52 ).Cast<dynamic>().FirstOrDefault();
            if ( detail != null && data != null )
            {
                detail.DataSize = data.ImageData.Length;
            }
            return records;
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

        protected override Stream ConvertImageToTiffG4( Stream imageStream )
        {
            var bitmap = new Bitmap( imageStream );
            bitmap.SetResolution( 200, 200 );

            var bytes = GetTiffImageBytes( bitmap );

            return new MemoryStream( bytes );
        }

        public static byte[] GetTiffImageBytes( Bitmap img )
        {
            try
            {
                // Invert the white/black bytes
                byte[] raster = GetImageRasterBytes( img );
                for( int k = 0; k < raster.Length; k++ )
                {
                    raster[k] = (byte)( ~raster[k] );
                }

                using ( MemoryStream ms = new MemoryStream() )
                {
                    using ( Tiff tif = Tiff.ClientOpen( "InMemory", "w", ms, new TiffStream() ) )
                    {
                        if ( tif == null )
                            return null;

                        tif.SetField( TiffTag.IMAGEWIDTH, img.Width );
                        tif.SetField( TiffTag.IMAGELENGTH, img.Height );
                        tif.SetField( TiffTag.COMPRESSION, Compression.CCITTFAX4 );
                        tif.SetField( TiffTag.PHOTOMETRIC, Photometric.MINISWHITE );
                        tif.SetField( TiffTag.STRIPOFFSETS, 1 );
                        tif.SetField( TiffTag.ROWSPERSTRIP, img.Height );
                        tif.SetField( TiffTag.STRIPBYTECOUNTS, 1 );
                        tif.SetField( TiffTag.XRESOLUTION, img.HorizontalResolution );
                        tif.SetField( TiffTag.YRESOLUTION, img.VerticalResolution );
                        tif.SetField( TiffTag.RESOLUTIONUNIT, ResUnit.INCH );

                        int tiffStride = tif.ScanlineSize();
                        int stride = raster.Length / img.Height;

                        if ( tiffStride < stride )
                        {
                            // raster stride is bigger than TIFF stride
                            // this is due to padding in raster bits
                            // we need to create correct TIFF strip and write it into TIFF

                            byte[] stripBits = new byte[tiffStride * img.Height];
                            for ( int i = 0, rasterPos = 0, stripPos = 0; i < img.Height; i++ )
                            {
                                System.Buffer.BlockCopy( raster, rasterPos, stripBits, stripPos, tiffStride );
                                rasterPos += stride;
                                stripPos += tiffStride;
                            }

                            // Write the information to the file
                            int n = tif.WriteEncodedStrip( 0, stripBits, stripBits.Length );
                            if ( n <= 0 )
                                return null;
                        }
                        else
                        {
                            // Write the information to the file
                            int n = tif.WriteEncodedStrip( 0, raster, raster.Length );
                            if ( n <= 0 )
                                return null;
                        }
                        
                    }

                    return ms.GetBuffer();
                }
            }
            catch ( Exception )
            {
                return null;
            }
        }

        public static byte[] GetImageRasterBytes( Bitmap img )
        {
            // Specify full image
            Rectangle rect = new Rectangle( 0, 0, img.Width, img.Height );

            Bitmap bmp = img;
            byte[] bits = null;

            try
            {
                // Lock the managed memory
                if ( img.PixelFormat != PixelFormat.Format1bppIndexed )
                    bmp = convertToBitonal( img );

                BitmapData bmpdata = bmp.LockBits( rect, ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed );

                // Declare an array to hold the bytes of the bitmap.
                bits = new byte[bmpdata.Stride * bmpdata.Height];

                // Copy the sample values into the array.
                Marshal.Copy( bmpdata.Scan0, bits, 0, bits.Length );

                // Release managed memory
                bmp.UnlockBits( bmpdata );
            }
            finally
            {
                if ( bmp != img )
                    bmp.Dispose();
            }

            return bits;
        }

        private static Bitmap convertToBitonal( Bitmap original )
        {
            int sourceStride;
            byte[] sourceBuffer = extractBytes( original, out sourceStride );

            // Create destination bitmap
            Bitmap destination = new Bitmap( original.Width, original.Height,
                PixelFormat.Format1bppIndexed );

            destination.SetResolution( original.HorizontalResolution, original.VerticalResolution );

            // Lock destination bitmap in memory
            BitmapData destinationData = destination.LockBits(
                new Rectangle( 0, 0, destination.Width, destination.Height ),
                ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed );

            // Create buffer for destination bitmap bits
            int imageSize = destinationData.Stride * destinationData.Height;
            byte[] destinationBuffer = new byte[imageSize];

            int sourceIndex = 0;
            int destinationIndex = 0;
            int pixelTotal = 0;
            byte destinationValue = 0;
            int pixelValue = 128;
            int height = destination.Height;
            int width = destination.Width;
            int threshold = 500;

            for ( int y = 0; y < height; y++ )
            {
                sourceIndex = y * sourceStride;
                destinationIndex = y * destinationData.Stride;
                destinationValue = 0;
                pixelValue = 128;

                for ( int x = 0; x < width; x++ )
                {
                    // Compute pixel brightness (i.e. total of Red, Green, and Blue values)
                    pixelTotal = sourceBuffer[sourceIndex + 1] + sourceBuffer[sourceIndex + 2] +
                        sourceBuffer[sourceIndex + 3];

                    if ( pixelTotal > threshold )
                        destinationValue += (byte)pixelValue;

                    if ( pixelValue == 1 )
                    {
                        destinationBuffer[destinationIndex] = destinationValue;
                        destinationIndex++;
                        destinationValue = 0;
                        pixelValue = 128;
                    }
                    else
                    {
                        pixelValue >>= 1;
                    }

                    sourceIndex += 4;
                }

                if ( pixelValue != 128 )
                    destinationBuffer[destinationIndex] = destinationValue;
            }

            Marshal.Copy( destinationBuffer, 0, destinationData.Scan0, imageSize );
            destination.UnlockBits( destinationData );
            return destination;
        }

        private static byte[] extractBytes( Bitmap original, out int stride )
        {
            Bitmap source = null;

            try
            {
                // If original bitmap is not already in 32 BPP, ARGB format, then convert
                if ( original.PixelFormat != PixelFormat.Format32bppArgb )
                {
                    source = new Bitmap( original.Width, original.Height, PixelFormat.Format32bppArgb );
                    source.SetResolution( original.HorizontalResolution, original.VerticalResolution );
                    using ( Graphics g = Graphics.FromImage( source ) )
                    {
                        g.DrawImageUnscaled( original, 0, 0 );
                    }
                }
                else
                {
                    source = original;
                }

                // Lock source bitmap in memory
                BitmapData sourceData = source.LockBits(
                    new Rectangle( 0, 0, source.Width, source.Height ),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );

                // Copy image data to binary array
                int imageSize = sourceData.Stride * sourceData.Height;
                byte[] sourceBuffer = new byte[imageSize];
                Marshal.Copy( sourceData.Scan0, sourceBuffer, 0, imageSize );

                // Unlock source bitmap
                source.UnlockBits( sourceData );

                stride = sourceData.Stride;
                return sourceBuffer;
            }
            finally
            {
                if ( source != original )
                    source.Dispose();
            }

        }
    }

    public class WellsFargoCreditDetail : Record
    {
        [MoneyField( 2, 12 )]
        public decimal Amount { get; set; }

        [TextField( 3, 17, FieldJustification.Right )]
        public string CreditAccountNumber { get; set; }

        [TextField( 4, 6, FieldJustification.Right )]
        public string ProcessControl { get; set; }

        [TextField( 5, 9 )]
        public string PayorRoutingNumber { get; set; }

        [TextField( 6, 15, FieldJustification.Right )]
        public string AuxiliaryOnUs { get; set; }

        [TextField( 7, 15, FieldJustification.Right )]
        public string InstitutionItemSequenceNumber { get; set; }

        [TextField( 8, 1 )]
        public string ExternalProcessingCode { get; set; }

        [TextField( 9, 1 )]
        public string TypeOfAccountCode { get; set; }

        [TextField( 10, 1 )]
        public string SourceOfWorkCode { get; set; }

        [TextField( 11, 1 )]
        protected string Reserved { get; set; }

        public WellsFargoCreditDetail()
            : base( 61 )
        {
        }
    }
}
