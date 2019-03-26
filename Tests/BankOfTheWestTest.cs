using System;
using System.Collections.Generic;

using com.shepherdchurch.ImageCashLetter.FileFormatTypes;
using com.shepherdchurch.ImageCashLetter.Model;

using Xunit;

namespace com.shepherdchurch.ImageCashLetter.Tests
{
    public class BankOfTheWestTest
    {
        #region Test Empty Batch

        /// <summary>
        /// Verifies that an empty batch matches the expected hash value.
        /// </summary>
        [Fact]
        void TestEmptyBatchHash()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            Assert.Equal( "0x39046C12462D0C4111E08E2BFE777EF8A661509E", TestTools.Hash( stream ) );
        }

        /// <summary>
        /// Verifies that an empty batch matches the expected record count.
        /// </summary>
        [Fact]
        void TestEmptyBatchRecordCount()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            Assert.Equal( 4, x937.Records.Count );
        }

        /// <summary>
        /// Verifies all the values in the File Header record of an empty batch.
        /// </summary>
        [Fact]
        void TestEmptyBatchFileHeader()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[0] as X937.Records.FileHeader;

            Assert.NotNull( record );
            Assert.Equal( "US", record.CountryCode );
            Assert.Equal( options.ExportDateTime.TruncateToMinutes(), record.FileCreationDateTime );
            Assert.Equal( "A", record.FileIdModifier );
            Assert.Equal( "T", record.FileTypeIndicator );
            Assert.Equal( options.FileFormat.GetAttributeValue( "DestinationName" ), record.ImmediateDestinationName );
            Assert.Equal( Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "RoutingNumber" ) ), record.ImmediateDestinationRoutingNumber );
            Assert.Equal( options.FileFormat.GetAttributeValue( "OriginName" ), record.ImmediateOriginName );
            Assert.Equal( Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "AccountNumber" ) ), record.ImmediateOriginRoutingNumber );
            Assert.Equal( 1, record.RecordType );
            Assert.Equal( "N", record.ResendIndicator );
            Assert.Equal( 3, record.StandardLevel );
            Assert.Equal( string.Empty, record.UserField );
        }

        /// <summary>
        /// Verifies all the values in the File Control record of an empty batch.
        /// </summary>
        [Fact]
        void TestEmptyBatchFileControl()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[3] as X937.Records.FileControl;

            Assert.NotNull( record );
            Assert.Equal( 1, record.CashLetterCount );
            Assert.Equal( options.FileFormat.GetAttributeValue( "ContactName" ), record.ImmediateOriginContactName );
            Assert.Equal( options.FileFormat.GetAttributeValue( "ContactPhone" ), record.ImmediateOriginContactPhoneNumber );
            Assert.Equal( 99, record.RecordType );
            Assert.Equal( 0, record.TotalAmount );
            Assert.Equal( 0, record.TotalItemCount );
            Assert.Equal( 4, record.TotalRecordCount );
        }

        /// <summary>
        /// Verifies all the values in the Cash Letter Header record of an empty batch.
        /// </summary>
        [Fact]
        void TestEmptyBatchCashLetterHeader()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[1] as X937.Records.CashLetterHeader;

            Assert.NotNull( record );
            Assert.Equal( options.BusinessDateTime.Date, record.BusinessDate );
            Assert.Equal( Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "RoutingNumber" ) ), record.ClientInstitutionRoutingNumber );
            Assert.Equal( 1, record.CollectionTypeIndicator );
            Assert.Equal( options.ExportDateTime.TruncateToMinutes(), record.CreationDateTime );
            Assert.Equal( Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "RoutingNumber" ) ), record.DestinationRoutingNumber );
            Assert.Equal( "G", record.DocumentationTypeIndicator );
            Assert.Equal( "00000000", record.ID );
            Assert.Equal( options.FileFormat.GetAttributeValue( "ContactName" ), record.OriginatorContactName );
            Assert.Equal( options.FileFormat.GetAttributeValue( "ContactPhone" ), record.OriginatorContactPhoneNumber );
            Assert.Equal( 10, record.RecordType );
            Assert.Equal( "I", record.RecordTypeIndicator );
            Assert.Equal( string.Empty, record.UserField );
            Assert.Equal( string.Empty, record.WorkType );
        }

        /// <summary>
        /// Verifies all the values in the Cash Letter Control record of an empty batch.
        /// </summary>
        [Fact]
        void TestEmptyBatchCashLetterControl()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new Rock.Model.FinancialBatch[] { } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[2] as X937.Records.CashLetterControl;

            Assert.NotNull( record );
            Assert.Equal( 0, record.BundleCount );
            Assert.Equal( options.FileFormat.GetAttributeValue( "OriginName" ), record.ECEInstitutionName );
            Assert.Equal( 0, record.ImageCount );
            Assert.Equal( 0, record.ItemCount );
            Assert.Equal( 90, record.RecordType );
            Assert.Null( record.SettlementDate );
            Assert.Equal( 0, record.TotalAmount );
        }

        #endregion

        #region Test Alpha Batch

        /// <summary>
        /// Verifies the SHA1 hash of an Alpha batch export.
        /// </summary>
        [Fact]
        public void TestAlphaBatchHash()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );

            var stream = component.ExportBatches( options, out var errorMessages );

            Assert.Equal( "0x187C82DE0EFF6742055C7C061EAF832542975712", TestTools.Hash( stream ) );
        }

        /// <summary>
        /// Verifies the record count of an alpha batch.
        /// </summary>
        [Fact]
        public void TestAlphaBatchRecordCount()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            Assert.Equal( 29, x937.Records.Count );
        }

        /// <summary>
        /// Verifies all the values in the Bundle Header record of an alpha batch.
        /// </summary>
        [Fact]
        void TestAlphaBatchBundleHeader()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[2] as X937.Records.BundleHeader;

            var routingNumber = Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "RoutingNumber" ) );

            Assert.NotNull( record );
            Assert.Equal( options.BusinessDateTime.Date, record.BusinessDate );
            Assert.Equal( routingNumber, record.ClientInstitutionRoutingNumber );
            Assert.Equal( 1, record.CollectionTypeIndicator );
            Assert.Equal( options.ExportDateTime.Date, record.CreationDate );
            Assert.Equal( string.Empty, record.CycleNumber );
            Assert.Equal( routingNumber, record.DestinationRoutingNumber );
            Assert.Equal( string.Empty, record.ID );
            Assert.Equal( 20, record.RecordType );
            Assert.Equal( routingNumber, record.ReturnLocationRoutingNumber );
            Assert.Equal( "1", record.SequenceNumber );
            Assert.Equal( string.Empty, record.UserField );
        }

        /// <summary>
        /// Verifies all the values in the Bundle Control record of an alpha batch.
        /// </summary>
        [Fact]
        void TestAlphaBatchBundleControl()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[26] as X937.Records.BundleControl;

            Assert.NotNull( record );
            Assert.Equal( 8, record.ImageCount );
            Assert.Equal( 4, record.ItemCount );
            Assert.Equal( 425, record.MICRValidTotalAmount );
            Assert.Equal( 70, record.RecordType );
            Assert.Equal( 425, record.TotalAmount );
            Assert.Equal( string.Empty, record.UserField );
        }

        /// <summary>
        /// Verifies all the values in the Credit Detail record of an empty batch.
        /// </summary>
        [Fact]
        void TestAlphaBatchCreditDetail()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );
            var accountNumber = Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "AccountNumber" ) );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[3] as X937.Records.CreditDetail;

            Assert.NotNull( record );
            Assert.Equal( 425, record.Amount );
            Assert.Equal( string.Empty, record.AuxiliaryOnUs );
            Assert.Equal( accountNumber + "/", record.CreditAccountNumber );
            Assert.Equal( "2", record.DebitCreditIndicator );
            Assert.Equal( string.Empty, record.DocumentTypeIndicator );
            Assert.Equal( string.Empty, record.ExternalProcessingCode );
            Assert.Equal( "000000000000001", record.InstitutionItemSequenceNumber );
            Assert.Equal( "500100015", record.PayorRoutingNumber );
            Assert.Equal( 61, record.RecordType );
            Assert.Equal( string.Empty, record.SourceOfWorkCode );
            Assert.Equal( string.Empty, record.TypeOfAccountCode );
            Assert.Equal( string.Empty, record.WorkType );
        }

        /// <summary>
        /// Verifies all the values in the Check Detail record of an empty batch.
        /// </summary>
        [Fact]
        void TestAlphaBatchCheckDetail()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[8] as X937.Records.CheckDetail;

            Assert.NotNull( record );
            Assert.Equal( string.Empty, record.ArchiveTypeIndicator );
            Assert.Equal( string.Empty, record.AuxiliaryOnUs );
            Assert.Equal( "Y", record.BankOfFirstDepositIndicator );
            Assert.Equal( 1, record.CheckDetailRecordAddendumCount );
            Assert.Equal( "000000000000002", record.ClientInstitutionItemSequenceNumber );
            Assert.Equal( string.Empty, record.CorrectionIndicator );
            Assert.Equal( "G", record.DocumentationTypeIndicator );
            Assert.Equal( string.Empty, record.ElectronicReturnAcceptanceIndicator );
            Assert.Equal( string.Empty, record.ExternalProcessingCode );
            Assert.Equal( 200, record.ItemAmount );
            Assert.Null( record.MICRValidIndicator );
            Assert.Equal( "123-456-7/5431", record.OnUs );
            Assert.Equal( "12345678", record.PayorBankRoutingNumber );
            Assert.Equal( "0", record.PayorBankRoutingNumberCheckDigit );
            Assert.Equal( 25, record.RecordType );
        }

        /// <summary>
        /// Verifies all the values in the Check Detail Addendum A record of an empty batch.
        /// </summary>
        [Fact]
        void TestAlphaBatchCheckDetailAddendumA()
        {
            var component = new BankOfTheWestMock( null );
            var options = GetExportOptionsAlpha( new[] { TestTools.GetFinancialBatchAlpha() } );
            var routingNumber = Rock.Security.Encryption.DecryptString( options.FileFormat.GetAttributeValue( "RoutingNumber" ) );

            var stream = component.ExportBatches( options, out var errorMessages );

            var x937 = new X937.X937File( stream );

            var record = x937.Records[9] as X937.Records.CheckDetailAddendumA;

            Assert.NotNull( record );
            Assert.Equal( string.Empty, record.BankOfFirstDepositAccountNumber );
            Assert.Equal( string.Empty, record.BankOfFirstDepositBranch );
            Assert.Equal( options.BusinessDateTime.Date, record.BankOfFirstDepositBusinessDate );
            Assert.Equal( "2", record.BankOfFirstDepositConversionIndicator );
            Assert.Equal( "0", record.BankOfFirstDepositCorrectionIndicator );
            Assert.Equal( string.Empty, record.BankOfFirstDepositItemSequenceNumber );
            Assert.Equal( routingNumber, record.BankOfFirstDepositRoutingNumber );
            Assert.Equal( string.Empty, record.PayeeName );
            Assert.Equal( 1, record.RecordNumber );
            Assert.Equal( 26, record.RecordType );
            Assert.Equal( "N", record.TruncationIndicator );
            Assert.Equal( string.Empty, record.UserField );
        }

        #endregion

        #region Support Methods

        /// <summary>
        /// Gets the file format record used during testing.
        /// </summary>
        /// <returns></returns>
        private static ImageCashLetterFileFormat GetFileFormat()
        {
            var fileFormat = new ImageCashLetterFileFormat();

            fileFormat.AttributeValues = new Dictionary<string, Rock.Web.Cache.AttributeValueCache>
            {
                { "RoutingNumber", new Rock.Web.Cache.AttributeValueCache { Value = Rock.Security.Encryption.EncryptString( "121100782" ) } },
                { "AccountNumber", new Rock.Web.Cache.AttributeValueCache { Value = Rock.Security.Encryption.EncryptString( "012345678" ) } },
                { "InstitutionRoutingNumber", new Rock.Web.Cache.AttributeValueCache { Value = "921100782" } },
                { "DestinationName", new Rock.Web.Cache.AttributeValueCache { Value = "Bank of the West" } },
                { "OriginName", new Rock.Web.Cache.AttributeValueCache { Value = "Rock Solid Church" } },
                { "ContactName", new Rock.Web.Cache.AttributeValueCache { Value = "Ted Decker" } },
                { "ContactPhone", new Rock.Web.Cache.AttributeValueCache { Value = "2135551122" } },
                { "TestMode", new Rock.Web.Cache.AttributeValueCache { Value = "True" } },
                { "DepositSlipTemplate", new Rock.Web.Cache.AttributeValueCache { Value = "Deposit Amount: {{ Amount }}" } }
            };

            return fileFormat;
        }

        /// <summary>
        /// Gets the export options alpha.
        /// </summary>
        /// <param name="batches">The batches.</param>
        /// <returns></returns>
        public static ExportOptions GetExportOptionsAlpha( IEnumerable<Rock.Model.FinancialBatch> batches )
        {
            return new ExportOptions( GetFileFormat(), batches )
            {
                BusinessDateTime = new DateTime( 2019, 1, 13, 12, 0, 0, 0 ),
                ExportDateTime = new DateTime( 2019, 1, 15, 14, 30, 27, 0 )
            };
        }

        #endregion

        #region Support Classes

        /// <summary>
        /// A mock of the BankOfTheWest component that doesn't require DB access.
        /// </summary>
        /// <seealso cref="com.shepherdchurch.ImageCashLetter.FileFormatTypes.BankOfTheWest" />
        private class BankOfTheWestMock : BankOfTheWest
        {
            private readonly Dictionary<string, string> _systemSettings;

            public BankOfTheWestMock( Dictionary<string, string> systemSettings )
            {
                _systemSettings = systemSettings ?? new Dictionary<string, string>();
                EnabledLavaCommands = string.Empty;
            }

            protected override string GetSystemSetting( string key )
            {
                return _systemSettings.ContainsKey( key ) ? _systemSettings[key] : string.Empty;
            }

            protected override void SetSystemSetting( string key, string value )
            {
                _systemSettings[key] = value;
            }
        }
        
        #endregion
    }
}
