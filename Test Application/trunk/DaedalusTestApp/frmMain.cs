using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using GlobalHelpers;
using DaedalusTestApp.Comms;


namespace DaedalusTestApp
{
    public partial class frmMain : Form
    {
        #region Global Variables
        const string AESKey = "I<3Ponies";

        TPLTCPQueuedComm comm;

        CancellationTokenSource packetProcessingTokenSource = new CancellationTokenSource();
        #region Delegates
        //public delegate void UIDelegate (object UIArg);
        //public delegate void LogDelegate (string input);
        //public delegate string UIReadStringDelegate (System.Windows.Forms.Control controlToRead);
        //public delegate void UISetStringDelegate (System.Windows.Forms.Control controlToSet, string stringToSet);
        //public delegate void UIAddToListBoxDelegate (System.Windows.Forms.ListBox listBox, string stringToSet);
        //public delegate string UIReadComboBoxItemDelegate (System.Windows.Forms.ComboBox comboBox);
        //public delegate int UIReadComboBoxIndexDelegate (System.Windows.Forms.ComboBox comboBox);
        //public delegate void UISetCheckBoxDelegate (System.Windows.Forms.CheckBox checkBox, bool isChecked);
        //public delegate bool UIReadCheckBoxDelegate (System.Windows.Forms.CheckBox checkBox);
        //public delegate void UISetPictureBoxColorDelegate (System.Windows.Forms.PictureBox pictureBox, System.Drawing.Color color);

        //UIDelegate delShowErrMsgBox;
        //UISetPictureBoxColorDelegate delSetPictureBoxColor;
        //UIReadStringDelegate delReadUIString;
        //UISetStringDelegate delSetUIString;
        //UIAddToListBoxDelegate delAddToListBox;
        //UIReadComboBoxItemDelegate delReadCboSelectedItem;
        //UIReadComboBoxIndexDelegate delReadCboSelectedIndex;
        //UISetCheckBoxDelegate delSetCheckBox;
        //UIReadCheckBoxDelegate delReadCheckBox;
        //LogDelegate delWriteToDebugLog;
        #endregion
        #endregion

        public frmMain ()
        {
            InitializeComponent();
        }

        #region Comm processing delegates
        /// <summary>
        /// Delegate passed to comm routines to call when a new packet is received that doesn't already have a 
        /// process waiting for it. This method will begin a new processing sequence.
        /// </summary>
        /// <param name="packet">NetPacket containing the incoming packet to be processed</param>
        internal void processNewNetPacket(NetPacket packet)
        {
            //rxTask = Task.Factory.StartNew(x => networkRxTask(clientTokenSource.Token), "networkRxTask", TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(x => packetProcessingSequence(packet), "packetProcessingSequence" + packet.ToString(), packetProcessingTokenSource.Token);
        }

        /// <summary>
        /// Packet validation delegate passed to comm routines to call when attempting to determine if a valid
        /// new packet appears in an incoming stream.
        /// </summary>
        /// <param name="buffer">Data stream in which to search for a packet</param>
        /// <param name="rc">Output return code</param>
        /// <param name="packetStart">Output index of start of detected packet</param>
        /// <param name="packetLength">Output length of detected packet</param>
        /// <returns>True if a packet was detected, false otherwise. If false, value of output parameters will
        /// be undefined.</returns>
        internal bool validatePacket(byte[] buffer, out int rc, out int packetStart, out int packetLength)
        {
            DaedalusGlobal.ReturnCodes returnCode;
            int start;
            int length;

            bool valid = EncryptedDaedalusPacket.IsValidPacket(buffer, out returnCode, out start, out length);
            rc = (int)returnCode;
            packetStart = start;
            packetLength = length;
            return valid;
        }
        #endregion

        #region Helper classes
        private class ComboBoxEnumItem
        {
            public DecryptedDaedalusPacket.Commands enumValue { get; set; }
            public string enumString { get; set; }
            public string displayString { get; set; }
        }
        #endregion

