using System.Threading;

namespace ClassLibrary
{
    public class BackgroundThread
    {
        /// <summary>
        /// When true the background thread will terminate
        /// </summary>
        /// <param name="args">
        /// A <see cref="System.String"/>
        /// </param>
        private bool _threadStop;
        private Thread _backgroundThread;
  
        public bool ThreadStop
        {
            get { return _threadStop; }
            set { _threadStop = value; }
        }

        public BackgroundThread(Thread thread)
        {
            _backgroundThread = thread;
            _backgroundThread.IsBackground = true;
        }

        public void Abort()
        {
            _backgroundThread.Abort();
        }

        public bool IsBackground
        {
            set { _backgroundThread.IsBackground = true; }
        }

        public void Start()
        {
            _backgroundThread.Start();
        }

        public void Join()
        {
            _backgroundThread.Join();
        }
    }
}
