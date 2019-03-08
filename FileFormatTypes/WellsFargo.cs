using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;

using Rock;
//using Rock.Attribute;
using Rock.Model;

using X937;
using X937.Attributes;
using X937.Records;


namespace com.shepherdchurch.ImageCashLetter.FileFormatTypes
{
    /// <summary>
    /// Wells Fargo export file format
    /// </summary>
    [Description( "Processes a batch export for Wells Fargo." )]
    [Export( typeof( FileFormatTypeComponent ) )]
    [ExportMetadata( "ComponentName", "Wells Fargo" )]
    [Rock.Attribute.TextField("Bank Location Number", "A 3 Digit Bank Location Number", true, "031", "", 8 )]
    public class WellsFargo : X937DSTU
    {

        #region Fields

        protected int _cachHeaderId = 1;

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
            int bankLocationNumber = GetAttributeValue( options.FileFormat, "Bank Location Number" ).AsIntegerOrNull() ?? 31;
            int cashHeaderId = _cachHeaderId++;

            var header = base.GetCashLetterHeaderRecord( options );
            header.ID = "2" + bankLocationNumber.ToString("D3") + cashHeaderId.ToString( "D4" );

            header.WorkType = "C";

            return header;
        }

        protected override BundleHeader GetBundleHeader( ExportOptions options, int bundleIndex )
        {
            var header = base.GetBundleHeader( options, bundleIndex );
            header.CycleNumber = "01";
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
            checkDetail.BankOfFirstDepositIndicator = "U";
            checkDetail.CheckDetailRecordAddendumCount = 0;
            checkDetail.ArchiveTypeIndicator = "B";

            foreach ( var imageData in records.Where( r => r.RecordType == 52 ).Cast<dynamic>() )
            {
                imageData.CycleNumber = "01";
                imageData.ClientInstitutionItemSequenceNumber = sequenceNumber.ToString( "000000000000000" );
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
