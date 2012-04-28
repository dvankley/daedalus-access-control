using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace DaedalusTestApp.Command_Payload_Forms
{
    public partial class frmTransmitReadHash : Form
    {
        /// <summary>
        /// The primary output of this form, a payload buffer for a Daedalus protocol packet
        /// </summary>
        private byte[] payload;

        public frmTransmitReadHash(ref byte[] inPayload)
        {
            this.payload = inPayload;
            //inPayload = this.payload;
            InitializeComponent();
        }

        private void frmTransmitReadHash_Load(object sender, EventArgs e)
        {
            Type actionType = typeof(TransmitReadHash.ReadHashActions);
            // Fill the combo box with the string representations of the command enums
            // The things I do to avoid magic numbers and code repetition...
            List<TransmitReadHash.TransmitAction_ComboBoxEnumItem> commandItems =
                Enum.GetValues(actionType).Cast<TransmitReadHash.ReadHashActions>()
                .Select(x => new TransmitReadHash.TransmitAction_ComboBoxEnumItem()
            {
                enumString = Enum.GetName(actionType, x),
                displayString = Enum.GetName(actionType, x),
                enumValue = x
            }).ToList();

            cboAction.DataSource = commandItems;
            cboAction.DisplayMember = "displayString";
            cboAction.ValueMember = "enumValue";

            // Select the first entry
            cboAction.SelectedIndex = 0;

            // Defaults for other controls
            cboValueType.SelectedIndex = 1;
            txtValue.Text = "12345678901234567890";
        }

        private void cmdDone_Click(object sender, EventArgs e)
        {
            switch (cboValueType.Items[cboValueType.SelectedIndex].ToString())
            {
                case "Hashed Value":
                    {
                        List<byte> output =
                            Encoding.ASCII.GetBytes(
                            (string)Invoke((Func<object>)
                            (() => this.txtValue.Text)
                            )).ToList();

                        if (output.Count != UserHash.hashSize)
                        {
                            Invoke((Action)(() => MessageBox.Show(this, "Hashed Value input must be 20 bytes long when ASCII encoded.")));
                        }
                        else
                        {
                            // Add the <actionTaken> byte
                            output.Add(
                                (byte)Invoke((Func<object>)(() => cboAction.SelectedItem))
                                );
                            // Set the payload
                            payload = output.ToArray();

                            this.DialogResult = System.Windows.Forms.DialogResult.OK;
                            // And we're done
                            this.Close();
                        }
                    }
                    break;
                case "Value to Hash":
                    using (SHA1 sha = new SHA1CryptoServiceProvider())
                    {
                        // Get the string value from txtValue, encode it as ASCII bytes, hash it, and set it as payload
                        List<byte> output = sha.ComputeHash(
                            Encoding.ASCII.GetBytes(
                            (string)Invoke((Func<object>)
                            (() => this.txtValue.Text)
                            ))).ToList();
                        output.Add(
                            (byte)Invoke((Func<TransmitReadHash.ReadHashActions>)(
                            //(byte)Invoke((Func<byte>)(
                            () => ((TransmitReadHash.TransmitAction_ComboBoxEnumItem)cboAction.SelectedItem).enumValue))
                            );
                        // Set the payload
                        payload = output.ToArray();

                        this.DialogResult = System.Windows.Forms.DialogResult.OK;
                        // And we're done
                        this.Close();
                    }
                    break;
                default:
                    break;                    
            }
        }

        private void cboCancel_Click(object sender, EventArgs e)
        {
            // This result is enough to tell the caller to ignore the value of payload
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }
    }
}
