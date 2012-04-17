using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace DaedalusTestApp
{
    public class IDHash
    {
        public const int hashSize = 8;

        public byte[] hash { get; set; }

        /// <summary>
        /// Build a IDHash object from an already hashed byte buffer in a packet.
        /// </summary>
        public IDHash(byte[] inBuffer, int startIndex)
        {
            hash = new byte[hashSize];

            Array.Copy(inBuffer, startIndex, hash, 0, hashSize);
        }

        /// <summary>
        /// Build an IDHash object for a destination DeviceID and packetIndex
        /// </summary>
        /// <param name="DeviceID"></param>
        /// <param name="packetIndex"></param>
        public IDHash(string DeviceID, ushort packetIndex)
        {
            hash = new byte[hashSize];

            ushort index = packetIndex;
            unchecked
            {
                index += 42;
            }

            SHA1 sha = new SHA1CryptoServiceProvider();
            byte[] deviceIDBytes = ASCIIEncoding.ASCII.GetBytes(DeviceID + packetIndex.ToString());
            byte[] tempHash = sha.ComputeHash(deviceIDBytes);
            Array.Copy(tempHash, hash, hashSize);
        }
    }
}
