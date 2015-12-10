using SharpPcap;
using PacketDotNet;

namespace ClassLibrary
{
    public class PacketWrapper
    {
        public RawCapture _rawCapture;

        public int Count { get; private set; }
        public PosixTimeval Timeval { get { return _rawCapture.Timeval; } }
        public LinkLayers LinkLayerType { get { return _rawCapture.LinkLayerType; } }
        public int Length { get { return _rawCapture.Data.Length; } }

        public PacketWrapper(int count, RawCapture rawCapture)
        {
            _rawCapture = rawCapture;
            Count = count;
        }
    }
}
