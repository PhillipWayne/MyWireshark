using System;
using System.Windows.Forms;
using System.Collections.Generic;
using SharpPcap;
using SharpPcap.AirPcap;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;
using ClassLibrary;

namespace GUI
{
    public interface IMainForm
    {
        bool IsSelect { get; }
        int SelectedDevice { get; set; }
        int SelectedCellsCount { get; }
        object DataBoundItem { get; }
        string PacketInfoTextBox { set; }

        void SetPacketsCount(uint count);
        void SetDevices(CaptureDeviceList devices);
        void SetDataSource(BindingSource bs);
        void BeginInvoke(BindingSource bs, Queue<PacketWrapper> packetString);
        void BeginInvoke(Queue<PacketWrapper> packetStrings, PacketWrapper packetWrapper);

        event EventHandler StartCaptureClick;
        event EventHandler StopCaptureClick;
        event EventHandler FormClosingClick;
        event EventHandler DataGridSelectionChanged;
    }
}
