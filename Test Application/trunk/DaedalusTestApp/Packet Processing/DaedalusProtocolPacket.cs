using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
using System.Security.Cryptography;
using GlobalHelpers;
using System.IO;
using Encryptamajig;

namespace DaedalusTestApp
{
    public class DaedalusGlobal
    {
        public const int DaedalusPort = 25555;
        public enum ReturnCodes : byte { Valid = 0x00, InvalidPacketIndex = 0x20, InvalidIDHash = 0x21, 
            IncorrectCRC = 0x22, InvalidCommand = 0x23, InvalidPacketStructure = 0x24, CommandNotImplemented = 0x25, 
            PayloadFieldMissing = 0x26 };

        public class PacketElement
        {
            internal string ElementName {get; set; }
            internal int ElementSize { get; set; }
            /// <summary>
            /// -1 indicates a variable length
            /// </summary>
            internal int ElementStaticOffset { get; set; }
            /// <summary>
            /// -1 indicates a static offset. The offset is this element is payloadLength + this value.
            /// </summary>
            internal int ElementVariableOffset { get; set; }

            internal PacketElement(string name, int size, int staticOffset, int variableOffset)
            {
                ElementName = name;
                ElementSize = size;
                ElementStaticOffset = staticOffset;
                ElementVariableOffset = variableOffset;
            }
        }

        public class PayloadElement
        {
            internal string elementName;
            internal ushort elementSize;
            internal ushort elementOffset;
            internal object elementData;
        }
    }

    public class DecryptedDaedalusPacket
    {
        #region Packet Elements
        internal readonly static DaedalusGlobal.PacketElement elementSTX = new DaedalusGlobal.PacketElement("STX", 1, 0, -1);
        internal readonly static DaedalusGlobal.PacketElement elementPacketLength = new DaedalusGlobal.PacketElement("packetLength", 2, 1, -1);
        internal readonly static DaedalusGlobal.PacketElement elementPacketIndex = new DaedalusGlobal.PacketElement("packetIndex", 2, 3, -1);
        //internal readonly static DaedalusGlobal.PacketElement elementIDHash = new DaedalusGlobal.PacketElement("IDHash", IDHash.hashSize, 5);
        internal readonly static DaedalusGlobal.PacketElement elementCommand = new DaedalusGlobal.PacketElement("command", 1, 5, -1);
        internal readonly static DaedalusGlobal.PacketElement elementCommandVersion = new DaedalusGlobal.PacketElement("commandVersion", 1, 6, -1);
        internal readonly static DaedalusGlobal.PacketElement elementCommandPayloadLength = new DaedalusGlobal.PacketElement("commandPayloadLength", 2, 7, -1);
        internal readonly static DaedalusGlobal.PacketElement elementCommandPayload = new DaedalusGlobal.PacketElement("commandPayload", -1, 9, -1);
        internal readonly static DaedalusGlobal.PacketElement elementETX = new DaedalusGlobal.PacketElement("ETX", 1, -1, 9);
        internal readonly static DaedalusGlobal.PacketElement elementCRC = new DaedalusGlobal.PacketElement("CRC", 2, -1, 10);
        internal readonly static DaedalusGlobal.PacketElement elementEOT = new DaedalusGlobal.PacketElement("EOT", 1, -1, 12);

        internal readonly static IEnumerable<IDaedalusCommandType> commandTypes;
        #endregion

        public enum Commands : byte { TransmitReadHash = 0x20, AuthorizeHashes = 0x21, DeauthorizeHashes = 0x22, 
            GetAuthorizedHashes = 0x23, ClearAuthorizedHashes = 0x24, PacketIndexSynch = 0x25, ACK = 0x81, NACK = 0x82 };

        #region Instance Variables
        internal ushort packetLength { get; set; }
        internal ushort packetIndex { get; set; }
        //internal IDHash hash { get; set; }
        internal Commands command { get; set; }
        internal byte commandVersion { get; set; }
        internal IDaedalusCommandType commandType { get; set; }
        internal Dictionary<string, DaedalusGlobal.PayloadElement> payload { get; set; }
        #endregion

