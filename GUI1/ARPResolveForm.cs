using System.Windows.Forms;
using SharpPcap;
using SharpPcap.LibPcap;

namespace GUI
{
    public partial class ARPResolveForm : Form
    {
        private LibPcapLiveDevice _device;

        public ARPResolveForm(LibPcapLiveDevice device)
        {
            InitializeComponent();
            _device = device;
        }

        private void butARPRequest_Click(object sender, System.EventArgs e)
        {
            System.Net.IPAddress ip;

            if (!System.Net.IPAddress.TryParse(textBoxIP.Text, out ip))
            {
                MessageBox.Show("Incorrect IP adress format");
            }
            else
            {
                ARP arper = new ARP(_device);
                var resolvedMacAdress = arper.Resolve(ip);
                if (resolvedMacAdress == null)
                {
                    MessageBox.Show("No mac address found for this ip", "Info");
                }
                else
                {
                    textBoxMACadr.Text = resolvedMacAdress.ToString();
                }
            }
        }
    }
}
