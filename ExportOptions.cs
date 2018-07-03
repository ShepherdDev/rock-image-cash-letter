using System;
using System.Collections.Generic;

using Rock;
using Rock.Model;

using com.shepherdchurch.ImageCashLetter.Model;

namespace com.shepherdchurch.ImageCashLetter
{
    /// <summary>
    /// Provides additional options to the export process.
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Gets or sets the file format.
        /// </summary>
        /// <value>
        /// The file format.
        /// </value>
        public ImageCashLetterFileFormat FileFormat { get; set; }

        /// <summary>
        /// Gets or sets the batches to be exported.
        /// </summary>
        /// <value>
        /// The batches to be exported.
        /// </value>
        public IEnumerable<FinancialBatch> Batches { get; set; }

        /// <summary>
        /// Gets or sets the date and time the export process was run.
        /// </summary>
        /// <value>
        /// The date and time the export process was run.
        /// </value>
        public DateTime ExportDateTime { get; set; }

        /// <summary>
        /// Gets or sets the business date time.
        /// </summary>
        /// <value>
        /// The business date time.
        /// </value>
        public DateTime BusinessDateTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportOptions"/> class.
        /// </summary>
        public ExportOptions( ImageCashLetterFileFormat fileFormat, IEnumerable<FinancialBatch> batches )
        {
            FileFormat = fileFormat;
            Batches = batches;

            ExportDateTime = RockDateTime.Now;
            BusinessDateTime = RockDateTime.Now;
        }
    }
}