        #region Constructors
        static DecryptedDaedalusPacket()
        {
            // Find all the classes that implement the Daedalus packet type interface, i.e. all the packet types
            Type interfaceType = typeof(IDaedalusCommandType);
            commandTypes = AppDomain.CurrentDomain.GetAssemblies().ToList()
                .SelectMany(s => s.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p) && p.IsClass)
                .Select(t => (IDaedalusCommandType)Activator.CreateInstance(t));
        }

        internal DecryptedDaedalusPacket()
        {
            packetLength = 0;
            packetIndex = 0;
            command = Commands.NACK;
            commandVersion = 1;
            payload = new Dictionary<string, DaedalusGlobal.PayloadElement>();
        }

        /// <summary>
        /// Builds an DaedalusPacket object from a byte stream.
        /// This constructor assumes the buffer passed to it has already been verified using DaedalusPacket.IsValidPacket
        /// and starts with STX and ends with EOT, with no extraneous bytes.
        /// </summary>
        internal DecryptedDaedalusPacket(byte[] inBuffer, int startIndex, int length, out DaedalusTestApp.DaedalusGlobal.ReturnCodes returnCode)
        {
            // Check for STX and EOT in the right places
            if ((inBuffer[startIndex] != GlobalConstants.STX) || (inBuffer[startIndex + length - 1] != GlobalConstants.EOT))
            {
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                return;
            }

            // Parse packet elements
            packetLength = BitConverter.ToUInt16(inBuffer, startIndex + elementPacketLength.ElementStaticOffset);
            packetIndex = BitConverter.ToUInt16(inBuffer, startIndex + elementPacketIndex.ElementStaticOffset);
            //hash = new IDHash(inBuffer, startIndex + elementIDHash.ElementOffset);
            command = (Commands)inBuffer[startIndex + elementCommand.ElementStaticOffset];
            commandVersion = inBuffer[startIndex + elementCommandVersion.ElementStaticOffset];
            
            ushort payloadLength = BitConverter.ToUInt16(inBuffer, startIndex + elementCommandPayloadLength.ElementStaticOffset);
            payload = new Dictionary<string, DaedalusGlobal.PayloadElement>();

            // Select the first of the packet types (should only be one) that match the type of this command
            IEnumerable<IDaedalusCommandType> commandTypeMatches = commandTypes.Where(p => p.getCommand() == command);

            // Parse this packet's payload
            if (commandTypeMatches.Count() == 0)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer, startIndex, length) + " invokes command that is not implemented.");
#endif
                returnCode = DaedalusGlobal.ReturnCodes.CommandNotImplemented;
            }
            else
            {
                // Just in case there's more than one type for each command, take the first one
                this.commandType = commandTypeMatches.First();

                byte[] payloadBuffer = new byte[length];
                Array.Copy(inBuffer, startIndex, payloadBuffer, 0, length);

                Dictionary<string, DaedalusGlobal.PayloadElement> tryPayload;

                // Try to parse the packet's payload
                returnCode = commandType.parsePayload(payloadBuffer, elementCommandPayload.ElementStaticOffset, out tryPayload);
                this.payload = tryPayload;
#if DEBUG
                if (returnCode == DaedalusGlobal.ReturnCodes.Valid)
                {
                    GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer, startIndex, length) + " constructed.");
                }
                else
                {
                    GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer, startIndex, length) + " construction failed, code: " + Enum.GetName(typeof(DaedalusGlobal.ReturnCodes), returnCode) + ".");
                }
