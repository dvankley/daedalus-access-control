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
using System.Net.NetworkInformation;


namespace DaedalusTestApp
{
    public partial class frmMain : Form
    {
        #region Global Variables
        const string AESKey = "I<3Ponies";
        // Why can't these be const?
        readonly Color listening = Color.Green;
        readonly Color notListening = Color.Red;
        bool lastListeningState = false;

        object commLocker = new object();
        TPLTCPQueuedComm comm;
        static ushort packetIndex = 0;

        System.Threading.Timer checkListenerTimer;

        CancellationTokenSource packetProcessingTokenSource = new CancellationTokenSource();

        object selectedNetworkInterfaceLocker = new object();
        NetworkInterface selectedNetworkInterface;

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

        public frmMain()
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

            // Replace encrypted packet validator later
            bool valid = EncryptedDaedalusPacket.IsValidPacket(buffer, out returnCode, out start, out length);
            //bool valid = DecryptedDaedalusPacket.IsValidPacket(buffer, out returnCode, out start, out length);
            rc = (int)returnCode;
            packetStart = start;
            packetLength = length;
            return valid;
        }
        #endregion

        #region Helper classes
        private class DaedalusCommand_ComboBoxEnumItem
        {
            public DecryptedDaedalusPacket.Commands enumValue { get; set; }
            public string enumString { get; set; }
            public string displayString { get; set; }
        }
        #endregion

        #region UI Event Handlers
        private void frmMain_Load(object sender, EventArgs e)
        {
            txtAESKey.Text = "AESKey";

            // Fill the combo box with the string representations of the command enums
            List<DaedalusCommand_ComboBoxEnumItem> commandItems = DecryptedDaedalusPacket.commandTypes.Select(x => new DaedalusCommand_ComboBoxEnumItem()
            {
                enumString = Enum.GetName(typeof(DecryptedDaedalusPacket.Commands), x.getCommand()),
                displayString = Enum.GetName(typeof(DecryptedDaedalusPacket.Commands), x.getCommand()),
                enumValue = x.getCommand()
            }).ToList();

            cboProtocolCommand.DataSource = commandItems;
            cboProtocolCommand.DisplayMember = "displayString";
            cboProtocolCommand.ValueMember = "enumValue";

            // Select the first entry
            cboProtocolCommand.SelectedIndex = 0;

            // Get network interfaces
            List<NetworkInterface> activeNetworkInterfaces = getActiveNetworkInterfaces();

            lock (selectedNetworkInterfaceLocker)
            {
                // Select the first interface by default
                selectedNetworkInterface = activeNetworkInterfaces[0];

                // Initialize the comm object with the end point from the selected network interface
                comm = new TPLTCPQueuedComm(validatePacket, processNewNetPacket,
                    getDefaultIPv4AddressFromInterface(selectedNetworkInterface), DaedalusGlobal.DaedalusPort);
            }

            updateSelectedInterfaceDisplay();

            checkListenerTimer = new System.Threading.Timer(new TimerCallback(updateListenerStateDisplay), null, 0, 100);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Signal to async tasks that they need to stop what they're doing and shut down
            comm.Dispose();
        }

