namespace DaedalusTestApp
{
    partial class frmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.AccessControl = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.cboFieldDevices = new System.Windows.Forms.ComboBox();
            this.lstDeviceAuthorizedHashes = new System.Windows.Forms.ListBox();
            this.ProtocolTester = new System.Windows.Forms.TabPage();
            this.label8 = new System.Windows.Forms.Label();
            this.cmdSendCommand = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.cboProtocolCommandVersion = new System.Windows.Forms.ComboBox();
            this.txtPacketContent = new System.Windows.Forms.TextBox();
            this.cmdDefinePacketPayload = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.txtPacketID = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtDestinationDeviceID = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cboProtocolCommand = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.txtAESKey = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.lstTraffic = new System.Windows.Forms.ListBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.cmdToggleListen = new System.Windows.Forms.Button();
            this.tabControl1.SuspendLayout();
            this.AccessControl.SuspendLayout();
            this.ProtocolTester.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.AccessControl);
            this.tabControl1.Controls.Add(this.ProtocolTester);
            this.tabControl1.Location = new System.Drawing.Point(12, 51);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(984, 237);
            this.tabControl1.TabIndex = 0;
            // 
            // AccessControl
            // 
            this.AccessControl.BackColor = System.Drawing.SystemColors.Control;
            this.AccessControl.Controls.Add(this.label1);
            this.AccessControl.Controls.Add(this.cboFieldDevices);
            this.AccessControl.Controls.Add(this.lstDeviceAuthorizedHashes);
            this.AccessControl.Location = new System.Drawing.Point(4, 22);
            this.AccessControl.Name = "AccessControl";
            this.AccessControl.Padding = new System.Windows.Forms.Padding(3);
            this.AccessControl.Size = new System.Drawing.Size(976, 211);
            this.AccessControl.TabIndex = 0;
            this.AccessControl.Text = "Access Control System Configuration";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(565, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(85, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Field Device List";
            // 
            // cboFieldDevices
            // 
            this.cboFieldDevices.FormattingEnabled = true;
            this.cboFieldDevices.Location = new System.Drawing.Point(565, 31);
            this.cboFieldDevices.Name = "cboFieldDevices";
            this.cboFieldDevices.Size = new System.Drawing.Size(298, 21);
            this.cboFieldDevices.TabIndex = 1;
            // 
            // lstDeviceAuthorizedHashes
            // 
            this.lstDeviceAuthorizedHashes.FormattingEnabled = true;
            this.lstDeviceAuthorizedHashes.Location = new System.Drawing.Point(628, 248);
            this.lstDeviceAuthorizedHashes.Name = "lstDeviceAuthorizedHashes";
            this.lstDeviceAuthorizedHashes.Size = new System.Drawing.Size(235, 329);
            this.lstDeviceAuthorizedHashes.TabIndex = 0;
            // 
            // ProtocolTester
            // 
            this.ProtocolTester.BackColor = System.Drawing.SystemColors.Control;
            this.ProtocolTester.Controls.Add(this.label8);
            this.ProtocolTester.Controls.Add(this.cmdSendCommand);
            this.ProtocolTester.Controls.Add(this.label6);
            this.ProtocolTester.Controls.Add(this.cboProtocolCommandVersion);
            this.ProtocolTester.Controls.Add(this.txtPacketContent);
            this.ProtocolTester.Controls.Add(this.cmdDefinePacketPayload);
            this.ProtocolTester.Controls.Add(this.label5);
            this.ProtocolTester.Controls.Add(this.txtPacketID);
            this.ProtocolTester.Controls.Add(this.label4);
            this.ProtocolTester.Controls.Add(this.txtDestinationDeviceID);
            this.ProtocolTester.Controls.Add(this.label2);
            this.ProtocolTester.Controls.Add(this.cboProtocolCommand);
            this.ProtocolTester.Location = new System.Drawing.Point(4, 22);
            this.ProtocolTester.Name = "ProtocolTester";
            this.ProtocolTester.Padding = new System.Windows.Forms.Padding(3);
            this.ProtocolTester.Size = new System.Drawing.Size(976, 211);
            this.ProtocolTester.TabIndex = 1;
            this.ProtocolTester.Text = "Omega Protocol Tester";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(6, 67);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(81, 13);
            this.label8.TabIndex = 11;
            this.label8.Text = "Packet Content";
            // 
            // cmdSendCommand
            // 
            this.cmdSendCommand.Location = new System.Drawing.Point(9, 145);
            this.cmdSendCommand.Name = "cmdSendCommand";
            this.cmdSendCommand.Size = new System.Drawing.Size(124, 23);
            this.cmdSendCommand.TabIndex = 10;
            this.cmdSendCommand.Text = "Send Command";
            this.cmdSendCommand.UseVisualStyleBackColor = true;
            this.cmdSendCommand.Click += new System.EventHandler(this.cmdSendCommand_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(359, 16);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(92, 13);
            this.label6.TabIndex = 9;
            this.label6.Text = "Command Version";
            // 
            // cboProtocolCommandVersion
            // 
            this.cboProtocolCommandVersion.FormattingEnabled = true;
            this.cboProtocolCommandVersion.Location = new System.Drawing.Point(359, 31);
            this.cboProtocolCommandVersion.Name = "cboProtocolCommandVersion";
            this.cboProtocolCommandVersion.Size = new System.Drawing.Size(92, 21);
            this.cboProtocolCommandVersion.TabIndex = 8;
            // 
            // txtPacketContent
            // 
            this.txtPacketContent.Location = new System.Drawing.Point(9, 83);
            this.txtPacketContent.Name = "txtPacketContent";
            this.txtPacketContent.Size = new System.Drawing.Size(887, 20);
            this.txtPacketContent.TabIndex = 7;
            // 
            // cmdDefinePacketPayload
            // 
            this.cmdDefinePacketPayload.Location = new System.Drawing.Point(457, 31);
            this.cmdDefinePacketPayload.Name = "cmdDefinePacketPayload";
            this.cmdDefinePacketPayload.Size = new System.Drawing.Size(160, 23);
            this.cmdDefinePacketPayload.TabIndex = 6;
            this.cmdDefinePacketPayload.Text = "Define Packet Payload";
            this.cmdDefinePacketPayload.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 16);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(55, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "Packet ID";
            // 
            // txtPacketID
            // 
            this.txtPacketID.Location = new System.Drawing.Point(9, 32);
            this.txtPacketID.Name = "txtPacketID";
            this.txtPacketID.Size = new System.Drawing.Size(100, 20);
            this.txtPacketID.TabIndex = 4;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(112, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(111, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Destination Device ID";
            // 
            // txtDestinationDeviceID
            // 
            this.txtDestinationDeviceID.Location = new System.Drawing.Point(115, 32);
            this.txtDestinationDeviceID.Name = "txtDestinationDeviceID";
            this.txtDestinationDeviceID.Size = new System.Drawing.Size(111, 20);
            this.txtDestinationDeviceID.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(229, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Command";
            // 
            // cboProtocolCommand
            // 
            this.cboProtocolCommand.FormattingEnabled = true;
            this.cboProtocolCommand.Location = new System.Drawing.Point(232, 32);
            this.cboProtocolCommand.Name = "cboProtocolCommand";
            this.cboProtocolCommand.Size = new System.Drawing.Size(121, 21);
            this.cboProtocolCommand.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(530, 22);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 12;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // txtAESKey
            // 
            this.txtAESKey.Location = new System.Drawing.Point(12, 25);
            this.txtAESKey.Name = "txtAESKey";
            this.txtAESKey.Size = new System.Drawing.Size(100, 20);
            this.txtAESKey.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(49, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "AES Key";
            // 
            // lstTraffic
            // 
            this.lstTraffic.FormattingEnabled = true;
            this.lstTraffic.Location = new System.Drawing.Point(6, 19);
            this.lstTraffic.Name = "lstTraffic";
            this.lstTraffic.Size = new System.Drawing.Size(972, 186);
            this.lstTraffic.TabIndex = 3;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lstTraffic);
            this.groupBox1.Location = new System.Drawing.Point(12, 294);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(984, 209);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Traffic";
            // 
            // txtPort
            // 
            this.txtPort.Location = new System.Drawing.Point(196, 25);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(100, 20);
            this.txtPort.TabIndex = 5;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(196, 9);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(26, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Port";
            // 
            // cmdToggleListen
            // 
            this.cmdToggleListen.Location = new System.Drawing.Point(302, 23);
            this.cmdToggleListen.Name = "cmdToggleListen";
            this.cmdToggleListen.Size = new System.Drawing.Size(99, 23);
            this.cmdToggleListen.TabIndex = 7;
            this.cmdToggleListen.Text = "Toggle Listen";
            this.cmdToggleListen.UseVisualStyleBackColor = true;
            this.cmdToggleListen.Click += new System.EventHandler(this.cmdToggleListen_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 517);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.cmdToggleListen);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtAESKey);
            this.Controls.Add(this.tabControl1);
            this.Name = "frmMain";
            this.Text = "Daedalus Home Automation Test App";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain_FormClosing);
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.tabControl1.ResumeLayout(false);
            this.AccessControl.ResumeLayout(false);
            this.AccessControl.PerformLayout();
            this.ProtocolTester.ResumeLayout(false);
            this.ProtocolTester.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage AccessControl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cboFieldDevices;
        private System.Windows.Forms.ListBox lstDeviceAuthorizedHashes;
        private System.Windows.Forms.TabPage ProtocolTester;
        private System.Windows.Forms.TextBox txtPacketContent;
        private System.Windows.Forms.Button cmdDefinePacketPayload;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtPacketID;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtDestinationDeviceID;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cboProtocolCommand;
        private System.Windows.Forms.TextBox txtAESKey;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox lstTraffic;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox cboProtocolCommandVersion;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button cmdSendCommand;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button cmdToggleListen;
        private System.Windows.Forms.Button button1;
    }
}

