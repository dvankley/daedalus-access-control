using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using GlobalHelpers;
using System.Reflection;
using System.Diagnostics;

namespace DaedalusTestApp
{
    public class TestHelpers
    {

    }

    [TestFixture]
    public class DaedalusProtocolTests_VerifyDecryptedPacketFromBytes
    {
        [Test]
        public void SOHFailure ()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            byte[] testPacket = { 0x20, 0x21, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, false);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.InvalidPacketStructure);

        }

        [Test]
        public void LengthFailure()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            byte[] testPacket = { 0x05, 0x04, 0x02, 0x06, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, false);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.InvalidPacketStructure);

        }

        [Test]
        public void CRCFailure()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            byte[] testPacket = { 0x05, 0x04, 0x02, 0x07, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x03, 0xFF, 0xFF, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, false);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.IncorrectCRC);
        }

        [Test]
        public void CRCSuccess()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            byte[] testPacket = { 0x05, 0x04, 0x02, 0x07, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x03, 0x80, 0x48, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
        }
    }
    [TestFixture]
    public class DaedalusProtocolTests_ConstructDecryptedPacketFromBytes
    {
        [Test]
        public void PayloadParseCommandNotImplemented()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;
            DecryptedDaedalusPacket packet;

            //                   <STX><packetLength><packetIndex>                   <IDHash>                   <cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4     5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    34    35    36    37    38    39    40    41
            byte[] testPacket = { 0x02, 0x24, 0x00, 0x01, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x77, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x5f, 0x69, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            packet = new DecryptedDaedalusPacket(testPacket, 0, testPacket.Length, out returnCode);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.CommandNotImplemented);
        }

        [Test]
        public void PayloadParseSuccess()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;
            DecryptedDaedalusPacket packet;

            //                   <STX><packetLength><packetIndex>                   <IDHash>                   <cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4     5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    34    35    36    37    38    39    40    41
            byte[] testPacket = { 0x02, 0x24, 0x00, 0x01, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x70, 0xd7, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            packet = new DecryptedDaedalusPacket(testPacket, 0, testPacket.Length, out returnCode);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsNotNull(packet);
            Assert.IsTrue(packet.payload.Count == 2);
            Assert.IsTrue(packet.payload.ContainsKey("hash"));
            Assert.IsTrue(packet.payload.ContainsKey("actionTaken"));
        }
    }

    [TestFixture]
    public class DaedalusProtocolTests_EncryptAndDecryptPacket
    {

    }
}
