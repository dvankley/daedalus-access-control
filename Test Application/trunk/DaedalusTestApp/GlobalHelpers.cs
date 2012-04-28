using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace GlobalHelpers
{
    static class GlobalConstants
    {
        #region Global Constants
        public const byte SOH = 0x01;
        public const byte STX = 0x02;
        public const byte ETX = 0x03;
        public const byte EOT = 0x04;
        #endregion
    }

    static class GlobalMethods
    {
        public static void TestLog(string input)
        {
            StackTrace stackTrace = new StackTrace();

            // get calling method name
            Console.WriteLine(stackTrace.GetFrame(1).GetMethod().Name + ": " + input);
        }

        #region Extension methods
        /// <summary>
        /// Send a poll to check if a connection is still active. This is somewhat difficult due to the structure of TCP sockets, i.e.
        /// http://nitoprograms.blogspot.com/2009/05/detection-of-half-open-dropped.html
        /// </summary>
        /// <param name="socket">Socket to check status of.</param>
        /// <returns>True if the socket's still alive and connected, false otherwise.</returns>
        public static bool IsConnected (this Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }

        /// <summary>
        /// Extension class to print out a byte[] as a hexadecimal string.
        /// </summary>
        /// <param name="data">This byte[] instance.</param>
        /// <param name="delimiter">Delimiter to separate hex bytes. Use "-" if you don't care for slightly better performance.</param>
        /// <returns>Hexadecimal string with byte values.</returns>
        public static string ToHexString (this byte[] data, string delimiter)
        {
            if (delimiter != "-")
            {
                return BitConverter.ToString(data).Replace("-", delimiter);
            }
            else
            {
                return BitConverter.ToString(data);
            }
        }

        /// <summary>
        /// Find all instances of <paramref name="pattern"/> in <paramref name="buffer"/>, starting search from <paramref name="startIndex"/>.
        /// </summary>
        /// <param name="buffer">Byte buffer to search in.</param>
        /// <param name="pattern">Byte sequence to search for.</param>
        /// <param name="startIndex">Byte index to start searching from.</param>
        /// <returns>List of buffer positions where <paramref name="pattern"/> occurs.</returns>
        public static List<int> IndexOfSequence (this byte[] buffer, byte[] pattern, int startIndex)
        {
            List<int> positions = new List<int>();
            int i = Array.IndexOf<byte>(buffer, pattern[0], startIndex);
            while (i >= 0 && i <= buffer.Length - pattern.Length)
            {
                byte[] segment = new byte[pattern.Length];
                Buffer.BlockCopy(buffer, i, segment, 0, pattern.Length);
                if (segment.SequenceEqual<byte>(pattern))
                    positions.Add(i);
                i = Array.IndexOf<byte>(buffer, pattern[0], i + pattern.Length);
            }
            return positions;
        }

        /// <summary>
        /// Find the first instance of <paramref name="pattern"/> in <paramref name="buffer"/>, starting search from <paramref name="startIndex"/>.
        /// </summary>
        /// <param name="buffer">Byte buffer to search in.</param>
        /// <param name="pattern">Byte sequence to search for.</param>
        /// <param name="startIndex">Byte index to start searching from.</param>
        /// <returns>First buffer position where <paramref name="pattern"/> occurs, -1 if not found.</returns>
        public static int FirstIndexOfSequence (this byte[] buffer, byte[] pattern, int startIndex)
        {
            int i = Array.IndexOf<byte>(buffer, pattern[0], startIndex);
            while (i >= 0 && i <= buffer.Length - pattern.Length)
            {
                byte[] segment = new byte[pattern.Length];
                Buffer.BlockCopy(buffer, i, segment, 0, pattern.Length);
                if (segment.SequenceEqual<byte>(pattern))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Extension class to print out a byte List as a hexadecimal string.
        /// </summary>
        /// <param name="data">This byte[] instance.</param>
        /// <param name="delimiter">Delimiter to separate hex bytes. Use "-" if you don't care for slightly better performance.</param>
        /// <returns>Hexadecimal string with byte values.</returns>
        public static string ToHexString (this List<byte> data, string delimiter)
        {
            if (delimiter != "-")
            {
                return BitConverter.ToString(data.ToArray()).Replace("-", delimiter);
            }
            else
            {
                return BitConverter.ToString(data.ToArray());
            }
        }
        #endregion

        public static string BufferToHexString (byte[] data, int startIndex, int length, string delimiter)
        {
            if (delimiter != "-")
            {
                return BitConverter.ToString(data, startIndex, length).Replace("-", delimiter);
            }
            else
            {
                return BitConverter.ToString(data, startIndex, length);
            }
        }

        /// <summary>
        /// <para>Returns the highest level string enclosed by parentheses.</para>
        /// </summary>
        /// <param name="inputString">String to search.</param>
        /// <returns>Parenthetical substring, without parentheses, or "" if not found</returns>
        public static string FindParenthesisSubString (string inputString)
        {
            int parenthesisInCount = 0;
            int firstParenthesisIndex = 0;

            for (int i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] == '(')
                {
                    if (parenthesisInCount == 0)
                    {
                        firstParenthesisIndex = i;
                    }
                    parenthesisInCount++;
                }
                else if (inputString[i] == ')')
                {
                    parenthesisInCount--;
                    if (parenthesisInCount == 0)
                    {
                        return inputString.Substring(firstParenthesisIndex + 1, i - firstParenthesisIndex - 1);
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// <para>Returns the highest level string enclosed by parentheses.</para>
        /// <para>Must be a nested parenthetical statement to be returned.</para>
        /// </summary>
        /// <param name="inputString">String to search.</param>
        /// <returns>Parenthetical substring, without parentheses, or "" if not found.</returns>
        public static string FindNestedParenthesisSubString (string inputString)
        {
            int parenthesisInCount = 0;
            int parenthesisMaxCount = 0;
            int firstParenthesisIndex = -1;

            for (int i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] == '(')
                {
                    if ((parenthesisInCount == 1) && (firstParenthesisIndex == -1))
                    {
                        firstParenthesisIndex = i;
                    }
                    parenthesisInCount++;
                    if (parenthesisInCount > parenthesisMaxCount)
                    {
                        parenthesisMaxCount = parenthesisInCount;
                    }
                }
                else if (inputString[i] == ')')
                {
                    parenthesisInCount--;
                    // Has to be nested at least one level
                    if ((parenthesisInCount == 0) && (parenthesisMaxCount > 1))
                    {
                        return inputString.Substring(firstParenthesisIndex - 1, i - firstParenthesisIndex + 2);
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Finds the first instance of a given type in a generic list and returns that instance.
        /// </summary>
        /// <typeparam name="T">List type.</typeparam>
        /// <param name="list">Reference to list itself.</param>
        /// <param name="typeToFind">Type to search for.</param>
        /// <returns>First instance in the list of the given type.</returns>
        public static object FindFirstInstanceofType<T> (this List<T> list, Type typeToFind)
        {
            foreach (T item in list)
            {
                if (item.GetType() == typeToFind) { return item; }
            }
            return null;
        }

        /// <summary>
        /// Inserts value into dataBuffer at index, pushing the value at 
        /// index and every succeeding value back by the length of value.
        /// </summary>
        /// <param name="buffer">Buffer to insert value in. Must have enough total space to fit value.</param>
        /// <param name="index">Index of dataBuffer to insert value in.</param>
        /// <param name="bufferLength">Total occupied length of dataBuffer. This will be updated by the 
        /// function to reflect the final length of the buffer after the insert.</param>
        /// <param name="value">Value to insert.</param>
        /// <param name="isLittleEndian">If true, non-array values will be inserted LSB first.</param>
        /// <param name="overwriteBytes">Number of bytes at insert point to overwrite.</param>
        public static void insertValueInBuffer (byte[] dataBuffer, int index, ref int bufferLength, object value, bool isLittleEndian, int overwriteBytes)
        {
            byte[] tailBuffer = new byte[bufferLength - index - overwriteBytes];    // Create a buffer to hold everything in the source buffer on and after the insert point
            Array.Copy(dataBuffer, index + overwriteBytes, tailBuffer, 0, bufferLength - index - overwriteBytes);    // Copy the source buffer on and after the insert point into the temp buffer

            int i = index;

            if (value is byte)
            {
                dataBuffer[i++] = (byte)value;
            }
            else if (value is ushort)
            {
                ushort tempShort;
                tempShort = (ushort)value;
                if (isLittleEndian)
                {
                    dataBuffer[i++] = (byte)(tempShort >> 8);
                    dataBuffer[i++] = (byte)(tempShort & 0xFF);
                }
                else
                {
                    dataBuffer[i++] = (byte)(tempShort & 0xFF);
                    dataBuffer[i++] = (byte)(tempShort >> 8);
                }
            }
            else if (value is uint)
            {
                uint tempInt;
                tempInt = (uint)value;
                if (isLittleEndian)
                {
                    dataBuffer[i++] = (byte)(tempInt >> 24);
                    dataBuffer[i++] = (byte)((tempInt >> 16) & 0xFF);
                    dataBuffer[i++] = (byte)((tempInt >> 8) & 0xFF);
                    dataBuffer[i++] = (byte)(tempInt & 0xFF);
                }
                else
                {
                    dataBuffer[i++] = (byte)(tempInt & 0xFF);
                    dataBuffer[i++] = (byte)((tempInt >> 8) & 0xFF);
                    dataBuffer[i++] = (byte)((tempInt >> 16) & 0xFF);
                    dataBuffer[i++] = (byte)(tempInt >> 24);
                }
            }
            else if (value is byte[])
            {
                byte[] tempArray = (byte[])value;
                Array.Copy(tempArray, 0, dataBuffer, i, tempArray.Length);
                i += tempArray.Length;
            }
            else
            {

                throw new ArgumentException("insertValueInBuffer called with unsupported data type.", "value");
            }
            bufferLength += (i - index - overwriteBytes);
            Array.Copy(tailBuffer, 0, dataBuffer, i, bufferLength - i);

        }

        /// <summary>
        /// Inserts value into dataBuffer at index, pushing the value at index and every succeeding value back 
        /// by the length of value. Assumes that dataBuffer is entirely full of needed data and keeps it that way.
        /// </summary>
        /// <param name="buffer">Buffer to insert value in.</param>
        /// <param name="index">Index of dataBuffer to insert value in.</param>
        /// <param name="value">Value to insert.</param>
        /// <param name="isLittleEndian">If true, non-array values will be inserted LSB first.</param>
        /// <param name="overwriteBytes">Number of bytes at insert point to overwrite.</param>
        public static void insertValueInBuffer (byte[] dataBuffer, int index, object value, bool isLittleEndian, int overwriteBytes)
        {
            int valueSize = Marshal.SizeOf(value.GetType());
            byte[] copyBuffer = new byte[dataBuffer.Length + valueSize - overwriteBytes];    // Create a new buffer to exactly hold the resulting data            
            Array.Copy(dataBuffer, 0, copyBuffer, 0, index);
            Array.Copy(dataBuffer, index + overwriteBytes, copyBuffer, index + valueSize, dataBuffer.Length - index - overwriteBytes);    // Copy the source buffer on and after the insert point into the temp buffer

            int i = index;

            if (value is byte)
            {
                copyBuffer[i++] = (byte)value;
            }
            else if (value is ushort)
            {
                ushort tempShort;
                tempShort = (ushort)value;
                if (isLittleEndian)
                {
                    copyBuffer[i++] = (byte)(tempShort >> 8);
                    copyBuffer[i++] = (byte)(tempShort & 0xFF);
                }
                else
                {
                    copyBuffer[i++] = (byte)(tempShort & 0xFF);
                    copyBuffer[i++] = (byte)(tempShort >> 8);
                }
            }
            else if (value is uint)
            {
                uint tempInt;
                tempInt = (uint)value;
                if (isLittleEndian)
                {
                    copyBuffer[i++] = (byte)(tempInt >> 24);
                    copyBuffer[i++] = (byte)((tempInt >> 16) & 0xFF);
                    copyBuffer[i++] = (byte)((tempInt >> 8) & 0xFF);
                    copyBuffer[i++] = (byte)(tempInt & 0xFF);
                }
                else
                {
                    copyBuffer[i++] = (byte)(tempInt & 0xFF);
                    copyBuffer[i++] = (byte)((tempInt >> 8) & 0xFF);
                    copyBuffer[i++] = (byte)((tempInt >> 16) & 0xFF);
                    copyBuffer[i++] = (byte)(tempInt >> 24);
                }
            }
            else if (value is byte[])
            {
                byte[] tempArray = (byte[])value;
                Array.Copy(tempArray, 0, copyBuffer, i, tempArray.Length);
                i += tempArray.Length;
            }
            else
            {
                throw new ArgumentException("insertValueInBuffer called with unsupported data type.", "value");
            }
            dataBuffer = copyBuffer;
            //bufferLength += (i - index - overwriteBytes);
            //Array.Copy(tailBuffer, 0, dataBuffer, i, bufferLength - i);

        }

        /// <summary>
        /// Adds value to buffer at index. Increments index for the next object to be added.
        /// If isLittleEndian, non-array values are added LSB first.
        /// </summary>
        /// <param name="buffer">Byte array to add value to.</param>
        /// <param name="index">Reference to index in buffer to add value to. Will be incremented 
        /// by the length of value on completion to allow for next value to be easily added.</param>
        /// <param name="value">Value to be added.</param>
        /// <param name="isLittleEndian">If value is not an array and this is set, value will be added
        /// LSB first.</param>
        public static void addValueToBuffer (byte[] buffer, ref int index, object value, bool isLittleEndian)
        {
            if (value is byte)
            {
                buffer[index++] = (byte)value;
            }
            else if (value is ushort)
            {
                ushort tempShort;
                tempShort = (ushort)value;

                if (isLittleEndian)
                {
                    buffer[index++] = (byte)(tempShort & 0xFF);
                    buffer[index++] = (byte)(tempShort >> 8);
                }
                else
                {
                    buffer[index++] = (byte)(tempShort >> 8);
                    buffer[index++] = (byte)(tempShort & 0xFF);
                }
            }
            else if (value is uint)
            {
                uint tempInt;
                tempInt = (uint)value;
                if (isLittleEndian)
                {
                    buffer[index++] = (byte)(tempInt & 0xFF);
                    buffer[index++] = (byte)((tempInt >> 8) & 0xFF);
                    buffer[index++] = (byte)((tempInt >> 16) & 0xFF);
                    buffer[index++] = (byte)(tempInt >> 24);
                }
                else
                {
                    buffer[index++] = (byte)(tempInt >> 24);
                    buffer[index++] = (byte)((tempInt >> 16) & 0xFF);
                    buffer[index++] = (byte)((tempInt >> 8) & 0xFF);
                    buffer[index++] = (byte)(tempInt & 0xFF);
                }
            }
            else if (value is byte[])
            {
                byte[] tempArray = (byte[])value;
                Array.Copy(tempArray, 0, buffer, index, tempArray.Length);
                index += tempArray.Length;
            }
            else
            {
                throw new ArgumentException("AddValueToBuffer called with unsupported data type.", "value");
            }
        }

        /// <summary>
        /// <para>Removes <paramref name="length"/> bytes in <paramref name="buffer"/> at <paramref name="index"/></para>
        /// <para>Uses two Array.Copy operations and one temporary byte[] buffer.</para>
        /// </summary>
        /// <param name="buffer">Target byte[] buffer.</param>
        /// <param name="index">Index at which to remove bytes.</param>
        /// <param name="length">Number of bytes to remove.</param>
        public static void removeValueFromBuffer (byte[] buffer, int index, int length)
        {
            byte[] copyBuffer = new byte[buffer.Length - length];
            Array.Copy(buffer, 0, copyBuffer, 0, index);
            Array.Copy(buffer, index + length, copyBuffer, index, buffer.Length - index - length);
            buffer = copyBuffer;
        }

        /// <summary>
        /// Returns the value of a 16 bit unsigned integer in big endian encoding.
        /// </summary>
        /// <param name="dataBuffer">Buffer containing integer encoding.</param>
        /// <param name="startIndex">Index of encoding MSB.</param>
        /// <returns></returns>
        public static ushort BigEndianToUInt16 (byte[] dataBuffer, int startIndex)
        {
            return (ushort)((dataBuffer[startIndex++] << 8) + dataBuffer[startIndex]);
        }

        /// <summary>
        /// Returns the value of a 32 bit unsigned integer in big endian encoding.
        /// </summary>
        /// <param name="dataBuffer">Buffer containing integer encoding.</param>
        /// <param name="startIndex">Index of encoding MSB.</param>
        /// <returns></returns>
        public static uint BigEndianToUInt32 (byte[] dataBuffer, int startIndex)
        {
            return (uint)((dataBuffer[startIndex++] << 24) + (dataBuffer[startIndex++] << 16) + (dataBuffer[startIndex++] << 8) + dataBuffer[startIndex]);
        }

        /// <summary>
        /// Calculates the additive checksum of the specified bytes in <paramref name="buffer"/>
        /// </summary>
        /// <param name="buffer">Buffer containing bytes to add up.</param>
        /// <param name="startIndex">Index to start calculating at.</param>
        /// <param name="length">Number of bytes to add up starting from <paramref name="startIndex"/></param>
        /// <returns>Total value of all bytes added together. Overflow is dropped.</returns>
        public static byte additiveChecksum (byte[] buffer, int startIndex, int length)
        {
            byte runningSum = 0;

            for (int i = startIndex; i < (startIndex + length); i++)
            {
                unchecked
                {
                    runningSum += buffer[i];
                }
            }
            return runningSum;
        }

        public static bool isASCIIHexDigit (byte input)
        {
            return (((input >= 0x30) && (input < 0x39)) ||
                ((input > 0x40) && (input < 0x47)) ||
                ((input > 0x60) && (input < 0x67)));
        }

        public static byte[] XXHexStringToByteArray (string hexString)
        {
            int hexStringLength = hexString.Length;
            byte[] b = new byte[(hexStringLength / 2) + 1];
            for (int i = 0; i < hexStringLength; i += 2)
            {
                int topChar = (hexString[i] > 0x40 ? hexString[i] - 0x37 : hexString[i] - 0x30) << 4;
                int bottomChar = hexString[i + 1] > 0x40 ? hexString[i + 1] - 0x37 : hexString[i + 1] - 0x30;
                b[i / 2] = (byte)(topChar + bottomChar);
            }
            return b;
        }

        public static byte[] XXHexStringToByteArray (byte[] hexArray)
        {
            int hexStringLength = hexArray.Length;
            byte[] b = new byte[(hexStringLength / 3) + 1];
            for (int i = 0; i < hexStringLength - 1; i += 3)
            {
                int topChar = (hexArray[i] > 0x40 ? hexArray[i] - 0x37 : hexArray[i] - 0x30) << 4;
                int bottomChar = hexArray[i + 1] > 0x40 ? hexArray[i + 1] - 0x37 : hexArray[i + 1] - 0x30;
                b[i / 3] = (byte)(topChar + bottomChar);
            }
            return b;
        }
    }
}
