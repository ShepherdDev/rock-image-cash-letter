using System;

namespace com.shepherdchurch.ImageCashLetter
{
    /// <summary>
    /// A helper class for working with MICR data from Rock.
    /// </summary>
    public class Micr
    {
        #region Fields

        /// <summary>
        /// The content of the MICR data after it has been justified.
        /// </summary>
        private string _content;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Micr"/> class.
        /// </summary>
        /// <param name="content">The content of the MICR data, expected to be in Ranger Driver format.</param>
        /// <exception cref="System.ArgumentException">Argument does not contain valid micr data - content</exception>
        public Micr( string content )
        {
            //
            // Verify the data is valid.
            //
            if ( content == null )
            {
                _content = string.Empty;
            }
            else if ( !content.Contains( "d" ) )
            {
                throw new ArgumentException( "Argument does not contain valid micr data.", "content" );
            }
            else
            {
                //
                // MICR data should be aligned to have the left-most Routing Number symbol
                // at position 43 (from the right-side of the string). So we need to adjust
                // the length of the string so that the first 'd' we find is followed by 42
                // characters.
                //
                int index = content.IndexOf( 'd' );
                int length = content.Length - ( content.Length - 43 - index );

                if ( content.Length > length )
                {
                    content = content.Substring( 0, length );
                }
                else if ( content.Length < length )
                {
                    content = content.PadRight( length - content.Length );
                }

                _content = content;
            }
        }

        #endregion

        /// <summary>
        /// Gets the characters in the specified range from the MICR.
        /// </summary>
        /// <param name="start">The starting position in the MICR, from the right.</param>
        /// <param name="end">The ending position in the MICR (inclusive), from the right.</param>
        /// <returns></returns>
        public string GetCharacterFields( int start, int end )
        {
            if ( start > _content.Length )
            {
                return string.Empty;
            }

            if ( end > _content.Length )
            {
                end = _content.Length;
            }

            return _content.Substring( _content.Length - end, end - start + 1 );
        }

        /// <summary>
        /// Gets the field as specified by the "common MICR field" designations.
        /// Assuming all banks follow this.
        /// TODO: This should probably be replaced by multiple methods each named
        /// for what they are retrieving.
        /// </summary>
        /// <param name="field">The field number to retrieve. See inline comments for what each field is.</param>
        /// <returns>A whitespace trimmed string that represents the contents of the field.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">field - Field must be a valid MICR field number</exception>
        public string GetField( int field )
        {
            switch ( field )
            {
                //
                // The amount of the check.
                //
                case 1:
                    return GetCharacterFields( 1, 12 ).Trim();
                
                //
                // Extra data between the Account number and the Amount. Usually
                // this is the check number.
                //
                case 2:
                    var f2 = GetCharacterFields( 13, 32 );

                    return f2.Substring( f2.IndexOf( 'c' ) + 1 ).Trim();
                
                //
                // Account number.
                //
                case 3:
                    var f3 = GetCharacterFields( 13, 32 );

                    return f3.Substring( 0, f3.IndexOf( 'c' ) ).Trim();
                
                //
                // Routing number.
                //
                case 5:
                    return GetCharacterFields( 34, 42 ).Trim();
                
                //
                // External Processing Code.
                //
                case 6:
                    return GetCharacterFields( 44, 44 ).Trim();

                //
                // Auxiliary On-Us.
                //
                case 7:
                    return GetCharacterFields( 45, 1000 ).Trim();

                default:
                    throw new ArgumentOutOfRangeException( "field", "Field must be a valid MICR field number" );
            }
        }
    }
}
