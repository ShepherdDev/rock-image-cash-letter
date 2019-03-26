using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Rock;

namespace com.shepherdchurch.ImageCashLetter.Tests
{
    public static class TestTools
    {
        /// <summary>
        /// Hashes the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns></returns>
        public static string Hash( Stream stream )
        {
            using ( var sha1 = new SHA1Managed() )
            {
                var hash = sha1.ComputeHash( stream );
                var sb = new StringBuilder( "0x" );

                foreach ( byte b in hash )
                {
                    sb.Append( b.ToString( "X2" ) );
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets a stream that contains a blank check image.
        /// </summary>
        /// <returns></returns>
        public static Stream NewCheckImageStream()
        {
            var bitmap = new System.Drawing.Bitmap( 1209, 550 );

            //
            // Ensure the DPI is correct.
            //
            bitmap.SetResolution( 200, 200 );

            var g = System.Drawing.Graphics.FromImage( bitmap );
            g.FillRectangle( System.Drawing.Brushes.White, new System.Drawing.Rectangle( 0, 0, 1200, 550 ) );
            g.Flush();

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
        /// Gets a new financial transaction.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="processedDateTime">The processed date time.</param>
        /// <returns></returns>
        public static Rock.Model.FinancialTransaction NewFinancialTransaction( int id, DateTime processedDateTime )
        {
            var transaction = new Rock.Model.FinancialTransaction
            {
                Id = id,
                ProcessedDateTime = processedDateTime,
                FinancialPaymentDetail = new Rock.Model.FinancialPaymentDetail
                {
                    CurrencyTypeValue = new Rock.Model.DefinedValue
                    {
                        Guid = Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK.AsGuid()
                    }
                }
            };

            var transactionImage = new Rock.Model.FinancialTransactionImage
            {
                CreatedDateTime = processedDateTime,
                BinaryFile = new Rock.Model.BinaryFile
                {
                    ContentStream = NewCheckImageStream()
                }
            };
            transaction.Images.Add( transactionImage );

            transactionImage = new Rock.Model.FinancialTransactionImage
            {
                CreatedDateTime = processedDateTime,
                BinaryFile = new Rock.Model.BinaryFile
                {
                    ContentStream = NewCheckImageStream()
                }
            };
            transaction.Images.Add( transactionImage );

            return transaction;
        }

        /// <summary>
        /// Generate a Financial Batch with 3 transactions of [$200], [$50, $75], and [$100] amounts.
        /// </summary>
        /// <returns></returns>
        public static Rock.Model.FinancialBatch GetFinancialBatchAlpha()
        {
            var batch = new Rock.Model.FinancialBatch();

            var transaction = TestTools.NewFinancialTransaction( 1, new DateTime( 2019, 1, 15, 13, 58, 8, 0 ) );
            transaction.AddTransactionDetail( 200 );
            batch.Transactions.Add( transaction );
            transaction.CheckMicrEncrypted = Rock.Security.Encryption.EncryptString( "     d123456780d   123-456-7c  5431             " );

            transaction = TestTools.NewFinancialTransaction( 2, new DateTime( 2019, 1, 15, 14, 0, 12, 0 ) );
            transaction.AddTransactionDetail( 50 );
            transaction.AddTransactionDetail( 75 );
            batch.Transactions.Add( transaction );
            transaction.CheckMicrEncrypted = Rock.Security.Encryption.EncryptString( "                         d123456780d   123-456-7c" );

            transaction = TestTools.NewFinancialTransaction( 3, new DateTime( 2019, 1, 15, 14, 1, 43, 0 ) );
            transaction.AddTransactionDetail( 100 );
            batch.Transactions.Add( transaction );
            transaction.CheckMicrEncrypted = Rock.Security.Encryption.EncryptString( "d124444706d1021-823215406c                      " );

            return batch;
        }

        #region Extension Methods

        /// <summary>
        /// Add a new mock TransactionDetail record to the Transaction with the given amount.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="amount">The amount.</param>
        public static void AddTransactionDetail( this Rock.Model.FinancialTransaction transaction, decimal amount )
        {
            var transactionDetail = new Rock.Model.FinancialTransactionDetail
            {
                Amount = amount
            };

            transaction.TransactionDetails.Add( transactionDetail );
        }

        /// <summary>
        /// Truncates the Date Time to minute accuracy. Seconds and Milliseconds will be zero.
        /// </summary>
        /// <param name="dateTime">The date time.</param>
        /// <returns></returns>
        public static DateTime TruncateToMinutes( this DateTime dateTime )
        {
            return dateTime.Date.AddHours( dateTime.Hour ).AddMinutes( dateTime.Minute );
        }

        #endregion

    }
}
