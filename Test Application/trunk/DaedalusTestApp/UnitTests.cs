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
        public const string goodAESKey = "I<3Ponies";
        public const string badAESKey = "sendinthedancinglobsters";
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

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00, 0x19, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xb7, 0xdf, 0x04 };

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

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00,  0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x3A, 0x1f, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            packet = new DecryptedDaedalusPacket(testPacket, 0, testPacket.Length, out returnCode);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsNotNull(packet);
            Assert.IsTrue(packet.getTotalPacketLength() == testPacket.Length);
            Assert.IsTrue(packet.payload.Count == 2);
            Assert.IsTrue(packet.payload.ContainsKey("hash"));
            Assert.IsTrue(packet.payload.ContainsKey("actionTaken"));
        }
    }

    [TestFixture]
    public class DaedalusProtocolTests_ToByteBufferAndBack
    {
        [Test]
        public void DecryptedPacketToByteBufferCheck()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;
            DecryptedDaedalusPacket packet;

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00, 0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x3A, 0x1f, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            packet = new DecryptedDaedalusPacket(testPacket, 0, testPacket.Length, out returnCode);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsNotNull(packet);
            Assert.IsTrue(packet.getTotalPacketLength() == testPacket.Length);
            Assert.IsTrue(packet.payload.Count == 2);
            Assert.IsTrue(packet.payload.ContainsKey("hash"));
            Assert.IsTrue(packet.payload.ContainsKey("actionTaken"));

            byte[] bufferBackOut = packet.toByteBuffer();
            Assert.IsTrue(bufferBackOut.SequenceEqual(testPacket));
        }

        [Test]
        public void EncryptedPacketToByteBufferCheck()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            //                   <STX><packetLength><encryptedPayload ->>                                                                                                                                                                                <ETX>   <CRC>    <EOT> 
            //                     0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x01, 0x61, 0x00, 0x97, 0xBD, 0x95, 0xCF, 0xEB, 0x0A, 0x27, 0x0D, 0xE8, 0x09, 0x4F, 0xA4, 0x7F, 
                                    0x67, 0x0F, 0xC4, 0x08, 0xDA, 0x1A, 0x28, 0x6D, 0x0B, 0xDB, 0x32, 0x91, 0xA1, 0x9B, 0x37, 0xD3, 
                                    0xD7, 0xCA, 0x31, 0x00, 0xCA, 0xA0, 0x59, 0xC9, 0xC5, 0xB8, 0xDD, 0x59, 0xFC, 0xEB, 0x90, 0xCD, 
                                    0xA4, 0x4A, 0x71, 0x4B, 0xB9, 0xB0, 0x82, 0x64, 0x60, 0x0D, 0x5D, 0x28, 0x8F, 0x9F, 0xB1, 0x9F, 
                                    0x65, 0xEE, 0x96, 0xA6, 0xAE, 0xC3, 0xB5, 0x2B, 0x7F, 0x66, 0x40, 0x78, 0x4D, 0xAA, 0x25, 0xBA, 
                                    //                                                                                                               <ETX>    <CRC>    <EOT>
                                    0x78, 0xD5, 0x64, 0x05, 0xCB, 0x61, 0x51, 0x04, 0x02, 0xFC, 0x74, 0x96, 0xE8, 0x39, 0x3F, 0x53, 0xBA, 0x7F, 0x9E, 0x03, 0xd2, 0x22, 0x04 };
            isValid = EncryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsTrue(packetStart == 0);
            Assert.IsTrue(packetLength == testPacket.Length);
            EncryptedDaedalusPacket encryptedPacket2 = new EncryptedDaedalusPacket(testPacket, packetStart, packetLength, out returnCode, TestHelpers.goodAESKey);
            Assert.IsNotNull(encryptedPacket2);
            byte[] encryptedByteBuffer = encryptedPacket2.toByteBuffer();
            Assert.IsTrue(testPacket.SequenceEqual(encryptedByteBuffer));
        }
    }

    [TestFixture]
    public class DaedalusProtocolTests_EncryptAndDecryptPacket
    {
        [Test]
        public void BuildEncryptedPacketFromDecryptedPacketSuccess()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;
            DecryptedDaedalusPacket packet;

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00, 0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x3A, 0x1f, 0x04 };

            isValid = DecryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsTrue(packetStart == 0);
            Assert.IsTrue(packetLength == testPacket.Length);
            packet = new DecryptedDaedalusPacket(testPacket, 0, testPacket.Length, out returnCode);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsNotNull(packet);
            Assert.IsTrue(packet.payload.Count == 2);
            Assert.IsTrue(packet.payload.ContainsKey("hash"));
            Assert.IsTrue(packet.payload.ContainsKey("actionTaken"));

            // At this point decrypted packet is built and verified

            // Build encrypted packet from the decrypted packet
            EncryptedDaedalusPacket encryptedPacket1 = new EncryptedDaedalusPacket(packet, TestHelpers.goodAESKey);

            byte[] encryptedBuffer1 = encryptedPacket1.toByteBuffer();
            Assert.IsTrue(encryptedBuffer1.Length > testPacket.Length);

            Assert.IsNotNull(encryptedPacket1);          
            byte[] encryptedPayload = encryptedPacket1.encryptedPayload;
            Assert.IsNotNull(encryptedPayload);

            byte[] decryptedPayload1 = encryptedPacket1.decryptedPayload;
            Assert.IsNotNull(decryptedPayload1);
            Assert.IsTrue(testPacket.SequenceEqual(decryptedPayload1));


            // Build encrypted packet from decrypted packet's payload
            List<byte> encryptedPacketRaw = new List<byte>();
            encryptedPacketRaw.AddRange(new byte[] { GlobalConstants.SOH, 0x61, 0x00});
            encryptedPacketRaw.AddRange(encryptedPayload);
            encryptedPacketRaw.AddRange(new byte[] { GlobalConstants.ETX, 0x03, 0x46, GlobalConstants.EOT });
            byte[] encryptedPacketBuffer = encryptedPacketRaw.ToArray();

            // Actually we can't validate this packet because the CRC will change with each new encryption

            //isValid = EncryptedDaedalusPacket.IsValidPacket(encryptedPacketBuffer, out returnCode, out packetStart, out packetLength);
            //Assert.AreEqual(isValid, true);
            //Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            //Assert.IsTrue(packetStart == 0);
            //Assert.IsTrue(packetLength == encryptedPacketBuffer.Length);
            EncryptedDaedalusPacket encryptedPacket2 = new EncryptedDaedalusPacket(encryptedPacketBuffer, 0, encryptedPacketBuffer.Length, out returnCode, TestHelpers.goodAESKey);
            Assert.IsNotNull(encryptedPacket2);
            byte[] decryptedPayload2 = encryptedPacket2.decryptedPayload;
            Assert.IsNotNull(decryptedPayload2);
            Assert.IsTrue(testPacket.SequenceEqual(decryptedPayload2));
        }

        [Test]
        public void BuildEncryptedPacketFromEncryptedBufferSuccess()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] referenceDecyptedPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00, 0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x3A, 0x1f, 0x04 };


            //                   <STX><packetLength><encryptedPayload ->>                                                                                                                                                                                <ETX>   <CRC>    <EOT> 
            //                     0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x01, 0x61, 0x00, 0x97, 0xBD, 0x95, 0xCF, 0xEB, 0x0A, 0x27, 0x0D, 0xE8, 0x09, 0x4F, 0xA4, 0x7F, 
                                    0x67, 0x0F, 0xC4, 0x08, 0xDA, 0x1A, 0x28, 0x6D, 0x0B, 0xDB, 0x32, 0x91, 0xA1, 0x9B, 0x37, 0xD3, 
                                    0xD7, 0xCA, 0x31, 0x00, 0xCA, 0xA0, 0x59, 0xC9, 0xC5, 0xB8, 0xDD, 0x59, 0xFC, 0xEB, 0x90, 0xCD, 
                                    0xA4, 0x4A, 0x71, 0x4B, 0xB9, 0xB0, 0x82, 0x64, 0x60, 0x0D, 0x5D, 0x28, 0x8F, 0x9F, 0xB1, 0x9F, 
                                    0x65, 0xEE, 0x96, 0xA6, 0xAE, 0xC3, 0xB5, 0x2B, 0x7F, 0x66, 0x40, 0x78, 0x4D, 0xAA, 0x25, 0xBA, 
                                    //                                                                                                               <ETX>    <CRC>    <EOT>
                                    0x78, 0xD5, 0x64, 0x05, 0xCB, 0x61, 0x51, 0x04, 0x02, 0xFC, 0x74, 0x96, 0xE8, 0x39, 0x3F, 0x53, 0xBA, 0x7F, 0x9E, 0x03, 0xd2, 0x22, 0x04 };
            isValid = EncryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsTrue(packetStart == 0);
            Assert.IsTrue(packetLength == testPacket.Length);
            EncryptedDaedalusPacket encryptedPacket2 = new EncryptedDaedalusPacket(testPacket, packetStart, packetLength, out returnCode, TestHelpers.goodAESKey);
            Assert.IsNotNull(encryptedPacket2);
            byte[] decryptedPayload2 = encryptedPacket2.decryptedPayload;
            Assert.IsNotNull(decryptedPayload2);
            Assert.IsTrue(referenceDecyptedPacket.SequenceEqual(decryptedPayload2));
        }

        [Test]
        public void DecryptPacketFromBytesAESKeyFailure()
        {
            bool isValid;
            DaedalusGlobal.ReturnCodes returnCode;
            int packetStart;
            int packetLength;

            //                   <STX><packetLength><packetIndex><cmd><cmdVer> <cmdLen>  <cmdPay><hash>                                                                                                           <act> <ETX>   <CRC>    <EOT> 
            //                      0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] referenceDecyptedPacket = { 0x02, 0x1C, 0x00, 0x01, 0x00, 0x20, 0x01, 0x15, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x3A, 0x1f, 0x04 };


            //                   <STX><packetLength><encryptedPayload ->>                                                                                                                                                                                <ETX>   <CRC>    <EOT> 
            //                     0     1     2     3     4      5     6     7     8     9     10   11    12    13    14    15    16    17    18    19    20    21    22    23    24    25    26    27    28    29    30    31    32    33    
            byte[] testPacket = { 0x01, 0x61, 0x00, 0x97, 0xBD, 0x95, 0xCF, 0xEB, 0x0A, 0x27, 0x0D, 0xE8, 0x09, 0x4F, 0xA4, 0x7F, 
                                    0x67, 0x0F, 0xC4, 0x08, 0xDA, 0x1A, 0x28, 0x6D, 0x0B, 0xDB, 0x32, 0x91, 0xA1, 0x9B, 0x37, 0xD3, 
                                    0xD7, 0xCA, 0x31, 0x00, 0xCA, 0xA0, 0x59, 0xC9, 0xC5, 0xB8, 0xDD, 0x59, 0xFC, 0xEB, 0x90, 0xCD, 
                                    0xA4, 0x4A, 0x71, 0x4B, 0xB9, 0xB0, 0x82, 0x64, 0x60, 0x0D, 0x5D, 0x28, 0x8F, 0x9F, 0xB1, 0x9F, 
                                    0x65, 0xEE, 0x96, 0xA6, 0xAE, 0xC3, 0xB5, 0x2B, 0x7F, 0x66, 0x40, 0x78, 0x4D, 0xAA, 0x25, 0xBA, 
                                    //                                                                                                               <ETX>    <CRC>    <EOT>
                                    0x78, 0xD5, 0x64, 0x05, 0xCB, 0x61, 0x51, 0x04, 0x02, 0xFC, 0x74, 0x96, 0xE8, 0x39, 0x3F, 0x53, 0xBA, 0x7F, 0x9E, 0x03, 0xd2, 0x22, 0x04 };
            isValid = EncryptedDaedalusPacket.IsValidPacket(testPacket, out returnCode, out packetStart, out packetLength);
            Assert.AreEqual(isValid, true);
            Assert.AreEqual(returnCode, DaedalusGlobal.ReturnCodes.Valid);
            Assert.IsTrue(packetStart == 0);
            Assert.IsTrue(packetLength == testPacket.Length);
            try
            {
                // This should crap out because of the wrong key
                EncryptedDaedalusPacket encryptedPacket2 = new EncryptedDaedalusPacket(testPacket, packetStart, packetLength, out returnCode, TestHelpers.badAESKey);
                Assert.IsNotNull(encryptedPacket2);
                byte[] decryptedPayload2;

                decryptedPayload2 = encryptedPacket2.decryptedPayload;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Assert.IsNotNull(ex);
            }
            //Assert.IsNotNull(decryptedPayload2);
            //Assert.IsFalse(referenceDecyptedPacket.SequenceEqual(decryptedPayload2));
        }
    }
}
