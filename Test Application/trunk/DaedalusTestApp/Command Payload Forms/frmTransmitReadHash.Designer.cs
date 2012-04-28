namespace DaedalusTestApp.Command_Payload_Forms
{
    partial class frmTransmitReadHash
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
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
        private void InitializeComponent()
        {
            this.txtValue = new System.Windows.Forms.TextBox();
            this.cboValueType = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.cboAction = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cmdDone = new System.Windows.Forms.Button();
            this.cboCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // txtValue
            // 
            this.txtValue.Location = new System.Drawing.Point(12, 29);
            this.txtValue.Name = "txtValue";
            this.txtValue.Size = new System.Drawing.Size(100, 20);
            this.txtValue.TabIndex = 0;
            // 
            // cboValueType
            // 
            this.cboValueType.FormattingEnabled = true;
            this.cboValueType.Items.AddRange(new object[] {
            "Hashed Value",
            "Value to Hash"});
            this.cboValueType.Location = new System.Drawing.Point(118, 28);
            this.cboValueType.Name = "cboValueType";
            this.cboValueType.Size = new System.Drawing.Size(121, 21);
            this.cboValueType.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(115, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(61, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Value Type";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 10);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Value";
            // 
            // cboAction
            // 
            this.cboAction.FormattingEnabled = true;
            this.cboAction.Location = new System.Drawing.Point(118, 80);
            this.cboAction.Name = "cboAction";
            this.cboAction.Size = new System.Drawing.Size(121, 21);
            this.cboAction.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(118, 61);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(37, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Action";
            // 
            // cmdDone
            // 
            this.cmdDone.Location = new System.Drawing.Point(37, 115);
            this.cmdDone.Name = "cmdDone";
            this.cmdDone.Size = new System.Drawing.Size(75, 23);
            this.cmdDone.TabIndex = 6;
            this.cmdDone.Text = "Ok";
            this.cmdDone.UseVisualStyleBackColor = true;
            this.cmdDone.Click += new System.EventHandler(this.cmdDone_Click);
            // 
            // cboCancel
            // 
            this.cboCancel.Location = new System.Drawing.Point(121, 115);
            this.cboCancel.Name = "cboCancel";
            this.cboCancel.Size = new System.Drawing.Size(75, 23);
            this.cboCancel.TabIndex = 7;
            this.cboCancel.Text = "Cancel";
            this.cboCancel.UseVisualStyleBackColor = true;
            this.cboCancel.Click += new System.EventHandler(this.cboCancel_Click);
            // 
            // frmTransmitReadHash
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(258, 162);
            this.Controls.Add(this.cboCancel);
            this.Controls.Add(this.cmdDone);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cboAction);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cboValueType);
            this.Controls.Add(this.txtValue);
            this.Name = "frmTransmitReadHash";
            this.Text = "frmTransmitReadHash";
            this.Load += new System.EventHandler(this.frmTransmitReadHash_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtValue;
        private System.Windows.Forms.ComboBox cboValueType;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cboAction;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button cmdDone;
        private System.Windows.Forms.Button cboCancel;
    }
}