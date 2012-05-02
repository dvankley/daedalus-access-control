using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using DaedalusTestApp.Command_Payload_Forms;

namespace DaedalusTestApp
{
    /// <summary>
    /// An interface for each type of Daedalus protocol packet
    /// This is to avoid the traditional bigass switch statement for every action associated with processing a packet by type.
    /// This design pattern consolidates all the packet type-based code in one area
    /// </summary>
    public interface IDaedalusCommandType
    {
        /// <summary>
        /// Because interfaces aren't allowed to define fields, this method returns the qualifier that corresponds to
        /// the given command type
        /// </summary>
        /// <returns>Commands enum for the qualifier that corresponds to this command type</returns>
        DecryptedDaedalusPacket.Commands getCommand();

        /// <summary>
        /// Parses the packet buffer and returns a list of payload elements for this command type
        /// </summary>
        /// <param name="inBuffer">Reference to the incoming packet buffer</param>
        /// <param name="payload">List of payload elements parsed from the incoming packet buffer</param>
        /// <returns>Standard return code</returns>
        DaedalusGlobal.ReturnCodes parsePayload(byte[] inBuffer, int startIndex, out Dictionary<string, DaedalusGlobal.PayloadElement> payload);

        /// <summary>
        /// Builds a packet buffer from a command payload data structure based on the deterministic packet format of that command.
        /// </summary>
        /// <param name="payload">Payload data structure to build from</param>
        /// <param name="outputBuffer">Buffer to output to. The byte array passed in will be replaced by a new array
        /// of the appropriate size.</param>
        /// <returns></returns>
        DaedalusGlobal.ReturnCodes payloadToByteBuffer(Dictionary<string, DaedalusGlobal.PayloadElement> payload, out byte[] outputBuffer);

        ushort getPayloadLength(Dictionary<string, DaedalusGlobal.PayloadElement> payload);

        // This is where the magic happens
        DaedalusGlobal.ReturnCodes processAction(DecryptedDaedalusPacket packet, frmMain mainForm, IPEndPoint source);

        /// <summary>
        /// This is only really necessary for the test app. The central system will have entirely different mechanisms for 
        /// generating packet payloads.
        /// </summary>
        /// <param name="mainForm"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        bool showPayloadDefinitionForm(frmMain mainForm, out byte[] payload);
    }

    /// <summary>
    /// This command is used by a field device to alert the central that the field device has read a given user hash and taken 
    /// a given action based on the user hash and the authorized hashes list.
    /// </summary>
    public class TransmitReadHash : IDaedalusCommandType
    {
        internal enum ReadHashActions : byte { GrantedUserAuthorized = 0x20, DeniedUserNotAuthorized = 0x21 }

        internal class TransmitAction_ComboBoxEnumItem
        {
            public ReadHashActions enumValue { get; set; }
            public string enumString { get; set; }
            public string displayString { get; set; }
        }

        public const DecryptedDaedalusPacket.Commands command = DecryptedDaedalusPacket.Commands.TransmitReadHash;

        public DecryptedDaedalusPacket.Commands getCommand()
        {
            return command;
        }

        /// <summary>
        /// Parse a payload byte buffer into a payload data structure.
        /// </summary>
        /// <param name="inBuffer">Incoming byte buffer to parse</param>
        /// <param name="payload">Reference to the payload data structure in which to put the parsed data</param>
        /// <returns>Daedalus return code</returns>
        public DaedalusGlobal.ReturnCodes parsePayload(byte[] inBuffer, int startIndex, out Dictionary<string, DaedalusGlobal.PayloadElement> payload)
        {
            payload = new Dictionary<string, DaedalusGlobal.PayloadElement>();
            //if (payload == null)
            //{
            //    throw new ArgumentNullException("payload", "Payload object passed to " + this.GetType().ToString() + ".parsePayload() as null, should be a instantiated Dictionary.");
            //}

            DaedalusGlobal.ReturnCodes returnCode = DaedalusGlobal.ReturnCodes.Valid;

            payload.Add("hash", new DaedalusGlobal.PayloadElement()
            {
                elementName = "hash",
                elementOffset = 0,
                elementSize = 20,
                //elementData = new UserHash(inBuffer, DecryptedDaedalusPacket.elementCommandPayload.ElementStaticOffset)
                elementData = new UserHash(inBuffer, startIndex)
            });
            payload.Add("actionTaken", new DaedalusGlobal.PayloadElement()
            {
                elementName = "actionTaken",
                elementOffset = (ushort)(payload["hash"].elementSize + payload["hash"].elementOffset),
                elementSize = 1,
                //elementData = (ReadHashActions)inBuffer[DecryptedDaedalusPacket.elementCommandPayload.ElementStaticOffset + payload["hash"].elementSize]
                elementData = (ReadHashActions)inBuffer[startIndex + payload["hash"].elementSize]
            });

            return returnCode;
        }

        public DaedalusGlobal.ReturnCodes payloadToByteBuffer(Dictionary<string, DaedalusGlobal.PayloadElement> payload, out byte[] outputBuffer)
        {            
            if (payload == null)
            {
                throw new ArgumentNullException("payload", "Payload object passed to " + this.GetType().ToString() + ".payloadToByteBuffer() as null, should be a instantiated Dictionary.");
            }

            DaedalusGlobal.ReturnCodes returnCode = DaedalusGlobal.ReturnCodes.Valid;

            // Make sure the payload has the fields required by this command in it
            if (payload.ContainsKey("hash") && payload.ContainsKey("actionTaken"))
            {
                int payloadLength = payload.Sum(x => x.Value.elementSize);

                outputBuffer = new byte[payloadLength];
                byte[] hash = ((UserHash)(payload["hash"].elementData)).getByteBuffer();

                Array.Copy(hash, outputBuffer, UserHash.hashSize);

                outputBuffer[payload["actionTaken"].elementOffset] = (byte)payload["actionTaken"].elementData;                
            }
            else
            {
                outputBuffer = null;
                return DaedalusGlobal.ReturnCodes.PayloadFieldMissing;
            }

            return returnCode;
        }

        public ushort getPayloadLength(Dictionary<string, DaedalusGlobal.PayloadElement> payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException();
            }
            return (ushort)payload.Sum(x => x.Value.elementSize);
        }
        
        public DaedalusGlobal.ReturnCodes processAction(DecryptedDaedalusPacket packet, frmMain mainForm, IPEndPoint source)
        {
            // So this isn't exactly ideal, but I didn't want to write custom mutators for each control.
            // So sue me.
            mainForm.BeginInvoke((Action)(() => mainForm.lstTraffic.Items.Add(
                  packet.packetIndex + ":" + source.ToString() + ": " + BitConverter.ToString(packet.toByteBuffer())
                )));

            return DaedalusGlobal.ReturnCodes.Valid;
        }

        public bool showPayloadDefinitionForm(frmMain mainForm, out byte[] payload)
        {
            frmTransmitReadHash dialogForm = new frmTransmitReadHash();
            DialogResult result = dialogForm.ShowDialog(mainForm);
            payload = dialogForm.payload;
            dialogForm.Dispose();

            // If the dialog was canceled or the payload value was not set...
            if ((result != DialogResult.OK) || (payload == null))
            {
                return false;
            }
            // Otherwise the result was ok and we have a payload value
            else
            {
                return true;
            }            
        }
    }
}
