using System.Threading;

namespace ClassLibrary
{
    public class BackgroundThread
    {
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