        private void cmdSendCommand_Click(object sender, EventArgs e)
        {
            // Decrypted packet that we're going to send out
            DecryptedDaedalusPacket outPacket = new DecryptedDaedalusPacket();

            // Should probably invert this and only allow the fields to be set via a constructor (readonly)

            outPacket.commandType = getDaedalusCommandTypeFromComboBox(cboProtocolCommand);
            unchecked { outPacket.packetIndex = packetIndex++; }
            outPacket.command = outPacket.commandType.getCommand();
            outPacket.commandVersion = Byte.Parse((string)Invoke((Func<object>)(() => txtCommandVersion.Text)));
            string payloadString = (string)Invoke((Func<object>)(() => txtPacketPayload.Text));
            byte[] payloadBuffer = GlobalHelpers.GlobalMethods.XXHexStringToByteArray(payloadString.Replace(" ", ""));
            Dictionary<string, DaedalusGlobal.PayloadElement> payload;
            DaedalusGlobal.ReturnCodes rc = outPacket.commandType.parsePayload(payloadBuffer, 0, out payload);
            outPacket.payload = payload;
            if (rc == DaedalusGlobal.ReturnCodes.Valid)
            {
                outPacket.setPacketLengthFieldValue();
                // At this point the decrypted packet should be ready to go

                // Build encrypted packet here
                EncryptedDaedalusPacket encPacket = new EncryptedDaedalusPacket(outPacket, AESKey);

                string IPString = (string)Invoke((Func<object>)(() => txtDestinationIP.Text));
                IPEndPoint destinationEndPoint = new IPEndPoint(IPAddress.Parse(IPString), DaedalusGlobal.DaedalusPort);
                //lock (commLocker)
                //{
                lock (selectedNetworkInterfaceLocker)
                {
                    comm.outPackets.Add(new NetPacket
                    {
                        destination = destinationEndPoint,
                        source = new IPEndPoint(getDefaultIPv4AddressFromInterface(selectedNetworkInterface), DaedalusGlobal.DaedalusPort),
                        transportType = TransportType.Tcp,
                        payload = encPacket.toByteBuffer()
                    });
                }
                //}
            }
            else
            {
                throw new ArgumentException("Packet payload incorrectly formatted for this command type.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DaedalusProtocolTests_ConstructDecryptedPacketFromBytes test1 = new DaedalusProtocolTests_ConstructDecryptedPacketFromBytes();
            test1.PayloadParseCommandNotImplemented();
            test1.PayloadParseSuccess();
            DaedalusProtocolTests_EncryptAndDecryptPacket test2 = new DaedalusProtocolTests_EncryptAndDecryptPacket();
            test2.BuildEncryptedPacketFromDecryptedPacketSuccess();
            test2.BuildEncryptedPacketFromEncryptedBufferSuccess();
            DaedalusProtocolTests_ToByteBufferAndBack test3 = new DaedalusProtocolTests_ToByteBufferAndBack();
            test3.EncryptedPacketToByteBufferCheck();
        }

        /// <summary>
        /// Toggles the listening state of the application's socket listener.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmdToggleListen_Click(object sender, EventArgs e)
        {
            // Deadlock note: nested lock
            //lock (commLocker)
            //{
                lock (selectedNetworkInterfaceLocker)
                {
                    comm.toggleListen(getDefaultIPv4AddressFromInterface(selectedNetworkInterface), DaedalusGlobal.DaedalusPort);                    
                }
            //}
        }

        private void cmdSelectNetworkInterface_Click(object sender, EventArgs e)
        {
            if (comm.isListening())
            {
                Invoke(((Action)(() => MessageBox.Show("Please toggle the socket listener off before attempting to change the active network interface."))));
            }
            else
            {
                List<NetworkInterface> interfaces = getActiveNetworkInterfaces();
                using (frmSelectNetworkInterface form = new frmSelectNetworkInterface(interfaces))
                {
                    if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        lock (selectedNetworkInterfaceLocker)
                        {
                            selectedNetworkInterface = form.selectedNetworkInterface;
                            updateSelectedInterfaceDisplay();
                        }
                    }
                }
            }
        }
        #endregion

        #region private methods

        /// <summary>
        /// Timer callback to update the status display of the "Toggle Listen" button to indicate 
        /// if the application's socket listener is currently active.
        /// </summary>
        /// <param name="state">Null timer callback state object</param>
        private void updateListenerStateDisplay(object state)
        {
            //lock (commLocker)
            //{
                bool isListening = comm.isListening();
                if (isListening != lastListeningState)
                {
                    if (isListening)
                    {
                        BeginInvoke((Action)(() => cmdToggleListen.BackColor = listening));
                    }
                    else
                    {
                        BeginInvoke((Action)(() => cmdToggleListen.BackColor = notListening));
                    }
                    lastListeningState = isListening;
                }
            //}
        }

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
                    IDaedalusCommandType commandType = decPacket.commandType;
                    //IDaedalusCommandType commandType = DecryptedDaedalusPacket.commandTypes.Where(p => p.getCommandType() == decPacket.command).First();

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

        private void cmdDefinePacketPayload_Click(object sender, EventArgs e)
        {
            IDaedalusCommandType commandInterface = getDaedalusCommandTypeFromComboBox(cboProtocolCommand);

            byte[] payload;
            // If we were able to successfully get a payload value...
            if (commandInterface.showPayloadDefinitionForm(this, out payload))
            {
                // Stick our payload into the UI
                Invoke((Action)(() => txtPacketPayload.Text = GlobalHelpers.GlobalMethods.BufferToHexString(payload, 0, payload.Length, " ")));
            }
            else
            {
                // Blank out the payload
                Invoke((Action)(() => txtPacketPayload.Text = ""));
            }
        }

        /// <summary>
        /// Parses the IdaedalusCommandType from the selected combo box, assuming the combo box 
        /// is filled with DaedalusCommand_ComboBoxEnumItems
        /// </summary>
        /// <param name="box"></param>
        /// <returns></returns>
        private IDaedalusCommandType getDaedalusCommandTypeFromComboBox(ComboBox box)
        {
            // Get the currently selected comboboxitem
            // I would be interested in a more generic-friendly way of doing this
            DaedalusCommand_ComboBoxEnumItem selectedCommand = (DaedalusCommand_ComboBoxEnumItem)Invoke((Func<object>)(() => box.SelectedItem));
            return DecryptedDaedalusPacket.commandTypes.Where(p => p.getCommand() == selectedCommand.enumValue).First();
        }

        #region Network Methods
        private List<NetworkInterface> getActiveNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(x =>
                (NetworkInterface.GetIsNetworkAvailable() &&
                (x.OperationalStatus == OperationalStatus.Up) &&
                ((x.NetworkInterfaceType == NetworkInterfaceType.Ethernet) || (x.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet))
                )).ToList();
        }

        private void updateSelectedInterfaceDisplay()
        {
            lock (selectedNetworkInterfaceLocker)
            {
                string text = selectedNetworkInterface.Name + ":" + getDefaultIPv4AddressFromInterface(selectedNetworkInterface).ToString();

                if (lblSelectedInterface.InvokeRequired)
                {
                    Invoke(((Action)(() => lblSelectedInterface.Text = text)));
                }
                else
                {
                    lblSelectedInterface.Text = text;
                }
            }
        }


        ///// <summary>
        ///// Displays a form to the user to select a network interface from a list of
        ///// active interfaces.
        ///// Should only be called on the UI thread.
        ///// </summary>
        ///// <returns>Selected network interface object.</returns>
        //private NetworkInterface getUserNetworkInterfaceSelection(List<NetworkInterface> activeNetworkInterfaces)
        //{

        //}
        #endregion
        #endregion

        #region Public methods
        public static IPAddress getDefaultIPv4AddressFromInterface(NetworkInterface netInterface)
        {
            return netInterface.GetIPProperties().UnicastAddresses.Where(x => (
                           (!IPAddress.IsLoopback(x.Address)) &&
                           (x.Address.AddressFamily == AddressFamily.InterNetwork)
                            )).First().Address;
        }
        #endregion
    }
}