#endif
            }
        }
        #endregion

        #region Public Methods
        internal static bool IsValidPacket (byte[] inBuffer,
            out DaedalusGlobal.ReturnCodes returnCode, out int validatedPacketStart, out int validatedPacketLength)
        {
            // Try to find the STX that starts the packet
            int startIndex = inBuffer.FirstIndexOfSequence(new byte[] { GlobalConstants.STX }, 0);

            // If we couldn't find an STX, there's no packet
            if (startIndex == -1)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " could not find STX.");
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // Decode packetLength from the next two bytes
            // Note packetLength is number of bytes between end of packetLength and CRC
            // Yes, I did that on purpose
            ushort inPacketLength = BitConverter.ToUInt16(inBuffer, startIndex + elementSTX.ElementSize);

            // Check to make sure bufferLength can fit a whole packet
            // Literals are length of STX + length of packetIndex + length of CRC + length of EOT
            //if (inBuffer.Length < (startIndex + inPacketLength + elementSTX.ElementSize + elementPacketIndex.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize))
            if (inBuffer.Length < (startIndex + inPacketLength + elementSTX.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize))
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed length check. Length was " +
                    inBuffer.Length + ", should have been at least " + (startIndex + inPacketLength + elementSTX.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize + 1).ToString());
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // CRC position should be packetLength from index 3, which is just after packetLength
            int CRCIndex = startIndex + elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength;

            // Check for an ETX character right before the CRC and EOT after the CRC
            if (((inBuffer[CRCIndex - 1]) != GlobalConstants.ETX) || ((inBuffer[CRCIndex + 2]) != GlobalConstants.EOT))
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed ETX/EOT check. Byte at ETX position was " + inBuffer[CRCIndex - 1].ToString("X") + ", should be 0x03. Byte at EOT position was " +
                    inBuffer[CRCIndex + 2].ToString("X") + ", should have been 0x04.");
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // Read in CRC of input packet
            ushort CRCIn = BitConverter.ToUInt16(inBuffer, CRCIndex);

            // Calculate CRC from content of input packet, STX to ETX, inclusive
            ushort CRCCalc = CRC16.calc_crc(inBuffer, startIndex, (elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength));

            // If the CRC check failed...
            if (CRCIn != CRCCalc)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed CRC verification. Calculated CRC was " + CRCCalc.ToString("X") + ", input CRC was " + CRCIn.ToString("X"));
#endif
                returnCode = DaedalusGlobal.ReturnCodes.IncorrectCRC;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

#if DEBUG
            GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " verified.");
#endif
            returnCode = DaedalusGlobal.ReturnCodes.Valid;
            validatedPacketStart = startIndex;
            validatedPacketLength = elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength + elementCRC.ElementSize + elementEOT.ElementSize;
            return true;
        }

        internal byte[] toByteBuffer()
        {
            // Initialize the output buffer we're going to build our packet in
            byte[] outBuffer = new byte[this.getTotalPacketLength()];
            outBuffer[elementSTX.ElementStaticOffset] = GlobalConstants.STX;

            // The order we do this in doesn't really matter
            Array.Copy(BitConverter.GetBytes(packetIndex), 0, outBuffer, elementPacketIndex.ElementStaticOffset, elementPacketIndex.ElementSize);
            outBuffer[elementCommand.ElementStaticOffset] = (byte)command;
            outBuffer[elementCommandVersion.ElementStaticOffset] = (byte)commandVersion;

            // Select the first of the packet types (should only be one) that match the type of this command
            IEnumerable<IDaedalusCommandType> commandTypeMatches = commandTypes.Where(p => p.getCommand() == command);

            // This will be reassigned by payloadToByteBuffer
            byte[] payloadBuffer = new byte[1];

            // Parse this packet's payload
            if (commandTypeMatches.Count() == 0)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + command.ToString() + " invokes command that is not implemented.");