        #region UI Event Handlers
        private void frmMain_Load(object sender, EventArgs e)
        {
            //#region Delegates
            //delShowErrMsgBox = delegate(object input) { MessageBox.Show(this, input.ToString(), "WashDOT Sylvia Simulator Error", MessageBoxButtons.OK); };
            //delReadUIString = delegate(System.Windows.Forms.Control input) { return input.Text; };
            //delSetUIString = delegate(System.Windows.Forms.Control control, string input) { control.Text = input; };
            //delAddToListBox = delegate(System.Windows.Forms.ListBox listBox, string input)
            //{
            //    listBox.Items.Add(input);
            //    int itemsPerPage = (int)(listBox.Height / listBox.ItemHeight);
            //    listBox.TopIndex = listBox.Items.Count - itemsPerPage;
            //};
            //delReadCboSelectedItem = delegate(System.Windows.Forms.ComboBox input) { return (string)input.SelectedItem; };
            //delReadCboSelectedIndex = delegate(System.Windows.Forms.ComboBox input) { return input.SelectedIndex; };
            //delReadCheckBox = delegate(System.Windows.Forms.CheckBox input) { return input.Checked; };
            //delSetCheckBox = delegate(System.Windows.Forms.CheckBox input, bool isChecked) { input.Checked = isChecked; };
            //delSetPictureBoxColor = delegate(System.Windows.Forms.PictureBox input, System.Drawing.Color color) { input.BackColor = color; };
            //delWriteToDebugLog = delegate(string input) { System.Diagnostics.Debugger.Log(0, "Test", input); };

            //#endregion

            txtAESKey.Text = "AESKey";

            comm = new TPLTCPQueuedComm(validatePacket, processNewNetPacket);

            // Fill the combo box with the string representations of the command enums
            List<ComboBoxEnumItem> commandItems = DecryptedDaedalusPacket.commandTypes.Select(x => new ComboBoxEnumItem()
            {
                enumString = Enum.GetName(typeof(DecryptedDaedalusPacket.Commands), x.getCommandType()),
                displayString = Enum.GetName(typeof(DecryptedDaedalusPacket.Commands), x.getCommandType()),
                enumValue = x.getCommandType()
            }).ToList();

            cboProtocolCommand.DataSource = commandItems;
            cboProtocolCommand.DisplayMember = "displayString";
            cboProtocolCommand.ValueMember = "enumValue";

            // Select the first entry
            cboProtocolCommand.SelectedIndex = 0;
        }

        private void cmdSendCommand_Click (object sender, EventArgs e)
        {
            //if (comm == null || comm.IsDisposed)
            //{
            //    comm = new AsyncTCPComm(packetBufferSize, ValidateOmegaPacketDelegate, ReceivedOmegaPacketDelegate, SocketCloseDelegate, CommExceptionDelegate);

            //}            
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Signal to async tasks that they need to stop what they're doing and shut down
            comm.Dispose();
        }
        #endregion

        private void packetProcessingSequence(NetPacket packet)
        {
            DaedalusGlobal.ReturnCodes rc;
            int packetStart, packetLength;
            // We already did this in the comms stream parser, but what the hey
            if (EncryptedDaedalusPacket.IsValidPacket(packet.payload, out rc, out packetStart, out packetLength))
            {
                // Build the encrypted packet                
                EncryptedDaedalusPacket encPacket = new EncryptedDaedalusPacket(packet.payload, packetStart, packetLength, out rc, AESKey);

                // If the decrypted payload is a valid packet...
                if (DecryptedDaedalusPacket.IsValidPacket(encPacket.decryptedPayload, out rc, out packetStart, out packetLength))
                {
                    // Build the decrypted packet
                    DecryptedDaedalusPacket decPacket = new DecryptedDaedalusPacket(encPacket.decryptedPayload, packetStart, packetLength, out rc);

                    // Resolve the command type of this packet
                    IDaedalusCommandType commandType = DecryptedDaedalusPacket.commandTypes.Where(p => p.getCommandType() == decPacket.command).First();
                    
                    // I don't really like passing the whole form as a parameter, but it was either that, a huge list of parameters, or a big switch statement here
                    commandType.processAction(decPacket, this, packet.source);
                }
            }
        }

