using SharpPcap;
using PacketDotNet;

namespace ClassLibrary
{
    public class PacketWrapper
    {
        public RawCapture _rawCapture;

        public int Count { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public PosixTimeval Timeval { get { return _rawCapture.Timeval; } }
        public LinkLayers LinkLayerType { get { return _rawCapture.LinkLayerType; } }
        public int Length { get { return _rawCapture.Data.Length; } }

        /// <summary>
        /// Конструктор с параметрами
        /// </summary>
        /// <param name="count"></param>
        /// <param name="rawCapture"></param>
        public PacketWrapper(int count, RawCapture rawCapture)
        {
            _rawCapture = rawCapture;
            Count = count;
        }
    }
}
