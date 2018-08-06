using System;

namespace com.shepherdchurch.ImageCashLetter
{
    /// <summary>
    /// A helper class for working with MICR data from Rock.
    /// </summary>
    public class Micr
    {
        /// <summary>
        /// Enum for readability to fetch out the data we are after
        /// </summary>
        protected enum FIELD
        {
            /// <summary>
            /// Field 1
            /// </summary>
            CHECK_AMOUNT,

            /// <summary>
            /// Field 2
            /// </summary>
            CHECK_NUMBER,

            /// <summary>
            /// Field 3
            /// </summary>
            ACCOUNT_NUMBER,

            /// <summary>
            /// Field 5
            /// </summary>
            ROUTING_NUMBER,

            /// <summary>
            /// Field 6
            /// </summary>
            EXTERNAL_PROCESSING_CODE,

            /// <summary>
            /// Field 7
            /// </summary>
            AUX_ON_US
        }

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
            else if ( !content.Contains( "d" ) || !content.Contains( "c" ) )
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
                    content = content.PadRight( length );
                }

                _content = content;
            }
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Returns true if the MICR content is valid. This is not a perfect check, but
        /// it should give a strong indication on the MICR string being valid or not.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <returns>
        ///   <c>true</c> if the specified content is valid; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsValid( string content )
        {
            if ( content.Contains( "!" ) )
            {
                return false;
            }

            try
            {
                var micr = new Micr( content );

                if ( micr.GetRoutingNumber().Length != 9 )
                {
                    return false;
                }

                if ( micr.GetAccountNumber().Length < 6 )
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the characters in the specified range from the MICR.
        /// </summary>
        /// <param name="start">The starting position in the MICR, from the right.</param>
        /// <param name="end">The ending position in the MICR (inclusive), from the right.</param>
        /// <returns>A string containing the characters found in the specified range.</returns>
        protected string GetCharacterFields( int start, int end )
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
        /// Get a specific MICR field from the MICR data.
        /// </summary>
        /// <param name="FieldType">The MICR field to be retrieved.</param>
        /// <returns>String containing the component value from the MICR line.</returns>
        protected string GetField( FIELD FieldType )
        {
            var f = string.Empty;

            switch ( FieldType )
            {
                // Account number
                case FIELD.ACCOUNT_NUMBER:
                    {
                        f = GetCharacterFields( 13, 32 );

                        if ( f.IndexOf( 'c' ) != f.LastIndexOf( 'c' ) )
                        {
                            return f.Substring( f.IndexOf( 'c' ) + 1, f.LastIndexOf( 'c' ) - f.IndexOf( 'c' ) - 1 ).Trim();
                        }
                        else
                        {
                            return f.Substring( 0, f.IndexOf( 'c' ) ).Trim();
                        }
                    }

                // AUX OnUs
                case FIELD.AUX_ON_US:
                    {
                        return GetCharacterFields( 45, _content.Length ).Replace( "c", "" ).Trim();
                    }

                // Check Amount
                case FIELD.CHECK_AMOUNT:
                    {
                        return GetCharacterFields( 2, 11 ).Trim();
                    }

                // Check Number
                case FIELD.CHECK_NUMBER:
                    {
                        f = GetCharacterFields( 13, 32 );

                        if ( f.IndexOf( 'c' ) != f.LastIndexOf( 'c' ) )
                        {
                            return f.Substring( 0, f.IndexOf( 'c' ) - 1 ).Trim() + f.Substring( f.LastIndexOf( 'c' ) + 1 ).Trim();
                        }
                        else
                        {
                            return f.Substring( f.IndexOf( 'c' ) + 1 ).Trim();
                        }
                    }

                // External Processing code
                case FIELD.EXTERNAL_PROCESSING_CODE:
                    {
                        return GetCharacterFields( 44, 44 ).Trim();
                    }

                // Routing Number
                case FIELD.ROUTING_NUMBER:
                    {
                        return GetCharacterFields( 34, 42 ).Trim();
                    }
            }

            return string.Empty;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the amount of the check as specified on the MICR line.
        /// </summary>
        /// <returns>A string containing the amount characters.</returns>
        public string GetCheckAmount()
        {
            return GetField( FIELD.CHECK_AMOUNT );
        }

        /// <summary>
        /// Gets the routing number from the MICR line.
        /// </summary>
        /// <returns>A string containing the routing number characters.</returns>
        public string GetRoutingNumber()
        {
            return GetField( FIELD.ROUTING_NUMBER );
        }

        /// <summary>
        /// Gets the check number from the MICR line.
        /// </summary>
        /// <returns>A string containing, what is normally, the check number characters.</returns>
        public string GetCheckNumber()
        {
            return GetField( FIELD.CHECK_NUMBER );
        }

        /// <summary>
        /// Gets the account number from the MICR line.
        /// </summary>
        /// <returns>A string containing the account number this check will draw on.</returns>
        public string GetAccountNumber()
        {
            return GetField( FIELD.ACCOUNT_NUMBER );
        }

        /// <summary>
        /// Gets the EPC from the MICR line.
        /// </summary>
        /// <returns></returns>
        public string GetExternalProcessingCode()
        {
            return GetField( FIELD.EXTERNAL_PROCESSING_CODE );
        }

        /// <summary>
        /// GetAuxOnUs
        /// </summary>
        /// <returns>string</returns>
        public string GetAuxOnUs()
        {
            return GetField( FIELD.AUX_ON_US );
        }

        #endregion
    }
}