#endif
                return null;
                //returnCode = DaedalusGlobal.ReturnCodes.CommandNotImplemented;
            }
            else
            {
                // Just in case there's more than one type for each command, take the first one
                //IDaedalusCommandType commandType = commandTypeMatches.First();
                this.commandType = commandTypeMatches.First();

                commandType.payloadToByteBuffer(this.payload, ref payloadBuffer);
            }

            // I think this will return the sum of all the elementSize properties of each payloadElement in payload...
            //int payloadLength = payload.Values.Sum(x => x.elementSize);

            Array.Copy(BitConverter.GetBytes(payloadBuffer.Length), 0, outBuffer, elementCommandPayloadLength.ElementStaticOffset, elementCommandPayloadLength.ElementSize);

            Array.Copy(payloadBuffer, 0, outBuffer, elementCommandPayload.ElementStaticOffset, payloadBuffer.Length);

            ushort outPacketLengthFieldValue = (ushort)(elementPacketIndex.ElementSize + elementCommand.ElementSize + elementCommandVersion.ElementSize + 
                elementCommandPayloadLength.ElementSize + payloadBuffer.Length + elementETX.ElementSize);

            // Do packetLength now because we actually know the length now
            Array.Copy(BitConverter.GetBytes(outPacketLengthFieldValue), 0, outBuffer, elementPacketLength.ElementStaticOffset, elementPacketLength.ElementSize);

            outBuffer[elementETX.ElementVariableOffset + payloadBuffer.Length] = GlobalConstants.ETX;

            // Calculate CRC from content of input packet, STX to ETX, inclusive
            ushort CRCCalc = CRC16.calc_crc(outBuffer, 0, (elementSTX.ElementSize + elementPacketLength.ElementSize + outPacketLengthFieldValue));

            Array.Copy(BitConverter.GetBytes(CRCCalc), 0, outBuffer, elementCRC.ElementVariableOffset + payloadBuffer.Length, elementCRC.ElementSize);

            outBuffer[elementEOT.ElementVariableOffset + payloadBuffer.Length] = GlobalConstants.EOT;

            return outBuffer;
        }

        /// <summary>
        /// Used for packets built with the default contructor and then manually put together.
        /// This method will calculate and set the packetLength field value based on the size of the payload
        /// field and the static size of other packet elements.
        /// </summary>
        internal void setPacketLengthFieldValue()
        {   //<packetIndex><command><commandVersion><commandPayloadLength><commandPayload>
            this.packetLength = (ushort)(elementPacketIndex.ElementSize + elementCommand.ElementSize +
                elementCommandVersion.ElementSize + elementCommandPayloadLength.ElementSize + this.commandType.getPayloadLength(this.payload) +
                elementETX.ElementSize);
        }

        internal int getTotalPacketLength()
        {
            return (packetLength + elementSTX.ElementSize + elementPacketLength.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize);
        }

        internal int getPacketLengthFieldValue()
        {
            return packetLength;
        }
        #endregion

        #region Private Methods

        #endregion

        #region Public Properties

        #endregion
    }

    internal class EncryptedDaedalusPacket
    {
        #region Packet Elements
        internal readonly static DaedalusGlobal.PacketElement elementSTX = new DaedalusGlobal.PacketElement("STX", 1, 0, -1);
        internal readonly static DaedalusGlobal.PacketElement elementPacketLength = new DaedalusGlobal.PacketElement("packetLength", 2, 1, -1);

        internal readonly static DaedalusGlobal.PacketElement elementEncryptedPacket = new DaedalusGlobal.PacketElement("encryptedPacket", -1, 3, -1);
        internal readonly static DaedalusGlobal.PacketElement elementETX = new DaedalusGlobal.PacketElement("ETX", 1, -1, 3);
        internal readonly static DaedalusGlobal.PacketElement elementCRC = new DaedalusGlobal.PacketElement("CRC", 2, -1, 4);
        internal readonly static DaedalusGlobal.PacketElement elementEOT = new DaedalusGlobal.PacketElement("EOT", 1, -1, 6);
        #endregion

        #region Instance Variables
        internal string encryptionKey { get; set; }
        internal int encryptedPacketLength { get; set; }
        private byte[] _encryptedPayload;
        internal byte[] encryptedPayload 
        { 
            // Implements lazy loading (encrypting/decrypting) for encrypted and decrypted forms
            // of the Daedalus packet on demand
            get
            {
                if (_encryptedPayload == null)
                {
                    if ((encryptionKey == "") || (encryptionKey == null))
                    {
                        throw new NullReferenceException("Invalid request for the encrypted payload of a Daedalus " +
                            "packet that does not currently have an encrypted payload or an encryption key set.");
                    }
                    else if (_decryptedPayload == null)
                    {
                        throw new NullReferenceException("Invalid request for the encrypted payload of a Daedalus " +
                            "packet that does not currently have an encrypted or decrypted payload set.");
                    }
                    else
                    {
                        // Encrypt the decrypted payload, store it, and return it
                        _encryptedPayload = Convert.FromBase64String(AesEncryptamajig.Encrypt(Convert.ToBase64String(
                        _decryptedPayload), encryptionKey));

                        return _encryptedPayload;
                    }
                }
                else
                {
                    return _encryptedPayload;
                }
            } 
            set
            {
                _encryptedPayload = value;   
            } 
        }
        private byte[] _decryptedPayload;
        internal byte[] decryptedPayload 
        {    
            // Implements lazy loading (encrypting/decrypting) for encrypted and decrypted forms
            // of the Daedalus packet on demand
            get
            {
                if (_decryptedPayload == null)
                {
                    if ((encryptionKey == "") || (encryptionKey == null))
                    {
                        throw new NullReferenceException("Invalid request for the decrypted payload of a Daedalus " +
                            "packet that does not currently have a decrypted payload or encryption key set.");
                    }
                    else if (_encryptedPayload == null)
                    {
                        throw new NullReferenceException("Invalid request for the encrypted payload of a Daedalus " +
                            "packet that does not currently have an encrypted or decrypted payload set.");
                    }
                    else
                    {
                        // Decrypt the encrypted payload, store it, and return it
                        _decryptedPayload = Convert.FromBase64String(AesEncryptamajig.Decrypt(Convert.ToBase64String(
                        _encryptedPayload), encryptionKey));

                        return _decryptedPayload;
                    }
                }
                else
                {
                    return _decryptedPayload;
                }
            } 
            set
            {
                _decryptedPayload = value;   
            } 
        }
        //private DecryptedDaedalusPacket _clearPacket;
        //internal DecryptedDaedalusPacket clearPacket { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Builds a DaedalusPacket object from an encrypted byte stream.
        /// This constructor assumes the buffer passed to it has already been verified using DaedalusPacket.IsValidPacket
        /// and starts with STX and ends with EOT, with no extraneous bytes.
        /// </summary>
        internal EncryptedDaedalusPacket(byte[] inBuffer, int startIndex, int length, out DaedalusTestApp.DaedalusGlobal.ReturnCodes returnCode, string AESkey)
        {
            // Check for STX and EOT in the right places
            if ((inBuffer[startIndex] != GlobalConstants.SOH) || (inBuffer[startIndex + length - 1] != GlobalConstants.EOT))
            {
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                return;
            }

            encryptionKey = AESkey;

            // Parse packet elements
            encryptedPacketLength = BitConverter.ToUInt16(inBuffer, elementPacketLength.ElementStaticOffset + startIndex);

            // Copy encrypted payload to internal object
            encryptedPayload = new byte[startIndex + encryptedPacketLength - elementETX.ElementSize];
            Array.Copy(inBuffer, startIndex + elementEncryptedPacket.ElementStaticOffset, encryptedPayload, 0, encryptedPacketLength - elementETX.ElementSize);

            // Decrypt <encryptedPacket>
            decryptedPayload = Convert.FromBase64String(AesEncryptamajig.Decrypt(Convert.ToBase64String(
                encryptedPayload), AESkey));

            returnCode = DaedalusGlobal.ReturnCodes.Valid;
        }

        internal EncryptedDaedalusPacket(DecryptedDaedalusPacket inPacket, string AESKey)
        {
            encryptionKey = AESKey;

            // The decrypted packet's entire size is the payload of the encrypted packet, so the <encryptedPacketLength> is that + size of ETX
            encryptedPacketLength = inPacket.getTotalPacketLength() + elementETX.ElementSize;

            // Copy the decrypted packet into the local byte buffer
            decryptedPayload = new byte[inPacket.getTotalPacketLength()];
            Array.Copy(inPacket.toByteBuffer(), decryptedPayload, inPacket.getTotalPacketLength()); 
        }

        #endregion

        #region Public Methods
        internal static bool IsValidPacket(byte[] inBuffer,
            out DaedalusGlobal.ReturnCodes returnCode, out int validatedPacketStart, out int validatedPacketLength)
        {
            // Try to find the SOH that starts the packet
            int startIndex = inBuffer.FirstIndexOfSequence(new byte[] { GlobalConstants.SOH }, 0);

            // If we couldn't find an SOH, there's no packet
            if (startIndex == -1)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " could not find SOH.");
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // Decode packetLength from the next two bytes
            // Note packetLength is number of bytes between end of packetLength and CRC
            // Yes, I did that on purpose
            ushort inPacketLength = BitConverter.ToUInt16(inBuffer, startIndex + elementSTX.ElementSize);

            // Check to make sure bufferLength can fit a whole packet
            if (inBuffer.Length < (startIndex + inPacketLength + elementSTX.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize))
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed length check. Length was " +
                    inBuffer.Length + ", should have been at least " + (startIndex + inPacketLength + elementSTX.ElementSize + elementCRC.ElementSize + elementEOT.ElementSize + 1).ToString());
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // CRC position should be packetLength from index 3, which is just after packetLength
            int CRCIndex = startIndex + elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength;

            // Check for an ETX character right before the CRC and EOT after the CRC
            if (((inBuffer[CRCIndex - 1]) != GlobalConstants.ETX) || ((inBuffer[CRCIndex + 2]) != GlobalConstants.EOT))
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed ETX/EOT check. Byte at ETX position was " + inBuffer[CRCIndex - 1].ToString("X") + ", should be 0x03. Byte at EOT position was " +
                    inBuffer[CRCIndex + 2].ToString("X") + ", should have been 0x04.");
