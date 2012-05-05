namespace DaedalusTestApp
{
    partial class frmSelectNetworkInterface
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
            this.lsvNetworkInterfaces = new System.Windows.Forms.ListView();
            this.interfaceName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.interfaceAddress = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // lsvNetworkInterfaces
            // 
            this.lsvNetworkInterfaces.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.interfaceName,
            this.interfaceAddress});
            this.lsvNetworkInterfaces.FullRowSelect = true;
            this.lsvNetworkInterfaces.GridLines = true;
            this.lsvNetworkInterfaces.HideSelection = false;
            this.lsvNetworkInterfaces.Location = new System.Drawing.Point(12, 12);
            this.lsvNetworkInterfaces.MultiSelect = false;
            this.lsvNetworkInterfaces.Name = "lsvNetworkInterfaces";
            this.lsvNetworkInterfaces.Size = new System.Drawing.Size(393, 125);
            this.lsvNetworkInterfaces.TabIndex = 0;
            this.lsvNetworkInterfaces.UseCompatibleStateImageBehavior = false;
            this.lsvNetworkInterfaces.View = System.Windows.Forms.View.Details;
            this.lsvNetworkInterfaces.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.lsvNetworkInterfaces_MouseDoubleClick);
            // 
            // interfaceName
            // 
            this.interfaceName.Text = "Interface Name";
            this.interfaceName.Width = 299;
            // 
            // interfaceAddress
            // 
            this.interfaceAddress.Text = "Address";
            this.interfaceAddress.Width = 89;
            // 
            // frmSelectNetworkInterface
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(417, 149);
            this.Controls.Add(this.lsvNetworkInterfaces);
            this.Name = "frmSelectNetworkInterface";
            this.Text = "Network Interface Selection";
            this.Load += new System.EventHandler(this.frmSelectNetworkInterface_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView lsvNetworkInterfaces;
        private System.Windows.Forms.ColumnHeader interfaceName;
        private System.Windows.Forms.ColumnHeader interfaceAddress;

    }
}