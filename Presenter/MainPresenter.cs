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

        private List<RawCapture> _packetQueue = new List<RawCapture>();
        private CaptureDeviceList _devices;
        private ICaptureDevice _device;
        private int _packetCount;

        //private BackgroundThread _backgroundThread;
        private Thread _backgroundThread;
        private bool _backgroundThreadStop;
        /// <summary>
        /// Объект, который используется для предотвращения доступа двух потоков к PacketQueue
        /// в одно и тоже время
        /// </summary>
        private object QueueLock = new object();

        /// <summary>
        /// Время последнего вызова PcapDevice.Statistics() на активном устройстве
        /// Позволяет периодически отображать статистику устройства
        /// </summary>
        private DateTime LastStatisticsOutput;

        /// <summary>
        /// Временной интервал между выводом статистики(PcapDevice.Statistics())
        /// </summary>
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
            try
            {
                StopCapture();
            }
            catch(Exception ex)
            {
                _messageService.ShowError(ex.Message);
            }
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

            // старт фонового потока
            _backgroundThread = new Thread(BackgroundThreadFunc);
            _backgroundThreadStop = false;
            _backgroundThread.Start();
            //_backgroundThread = new BackgroundThread(new Thread(BackgroundThreadFunc));
            //_backgroundThread.ThreadStop = false;
            //_backgroundThread.Start();

            // настройка фонового захвата
            arrivalEventHandler = new PacketArrivalEventHandler(device_OnPacketArrival);
            _device.OnPacketArrival += arrivalEventHandler;
            captureStoppedEventHandler = new CaptureStoppedEventHandler(device_OnCaptureStopped);
            _device.OnCaptureStopped += captureStoppedEventHandler;
            _device.Open();

            // начальное обновление статистики
            captureStatistics = _device.Statistics;
            UpdateCaptureStatistics();

            // старт фонового захвата
            _device.StartCapture();
        }

        /// <summary>
        /// Событие прихода нового пакета
        /// </summary>
        void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            // печать периодической статистики об этом устройстве
            var Now = DateTime.Now;
            var interval = Now - LastStatisticsOutput;
            if (interval > LastStatisticsInterval)
            {
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

        /// <summary>
        /// Обновление статистики захвата
        /// </summary>
        private void UpdateCaptureStatistics()
        {
            _view.SetPacketsCount(captureStatistics.ReceivedPackets);
        }

        /// <summary>
        /// Событие остановки захвата
        /// </summary>
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

                // задать фоновый поток для закрытия
                // ask the background thread to shut down
                _backgroundThreadStop = true;

                // ждать прекращения фонового потока
                _backgroundThread.Join();
            }
        }

        private void StopCapture()
        {
            Shutdown();
        }

        /// <summary>
        /// Checks for queued packets. If any exist it locks the QueueLock, saves a
        /// reference of the current queue for itself, puts a new queue back into
        /// place into PacketQueue and unlocks QueueLock. This is a minimal amount of
        /// work done while the queue is locked.
        ///
        /// The background thread can then process queue that it saved without holding
        /// the queue lock.
        /// </summary>
        private void BackgroundThreadFunc()
        {
            while(!_backgroundThreadStop)
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
                        // Здесь мы можем обрабатывать пакеты свободно, не занимая перехватываемое устройство
                        // Если приходящий пакет
                        var packetWrapper = new PacketWrapper(_packetCount, packet);
                        packetStrings.Enqueue(packetWrapper);
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