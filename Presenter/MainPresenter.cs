using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections;
using SharpPcap;
using PacketDotNet;
using GUI;
using ClassLibrary;
using System.Windows.Forms;

namespace Presenter
{
    class MainPresenter
    {
        private readonly IMainForm _view;
        private readonly IFileManager _manager;
        private readonly IMessageService _messageService;

        private string _currentFilePath;
        private List<RawCapture> _packetQueue = new List<RawCapture>();
        private CaptureDeviceList _devices;
        private ICaptureDevice _device;
        private int _packetCount;

        private BackgroundThread _backgroundThread;
 
        private object QueueLock = new object();
        private DateTime LastStatisticsOutput;
        private TimeSpan LastStatisticsInterval = new TimeSpan(0, 0, 2);

        private PacketArrivalEventHandler arrivalEventHandler;
        private CaptureStoppedEventHandler captureStoppedEventHandler;

        private Queue<PacketWrapper> packetStrings;
        private System.Windows.Forms.BindingSource bs;
        private ICaptureStatistics captureStatistics;
        private bool statisticsUiNeedsUpdate = false;

        public MainPresenter(IMainForm view, IFileManager manager, IMessageService messageService)
        {
            _view = view;
            _manager = manager;
            _messageService = messageService;

            _view.SetPacketsCount(0);

            GetDevices();

            _view.StartCaptureClick += new EventHandler(_view_StartCaptureClick);
            _view.StopCaptureClick += new EventHandler(_view_StopCaptureClick);
            _view.FormClosingClick += new EventHandler(_view_FormClosingClick);
            _view.DataGridSelectionChanged += new EventHandler(_view_DataGridSelectionChanged);
        }

        private void _view_DataGridSelectionChanged(object sender, EventArgs e)
        {
            if (_view.SelectedCellsCount == 0)
                return;

            var packetWrapper = (PacketWrapper)_view.DataBoundItem;
            var packet = Packet.ParsePacket(packetWrapper._rawCapture.LinkLayerType, packetWrapper._rawCapture.Data);
            _view.PacketInfoTextBox = packet.ToString(StringOutputType.VerboseColored);
        }

        private void _view_StartCaptureClick(object sender, EventArgs e)
        {
            try
            {
                StartCapture();
            }
            catch(Exception ex)
            {
                _messageService.ShowError(ex.Message);
            }
        }

        private void _view_StopCaptureClick(object sender, EventArgs e)
        {
            try
            {
                StopCapture();
            }
            catch(Exception ex)
            {
                _messageService.ShowError(ex.Message);
            }
        }

        private void _view_FormClosingClick(object sender, EventArgs e)
        {
            Shutdown();
        }

        private void GetDevices()
        {
            _devices = CaptureDeviceList.Instance;
            if (_devices.Count < 1)
            {
                _messageService.ShowMessage("No devices were found on this machine");
                return;
            }
            _view.SetDevices(_devices);
        }

        private void StartCapture()
        {
            _packetCount = 0;
            _device = _devices[_view.SelectedDevice];
            packetStrings = new Queue<PacketWrapper>();
            bs = new System.Windows.Forms.BindingSource();
            _view.SetDataSource(bs);
            LastStatisticsOutput = DateTime.Now;

            // start the background thread
            _backgroundThread = new BackgroundThread(new Thread(BackgroundThreadFunc));
            _backgroundThread.ThreadStop = false;
            _backgroundThread.Start();

            // setup background capture
            arrivalEventHandler = new PacketArrivalEventHandler(device_OnPacketArrival);
            _device.OnPacketArrival += arrivalEventHandler;
            captureStoppedEventHandler = new CaptureStoppedEventHandler(device_OnCaptureStopped);
            _device.OnCaptureStopped += captureStoppedEventHandler;
            _device.Open();

            // force an initial statistics update
            captureStatistics = _device.Statistics;
            UpdateCaptureStatistics();

            // start the background capture
            _device.StartCapture();

            //startStopToolStripButton.Image = global::WinformsExample.Properties.Resources.stop_icon_enabled;
            //startStopToolStripButton.ToolTipText = "Stop capture";
        }

        void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            // print out periodic statistics about this device
            var Now = DateTime.Now; // cache 'DateTime.Now' for minor reduction in cpu overhead
            var interval = Now - LastStatisticsOutput;
            if (interval > LastStatisticsInterval)
            {
                //Console.WriteLine("device_OnPacketArrival: " + e.Device.Statistics);
                captureStatistics = e.Device.Statistics;
                statisticsUiNeedsUpdate = true;
                LastStatisticsOutput = Now;
            }
            // lock QueueLock to prevent multiple threads accessing PacketQueue at
            // the same time
            lock (QueueLock)
            {
                _packetQueue.Add(e.Packet);
            }
        }

        private void UpdateCaptureStatistics()
        {
            //captureStatisticsToolStripStatusLabel.Text = string.Format("Received packets: {0}",
            //                                           captureStatistics.ReceivedPackets);
        }

        void device_OnCaptureStopped(object sender, CaptureStoppedEventStatus status)
        {
            if (status != CaptureStoppedEventStatus.CompletedWithoutError)
            {
                _messageService.ShowError("Error stoping capture");
            }
        }

        private void Shutdown()
        {
            if (_device != null)
            {
                _device.StopCapture();
                _device.Close();
                _device.OnPacketArrival -= arrivalEventHandler;
                _device.OnCaptureStopped -= captureStoppedEventHandler;
                _device = null;

                // ask the background thread to shut down
                _backgroundThread.ThreadStop = true;
                // wait for the background thread to terminate
                _backgroundThread.Join();

                // switch the icon back to the play icon
                //startStopToolStripButton.Image = global::WinformsExample.Properties.Resources.play_icon_enabled;
                //startStopToolStripButton.ToolTipText = "Select device to capture from";
            }
        }

        private void StopCapture()
        {
            Shutdown();
        }

        private void BackgroundThreadFunc()
        {
            while(!_backgroundThread.ThreadStop)
            {
                bool shouldSleep = true;

                lock (QueueLock)
                {
                    if (_packetQueue.Count != 0)
                    {
                        shouldSleep = false;
                    }
                }

                if (shouldSleep)
                {
                    System.Threading.Thread.Sleep(250);
                }
                else // should process the queue
                {
                    List<RawCapture> ourQueue;
                    lock (QueueLock)
                    {
                        // swap queues, giving the capture callback a new one
                        ourQueue = _packetQueue;
                        _packetQueue = new List<RawCapture>();
                    }

                    //Console.WriteLine("BackgroundThread: ourQueue.Count is {0}", ourQueue.Count);

                    foreach (var packet in ourQueue)
                    {
                        // Here is where we can process our packets freely without
                        // holding off packet capture.
                        //
                        // NOTE: If the incoming packet rate is greater than
                        //       the packet processing rate these queues will grow
                        //       to enormous sizes. Packets should be dropped in these
                        //       cases

                        var packetWrapper = new PacketWrapper(_packetCount, packet);

                        _view.BeginInvoke(packetStrings, packetWrapper);

                        _packetCount++;

                        var time = packet.Timeval.Date;
                        var len = packet.Data.Length;
                        //Console.WriteLine("BackgroundThread: {0}:{1}:{2},{3} Len={4}",
                            //time.Hour, time.Minute, time.Second, time.Millisecond, len);
                    }

                    _view.BeginInvoke(bs, packetStrings);

                    if (statisticsUiNeedsUpdate)
                    {
                        UpdateCaptureStatistics();
                        statisticsUiNeedsUpdate = false;
                    }
                }
            }
        }
    }
}