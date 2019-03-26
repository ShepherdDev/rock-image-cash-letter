﻿using Xunit;

namespace com.shepherdchurch.ImageCashLetter.Tests
{
    public class MicrTest
    {
        [Fact]
        public void ValidMicr1()
        {
            Micr micr = new Micr( "     d123456780d   123-456-7c  5431             " );

            Assert.Equal( "123456780", micr.GetRoutingNumber() );
            Assert.Equal( "123-456-7", micr.GetAccountNumber() );
            Assert.Equal( "5431", micr.GetCheckNumber() );
            Assert.Equal( string.Empty, micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( string.Empty, micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr2()
        {
            Micr micr = new Micr( "                         d123456780d   123-456-7c" );

            Assert.Equal( "123456780", micr.GetRoutingNumber() );
            Assert.Equal( "123-456-7", micr.GetAccountNumber() );
            Assert.Equal( string.Empty, micr.GetCheckNumber() );
            Assert.Equal( string.Empty, micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( string.Empty, micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr3()
        {
            Micr micr = new Micr( "d124444706d1021-823215406c                      " );

            Assert.Equal( "124444706", micr.GetRoutingNumber() );
            Assert.Equal( "1021-823215406", micr.GetAccountNumber() );
            Assert.Equal( string.Empty, micr.GetCheckNumber() );
            Assert.Equal( string.Empty, micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( string.Empty, micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr4()
        {
            Micr micr = new Micr( "d044446110d033305789859c  0302                   " );

            Assert.Equal( "044446110", micr.GetRoutingNumber() );
            Assert.Equal( "033305789859", micr.GetAccountNumber() );
            Assert.Equal( "0302", micr.GetCheckNumber() );
            Assert.Equal( string.Empty, micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( string.Empty, micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr5()
        {
            Micr micr = new Micr( "c543123c d123456789d 987654321c" );

            Assert.Equal( "123456789", micr.GetRoutingNumber() );
            Assert.Equal( "987654321", micr.GetAccountNumber() );
            Assert.Equal( string.Empty, micr.GetCheckNumber() );
            Assert.Equal( string.Empty, micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( "543123", micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr6()
        {
            Micr micr = new Micr( "c706001c d075901231d    456327c  0101   b0000039275b" );

            Assert.Equal( "075901231", micr.GetRoutingNumber() );
            Assert.Equal( "456327", micr.GetAccountNumber() );
            Assert.Equal( "0101", micr.GetCheckNumber() );
            Assert.Equal( "0000039275", micr.GetCheckAmount() );
            Assert.Equal( string.Empty, micr.GetExternalProcessingCode() );
            Assert.Equal( "706001", micr.GetAuxOnUs() );
        }

        [Fact]
        public void ValidMicr7()
        {
            Micr micr = new Micr( "d382179107d1528 c233445801c                      " );

            Assert.Equal( "382179107", micr.GetRoutingNumber() );
            Assert.Equal( "233445801", micr.GetAccountNumber() );
            Assert.Equal( "1528", micr.GetCheckNumber() );
        }

        [Fact]
        public void ValidMicr8()
        {
            // This came from a Wells Fargo test check that they say is valid.
            var micr = new Micr( "c2336c d092901104d               c12c                   " );

            Assert.Equal( "092901104", micr.GetRoutingNumber() );
            Assert.Equal( "12", micr.GetAccountNumber() );
            Assert.Equal( "2336", micr.GetAuxOnUs() );
        }
    }
}