#endif
                returnCode = DaedalusGlobal.ReturnCodes.InvalidPacketStructure;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

            // Read in CRC of input packet
            ushort CRCIn = BitConverter.ToUInt16(inBuffer, CRCIndex);

            // Calculate CRC from content of input packet, STX to ETX, inclusive
            ushort CRCCalc = CRC16.calc_crc(inBuffer, startIndex, elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength);

            // If the CRC check failed...
            if (CRCIn != CRCCalc)
            {
#if DEBUG
                GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " failed CRC verification. Calculated CRC was " + CRCCalc.ToString("X") + ", input CRC was " + CRCIn.ToString("X"));
#endif
                returnCode = DaedalusGlobal.ReturnCodes.IncorrectCRC;
                validatedPacketStart = int.MaxValue;
                validatedPacketLength = int.MaxValue;
                return false;
            }

#if DEBUG
            GlobalMethods.TestLog("Packet " + BitConverter.ToString(inBuffer) + " verified.");
#endif
            returnCode = DaedalusGlobal.ReturnCodes.Valid;
            validatedPacketStart = (int)startIndex;
            validatedPacketLength = (int)(elementSTX.ElementSize + elementPacketLength.ElementSize + inPacketLength + elementCRC.ElementSize + elementEOT.ElementSize);
            return true;
        }

        internal DecryptedDaedalusPacket toDecryptedDaedalusPacket(out DaedalusGlobal.ReturnCodes returnCode)
        {
            DaedalusGlobal.ReturnCodes rc;
            int start;
            int len;

            // Build a clear text packet from the decrypted data
            if (DecryptedDaedalusPacket.IsValidPacket(decryptedPayload, out rc, out start, out len))
            {
                returnCode = DaedalusGlobal.ReturnCodes.Valid;
                return new DecryptedDaedalusPacket(decryptedPayload, start, len, out rc);
            }
            else
            {
                returnCode = rc;
                return null;
            }
        }

        internal byte[] toByteBuffer()
        {
            // To do
            return new byte[] { };
        }
        #endregion
    }
}
