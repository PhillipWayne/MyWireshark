﻿using System;
using System.Windows.Forms;
using SharpPcap;
using SharpPcap.AirPcap;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;
using ClassLibrary;
using System.Collections.Generic;
using System.Linq;

namespace GUI
{
    public partial class MainForm : Form, IMainForm
    {
        private int _selectedDeviceIndex;
        private CaptureDeviceList _devices;

        public MainForm()
        {
            InitializeComponent();

            btnStart.Click += new EventHandler(btnStart_Click);
            btnStop.Click += new EventHandler(btnStop_Click);
            this.FormClosingClick += new EventHandler(FormClosing_Click);
            dataGridView.SelectionChanged += new EventHandler(dataGrid_SelectionChanged);
        }

        #region Properties
        public bool IsSelect
        {
            get
            {
                if (devicesList.SelectedItem != null)
                    return true;
                else return false;
            }
        }

        public int SelectedDevice
        {
            get { return _selectedDeviceIndex; }
            set { _selectedDeviceIndex = value; }
        }

        public int SelectedCellsCount
        {
            get { return dataGridView.SelectedCells.Count; }
        }

        public object DataBoundItem
        {
            get { return dataGridView.Rows[dataGridView.SelectedCells[0].RowIndex].DataBoundItem; }
        }

        public string PacketInfoTextBox
        {
            set { packetInfoTextBox.Text = value; }
        }
        #endregion

        #region Methods
        public void SetPacketsCount(uint count)
        {
            labelPacketsCount.Text = count.ToString();
        }

        public void SetDevices(CaptureDeviceList devices)
        {
            _devices = devices;
            foreach (ICaptureDevice dev in _devices)
            {
                if (dev is AirPcapDevice)
                {
                    AirPcapDevice cpd = dev as AirPcapDevice;
                    devicesList.Items.Add(cpd.Interface.FriendlyName + " - " + dev.Description.ToString());
                }
                else if (dev is WinPcapDevice)
                {
                    WinPcapDevice cpd = dev as WinPcapDevice;
                    devicesList.Items.Add(cpd.Interface.FriendlyName + " - " + dev.Description.ToString());
                }
                else if (dev is LibPcapLiveDevice)
                {
                    LibPcapLiveDevice cpd = dev as LibPcapLiveDevice;
                    devicesList.Items.Add(cpd.Interface.FriendlyName + " - " + dev.Description.ToString());
                }
            }
        }

        public void SetDataSource(BindingSource bs)
        {
            var firstCell = dataGridView.FirstDisplayedCell;
            dataGridView.DataSource = bs;
            //dataGridView.FirstDisplayedCell = dataGridView.Rows[dataGridView.Rows.Count - 1].Cells[0];
            dataGridView.FirstDisplayedCell = firstCell;
        }

        public void BeginInvoke(BindingSource bs, Queue<PacketWrapper> packetStrings)
        {
            if (IsHandleCreated)
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    bs.DataSource = packetStrings.Reverse();
                }
                ));
        }

        public void BeginInvoke(Queue<PacketWrapper> packetStrings, PacketWrapper packetWrapper)
        {
            if (IsHandleCreated)
                this.BeginInvoke(new MethodInvoker(delegate
                {
                    packetStrings.Enqueue(packetWrapper);
                }
                ));
            
        }
        #endregion

        #region Events
        public event EventHandler StartCaptureClick;
        public event EventHandler StopCaptureClick;
        public event EventHandler FormClosingClick;
        public event EventHandler DataGridSelectionChanged;

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (StartCaptureClick != null) StartCaptureClick(this, EventArgs.Empty);
            if (!IsSelect)
                return;

            btnStart.Enabled = false;
            btnStop.Enabled = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            if (StopCaptureClick != null) StopCaptureClick(this, EventArgs.Empty);
        }

        private void FormClosing_Click(object sender, EventArgs e)
        {
            if (FormClosingClick != null) FormClosingClick(this, EventArgs.Empty);
            this.Close();
        }

        private void dataGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (DataGridSelectionChanged != null) DataGridSelectionChanged(this, EventArgs.Empty);
        }

        private void menuFileQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void devicesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedDevice = devicesList.SelectedIndex;
        }
        #endregion

        private void ARPResolveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ICaptureDevice device = _devices[SelectedDevice];
            ARPResolveForm arpForm = new ARPResolveForm(device as LibPcapLiveDevice);
            arpForm.StartPosition = FormStartPosition.CenterScreen;
            arpForm.ShowDialog();
        }
    }
}