        //#region AsyncComm delegates
        //public AsyncComm.ValidatePacketResponse ValidateOmegaPacketDelegate(byte[] packetData, uint packetLength, AsyncComm comm, IPEndPoint remoteEndPoint)
        //{
        //    bool isValid;
        //    DaedalusProtocolPacket.ReturnCodes returnCode;
        //    uint outPacketStart;
        //    ushort outPacketLength;

        //     Attempt to validate the incoming packet as an Omega protocol packet
        //    isValid = DaedalusProtocolPacket.IsValidPacket(packetData, out returnCode, out outPacketStart, out outPacketLength);

        //    return new AsyncComm.ValidatePacketResponse(isValid, outPacketStart, outPacketLength);
        //}

        //public void ReceivedOmegaPacketDelegate(byte[] packetData, AsyncComm comm, IPEndPoint remoteEndPoint)
        //{
        //    BeginInvoke(delAddToListBox, lstTraffic, "From: " + remoteEndPoint + ", server received packet: " +             // Post this transaction to the UI
        //        packetData.ToHexString("-"));
        //}

        //public void SocketCloseDelegate(SocketError socketEvent, AsyncComm comm, IPEndPoint remoteEndPoint)
        //{
        //    switch (socketEvent)
        //    {
        //        case SocketError.NotConnected:
        //            BeginInvoke(delAddToListBox, lstTraffic, "From: " + remoteEndPoint + ", remote end closed socket.");
        //            break;
        //        case SocketError.ConnectionRefused:
        //            BeginInvoke(delAddToListBox, lstTraffic, "From: " + remoteEndPoint + ", remote end refused connection.");
        //            break;
        //        case SocketError.ConnectionReset:
        //            BeginInvoke(delAddToListBox, lstTraffic, "From: " + remoteEndPoint + ", remote end reset connection.");
        //            break;
        //        default:
        //            BeginInvoke(delAddToListBox, lstTraffic, "From: " + remoteEndPoint + ", unknown socket event.");
        //            comm.Dispose();
        //            break;
        //    }
        //}

        //public void CommExceptionDelegate(Exception e, AsyncComm comm)
        //{
        //    BeginInvoke((Action)delegate() { lstTraffic.Items.Add(comm.ToString() + ": " + e.Message); });
        //}
        //#endregion

        private void button1_Click(object sender, EventArgs e)
        {
            DaedalusProtocolTests_ConstructDecryptedPacketFromBytes test1 = new DaedalusProtocolTests_ConstructDecryptedPacketFromBytes();
            test1.PayloadParseCommandNotImplemented();
            test1.PayloadParseSuccess();
            DaedalusProtocolTests_EncryptAndDecryptPacket test2 = new DaedalusProtocolTests_EncryptAndDecryptPacket();
            test2.BuildEncryptedPacketFromDecryptedPacketSuccess();
            test2.BuildEncryptedPacketFromEncryptedBufferSuccess();
        }

        private void cmdToggleListen_Click(object sender, EventArgs e)
        {
            comm.toggleListen();
        }

        private void cmdDefinePacketPayload_Click(object sender, EventArgs e)
        {
            // Get the currently selected comboboxitem
            // I would be interested in a more generic-friendly way of doing this
            ComboBoxEnumItem selectedCommand = (ComboBoxEnumItem)Invoke((Func<object>)(() => cboProtocolCommand.SelectedItem));
            IDaedalusCommandType commandInterface = DecryptedDaedalusPacket.commandTypes.Where(p => p.getCommandType() == selectedCommand.enumValue).First();

            byte[] payload;
            commandInterface.showPayloadDefinitionForm(this, out payload);

        }
    }
}
