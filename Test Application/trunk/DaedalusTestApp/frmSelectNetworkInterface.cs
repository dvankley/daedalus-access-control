using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.NetworkInformation;

namespace DaedalusTestApp
{
    public partial class frmSelectNetworkInterface : Form
    {
        #region Instance variables
        private List<NetworkInterface> interfaces;
        internal NetworkInterface selectedNetworkInterface { get; set; }
        #endregion
        public frmSelectNetworkInterface(List<NetworkInterface> interfaces)
        {
            this.interfaces = interfaces;
            selectedNetworkInterface = null;
            InitializeComponent();
        }

        private void frmSelectNetworkInterface_Load(object sender, EventArgs e)
        {
            lsvNetworkInterfaces.Items.AddRange(interfaces.Select(x =>
                {
                    ListViewItem temp = new ListViewItem(new [] {x.Name, frmMain.getDefaultIPv4AddressFromInterface(x).ToString() });
                    temp.Tag = x;
                    return temp;
                }).ToArray());
        }

        private void lsvNetworkInterfaces_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListView.SelectedListViewItemCollection selectedItems = lsvNetworkInterfaces.SelectedItems;
            if (selectedItems.Count == 1)
            {
                // I don't like storing the object reference in the tag as an object, but I
                // can't really think of a better way to do this.
                selectedNetworkInterface = (NetworkInterface)selectedItems[0].Tag;
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                // And we're done
                this.Close();
            }
            else
            {
                throw new ArgumentException("Only one item in the network interfaces list view should be selected when it is double clicked.");
                //this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                //// And we're done
                //this.Close();
            }
        }
    }
}
