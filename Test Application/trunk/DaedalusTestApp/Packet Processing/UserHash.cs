using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace DaedalusTestApp
{
    internal class UserHash
    {
        internal const int hashSize = 20;

        private byte[] hash { get; set; }

        /// <summary>
        /// Build a UserHash object from an already hashed byte buffer in a packet.
        /// </summary>
        /// <param name="?"></param>
        internal UserHash(byte[] inBuffer, int startIndex)
        {
            hash = new byte[hashSize];
            Array.Copy(inBuffer, startIndex, hash, 0, hashSize);
        }

        /// <summary>
        /// Build a UserHash object from a buffer of input data from a scanning device.
        /// </summary>
        /// <param name="inBuffer"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        internal UserHash(byte[] inBuffer, int startIndex, ushort length)
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            hash = sha.ComputeHash(inBuffer, startIndex, length);
        }

        internal byte[] getByteBuffer()
        {
            return hash;
        }
    }
}
