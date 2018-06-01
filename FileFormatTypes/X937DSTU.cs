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
    /// <summary>
    /// Defines the basic functionality of any component that will be exporting using the X9.37
    /// DSTU standard.
    /// </summary>
    [EncryptedTextField( "Routing Number", "Your bank account routing number.", false, "", order: 0 )]
    [EncryptedTextField( "Account Number", "Your bank account number.", false, "", order: 1 )]
    [EncryptedTextField( "Institution Routing Number", "This is defined by your bank, it is usually either the bank routing number or a customer number.", false, "", order: 2 )]
    [TextField( "Destination Name", "The name of the bank the deposit will be made to.", false, "", order: 3 )]
    [TextField( "Origin Name", "The name of the church.", false, "", order: 4 )]
    [TextField( "Contact Name", "The name of the person the bank will contact if there are issues.", false, "", order: 5 )]
    [TextField( "Contact Phone", "The phone number the bank will call if there are issues.", false, "", order: 6 )]
    [BooleanField( "Test Mode", "If true then the generated files will be marked as test-mode.", true, order: 7 )]
    public abstract class X937DSTU : FileFormatTypeComponent
    {
        /// <summary>
        /// Gets the maximum items per bundle. Most banks limit the number of checks that
        /// can exist in each bundle. This specifies what that maximum is.
        /// </summary>
        public virtual int MaxItemsPerBundle
        {
            get
            {
                return 200;
            }
        }

        /// <summary>
        /// Exports a collection of batches to a binary file that can be downloaded by the user
        /// and sent to their financial institution. The returned BinaryFile should not have been
        /// saved to the database yet.
        /// </summary>
        /// <param name="fileFormat">The <see cref="ImageCashLetterFileFormat" /> that is being used to export this data.</param>
        /// <param name="batches">The list of batches whose data needs to be exported.</param>
        /// <param name="errorMessages">On return will contain a list of error messages if not empty.</param>
        /// <returns>
        /// A <see cref="Stream" /> of data that should be downloaded to the user in a file.
        /// </returns>
        public override Stream ExportBatches( ImageCashLetterFileFormat fileFormat, List<FinancialBatch> batches, out List<string> errorMessages )
        {
            var records = new List<X937.Record>();

            errorMessages = new List<string>();

            //
            // Get all the transactions that will be exported from these batches.
            //
            var transactions = batches.SelectMany( b => b.Transactions ).ToList();

            //
            // Perform error checking to ensure that all the transactions in these batches
            // are of the proper currency type.
            //
            int currencyTypeCheckId = Rock.Web.Cache.DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ).Id;
            if ( transactions.Any( t => t.FinancialPaymentDetail.CurrencyTypeValueId != currencyTypeCheckId ) )
            {
                errorMessages.Add( "One or more transactions is not of type 'Check'." );
                return null;
            }

            //
            // Generate all the X9.37 records for this set of transactions.
            //
            records.Add( GetFileHeaderRecord( fileFormat ) );
            records.Add( GetCashLetterHeaderRecord( fileFormat ) );
            records.AddRange( GetBundleRecords( fileFormat, transactions ) );
            records.Add( GetCashLetterControlRecord( fileFormat, records ) );
            records.Add( GetFileControlRecord( fileFormat, records ) );

            //
            // Encode all the records into a memory stream so that it can be saved to a file
            // by the caller.
            //
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

        #region File Records

        /// <summary>
        /// Gets the file header record (type 01).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <returns>A FileHeader record.</returns>
        protected virtual X937.Records.FileHeader GetFileHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            var destinationRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            var originRoutingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );

            var header = new X937.Records.FileHeader
            {
                StandardLevel = 3,
                FileTypeIndicator = GetAttributeValue( fileFormat, "TestMode" ).AsBoolean( true ) ? "T" : "P",
                ImmediateDestinationRoutingNumber = destinationRoutingNumber,
                ImmediateOriginRoutingNumber = originRoutingNumber,
                FileCreationDateTime = DateTime.Now,
                ResendIndicator = "N",
                ImmediateDestinationName = GetAttributeValue( fileFormat, "DestinationName" ),
                ImmediateOriginName = GetAttributeValue( fileFormat, "OriginName" ),
                FileIdModifier = "1", // TODO: Need some way to track this and reset each day.
                CountryCode = "US", /* Should be safe, X9.37 is only used in the US as far as I know. */
                UserField = string.Empty
            };

            return header;
        }

        /// <summary>
        /// Gets the file control record (type 99).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="records">The existing records in the file.</param>
        /// <returns>A FileControl record.</returns>
        protected virtual X937.Records.FileControl GetFileControlRecord( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var cashHeaderRecords = records.Where( r => r.RecordType == 10 );
            var detailRecords = records.Where( r => r.RecordType == 25 ).Cast<dynamic>();
            var itemRecords = records.Where( r => r.RecordType == 25 || r.RecordType == 61 );

            var control = new X937.Records.FileControl
            {
                CashLetterCount = cashHeaderRecords.Count(),
                TotalRecordCount = records.Count + 1, /* Plus one to include self */
                TotalItemCount = itemRecords.Count(),
                TotalAmount = detailRecords.Sum( c => ( decimal ) c.ItemAmount ),
                ImmediateOriginContactName = GetAttributeValue( fileFormat, "ContactName" ),
                ImmediateOriginContactPhoneNumber = GetAttributeValue( fileFormat, "ContactPhone" )
            };

            return control;
        }

        #endregion

        #region Cash Letter Records

        /// <summary>
        /// Gets the cash letter header record (type 10).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <returns>A CashLetterHeader record.</returns>
        protected virtual X937.Records.CashLetterHeader GetCashLetterHeaderRecord( ImageCashLetterFileFormat fileFormat )
        {
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            var contactName = GetAttributeValue( fileFormat, "ContactName" );
            var contactPhone = GetAttributeValue( fileFormat, "ContactPhone" );

            var header = new X937.Records.CashLetterHeader
            {
                CollectionTypeIndicator = 1,
                DestinationRoutingNumber = routingNumber,
                ClientInstitutionRoutingNumber = routingNumber,
                BusinessDate = DateTime.Now, // TODO: We might need to ask the user this and pass it in.
                CreationDateTime = DateTime.Now,
                RecordTypeIndicator = "I",
                DocumentationTypeIndicator = "G",
                OriginatorContactName = contactName,
                OriginatorContactPhoneNumber = contactPhone
            };

            return header;
        }

        /// <summary>
        /// Gets the cash letter control record (type 90).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="records">Existing records in the cash letter.</param>
        /// <returns>A CashLetterControl record.</returns>
        protected virtual X937.Records.CashLetterControl GetCashLetterControlRecord( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var bundleHeaderRecords = records.Where( r => r.RecordType == 20 );
            var checkDetailRecords = records.Where( r => r.RecordType == 25 ).Cast<dynamic>();
            var itemRecords = records.Where( r => r.RecordType == 25 || r.RecordType == 61 );
            var imageDetailRecords = records.Where( r => r.RecordType == 52 );
            var organizationName = Rock.Web.Cache.GlobalAttributesCache.Value( "OrganizationName" );

            var control = new X937.Records.CashLetterControl
            {
                BundleCount = bundleHeaderRecords.Count(),
                ItemCount = itemRecords.Count(),
                TotalAmount = checkDetailRecords.Sum( c => ( decimal ) c.ItemAmount ),
                ImageCount = imageDetailRecords.Count(),
                ECEInstitutionName = organizationName
            };

            return control;
        }

        #endregion

        #region Bundle Records

        /// <summary>
        /// Gets all the bundle records in required for the transactions specified.
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transactions">The transactions to be exported.</param>
        /// <returns>A collection of records that identify all the exported transactions.</returns>
        protected virtual List<X937.Record> GetBundleRecords( ImageCashLetterFileFormat fileFormat, List<FinancialTransaction> transactions )
        {
            var records = new List<X937.Record>();

            for ( int bundleIndex = 0; ( bundleIndex * MaxItemsPerBundle ) < transactions.Count(); bundleIndex++ )
            {
                var bundleRecords = new List<X937.Record>();
                var bundleTransactions = transactions.Skip( bundleIndex * MaxItemsPerBundle )
                    .Take( MaxItemsPerBundle )
                    .ToList();

                //
                // Add the bundle header for this set of transactions.
                //
                bundleRecords.Add( GetBundleHeader( fileFormat, bundleIndex ) );

                //
                // Allow subclasses to provide credit detail records (type 61) if they want.
                //
                bundleRecords.AddRange( GetCreditDetailRecords( fileFormat, bundleIndex, bundleTransactions ) );

                //
                // Add records for each transaction in the bundle.
                //
                foreach ( var transaction in bundleTransactions )
                {
                    bundleRecords.AddRange( GetItemRecords( fileFormat, transaction ) );
                }
                
                //
                // Add the bundle control record.
                //
                bundleRecords.Add( GetBundleControl( fileFormat, bundleRecords ) );

                records.AddRange( bundleRecords );
            }

            return records;
        }

        /// <summary>
        /// Gets the bundle header record (type 20).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="bundleIndex">Number of existing bundle records in the cash letter.</param>
        /// <returns>A BundleHeader record.</returns>
        protected virtual X937.Records.BundleHeader GetBundleHeader( ImageCashLetterFileFormat fileFormat, int bundleIndex )
        {
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );

            var header = new X937.Records.BundleHeader
            {
                CollectionTypeIndicator = 1,
                DestinationRoutingNumber = routingNumber,
                ClientInstitutionRoutingNumber = routingNumber,
                BusinessDate = DateTime.Now,
                CreationDate = DateTime.Now,
                ID = string.Empty,
                SequenceNumber = ( bundleIndex + 1 ).ToString(),
                CycleNumber = string.Empty,
                ReturnLocationRoutingNumber = routingNumber
            };

            return header;
        }

        /// <summary>
        /// Gets the credit detail deposit record (type 61).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="bundleIndex">Number of existing bundle records in the cash letter.</param>
        /// <param name="transactions">The transactions associated with this deposit.</param>
        /// <returns>A collection of records.</returns>
        protected virtual List<X937.Record> GetCreditDetailRecords( ImageCashLetterFileFormat fileFormat, int bundleIndex, List<FinancialTransaction> transactions )
        {
            return new List<X937.Record>();
        }

        /// <summary>
        /// Gets the bundle control record (type 70).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="records">The existing records in the bundle.</param>
        /// <returns>A BundleControl record.</returns>
        protected virtual X937.Records.BundleControl GetBundleControl( ImageCashLetterFileFormat fileFormat, List<X937.Record> records )
        {
            var itemRecords = records.Where( r => r.RecordType == 25 || r.RecordType == 61 );
            var checkDetailRecords = records.Where( r => r.RecordType == 25 ).Cast<dynamic>();
            var imageDetailRecords = records.Where( r => r.RecordType == 52 );

            var control = new X937.Records.BundleControl
            {
                ItemCount = itemRecords.Count(),
                TotalAmount = checkDetailRecords.Sum( r => ( decimal ) r.ItemAmount ),
                MICRValidTotalAmount = checkDetailRecords.Sum( r => ( decimal ) r.ItemAmount ),
                ImageCount = imageDetailRecords.Count()
            };

            return control;
        }

        #endregion

        #region Item Records

        /// <summary>
        /// Gets the records that identify a single check being deposited.
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transaction">The transaction to be deposited.</param>
        /// <returns>A collection of records.</returns>
        protected virtual List<X937.Record> GetItemRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction )
        {
            var records = new List<X937.Record>();

            records.AddRange( GetItemDetailRecords( fileFormat, transaction ) );
            
            records.AddRange( GetImageRecords( fileFormat, transaction, transaction.Images.Take( 1 ).First(), true ) );
            records.AddRange( GetImageRecords( fileFormat, transaction, transaction.Images.Skip( 1 ).Take( 1 ).First(), true ) );

            return records;
        }

        /// <summary>
        /// Gets the item detail records (type 25, 26, etc.)
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transaction">The transaction being deposited.</param>
        /// <returns>A collection of records.</returns>
        protected virtual List<X937.Record> GetItemDetailRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction )
        {
            
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );

            //
            // Parse the MICR data from the transaction.
            //
            var micr = GetMicrInst(transaction.CheckMicrEncrypted);

            //var transactionRoutingNumber = micr.GetField( 5 );
            var transactionRoutingNumber = micr.GetRoutingNumber();

            //
            // Get the Check Detail record (type 25).
            //
            var detail = new X937.Records.CheckDetail
            {
                PayorBankRoutingNumber = transactionRoutingNumber.Substring( 0, 8 ),
                PayorBankRoutingNumberCheckDigit = transactionRoutingNumber.Substring( 8, 1 ),
                //OnUs = micr.GetField( 3 ) + "/" + micr.GetField( 2 ),
                OnUs = string.Format("{0}/{1}", micr.GetAccountNumber(), micr.GetCheckNumber()),
                //ExternalProcessingCode = micr.GetField( 6 ),
                ExternalProcessingCode = micr.GetExternalProcessingCode(),
                //AuxiliaryOnUs = micr.GetField( 7 ),
                AuxiliaryOnUs = micr.GetAuxOnUs(),
                ItemAmount = transaction.TotalAmount,
                ClientInstitutionItemSequenceNumber = accountNumber,
                DocumentationTypeIndicator = "G",
                BankOfFirstDepositIndicator = "Y",
                CheckDetailRecordAddendumCount = 1
            };

            //
            // Get the Addendum A record (type 26).
            //
            var detailA = new X937.Records.CheckDetailAddendumA
            {
                RecordNumber = 1,
                BankOfFirstDepositRoutingNumber = routingNumber,
                BankOfFirstDepositBusinessDate = DateTime.Now,
                TruncationIndicator = "N",
                BankOfFirstDepositConversionIndicator = "2",
                BankOfFirstDepositCorrectionIndicator = "0"
            };

            return new List<X937.Record> { detail, detailA };
        }

        /// <summary>
        /// Gets the image record for a specific transaction image (type 50 and 52).
        /// </summary>
        /// <param name="fileFormat">The file format that contains the configuration to use.</param>
        /// <param name="transaction">The transaction being deposited.</param>
        /// <param name="image">The check image scanned by the scanning application.</param>
        /// <param name="isFront">if set to <c>true</c> [is front].</param>
        /// <returns>A collection of records.</returns>
        protected virtual List<X937.Record> GetImageRecords( ImageCashLetterFileFormat fileFormat, FinancialTransaction transaction, FinancialTransactionImage image, bool isFront )
        {
            var routingNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "RoutingNumber" ) );
            var accountNumber = Rock.Security.Encryption.DecryptString( GetAttributeValue( fileFormat, "AccountNumber" ) );

            //
            // Get the Image View Detail record (type 50).
            //
            var detail = new X937.Records.ImageViewDetail
            {
                ImageIndicator = 1,
                ImageCreatorRoutingNumber = routingNumber,
                ImageCreatorDate = image.CreatedDateTime ?? DateTime.Now,
                ImageViewFormatIndicator = 0,
                CompressionAlgorithmIdentifier = 0,
                SideIndicator = isFront ? 0 : 1,
                ViewDescriptor = 0,
                DigitalSignatureIndicator = 0
            };

            //
            // Get the Image View Data record (type 51).
            //
            var data = new X937.Records.ImageViewData
            {
                InstitutionRoutingNumber = routingNumber,
                BundleBusinessDate = DateTime.Now,
                ClientInstitutionItemSequenceNumber = accountNumber,
                ClippingOrigin = 0,
                ImageData = image.BinaryFile.ContentStream.ReadBytesToEnd()
            };

            return new List<X937.Record> { detail, data };
        }

        #endregion

        #region Private Methods

        private Micr GetMicrInst(string encryptedMicrContent)
        {
            string decryptedMicrContent = Rock.Security.Encryption.DecryptString(encryptedMicrContent);

            if (decryptedMicrContent == null)
                throw new ArgumentException("MICR data is empty.");

            var micr = new Micr(decryptedMicrContent);
            
            return micr;
        }

        #endregion
    }
}
