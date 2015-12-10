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
                if (!_view.IsSelect)
                {
                    _messageService.ShowMessage("Choose the device");
                    return;
                }
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

            _backgroundThread = new BackgroundThread(new Thread(BackgroundThreadFunc));
            _backgroundThread.ThreadStop = false;
            _backgroundThread.Start();

            arrivalEventHandler = new PacketArrivalEventHandler(device_OnPacketArrival);
            _device.OnPacketArrival += arrivalEventHandler;
            captureStoppedEventHandler = new CaptureStoppedEventHandler(device_OnCaptureStopped);
            _device.OnCaptureStopped += captureStoppedEventHandler;
            _device.Open();

            captureStatistics = _device.Statistics;
            UpdateCaptureStatistics();

            _device.StartCapture();
        }

        void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            var Now = DateTime.Now;
            var interval = Now - LastStatisticsOutput;
            if (interval > LastStatisticsInterval)
            {
                captureStatistics = e.Device.Statistics;
                statisticsUiNeedsUpdate = true;
                LastStatisticsOutput = Now;
            }
            lock (QueueLock)
            {
                _packetQueue.Add(e.Packet);
            }
        }

        private void UpdateCaptureStatistics()
        {
            _view.SetPacketsCount(captureStatistics.ReceivedPackets);
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

                _backgroundThread.ThreadStop = true;
                _backgroundThread.Join();
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
                else
                {
                    List<RawCapture> ourQueue;
                    lock (QueueLock)
                    {
                        ourQueue = _packetQueue;
                        _packetQueue = new List<RawCapture>();
                    }

                    foreach (var packet in ourQueue)
                    {
                        var packetWrapper = new PacketWrapper(_packetCount, packet);

                        _view.BeginInvoke(packetStrings, packetWrapper);

                        _packetCount++;
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