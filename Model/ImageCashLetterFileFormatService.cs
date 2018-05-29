using Rock.Data;

namespace com.shepherdchurch.ImageCashLetter.Model
{
    /// <summary>
    /// Queries the database for instances of the ImageCashLetterFileFormat.
    /// </summary>
    public class ImageCashLetterFileFormatService : Service<ImageCashLetterFileFormat>
    {
        public ImageCashLetterFileFormatService( DbContext dbContext ) : base( dbContext )
        {
        }
    }
}
