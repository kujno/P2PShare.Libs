using P2PShare.Models;

namespace P2PShare.Libs.Models
{
    public class FilesBeingTransportedEventArgs : EventArgs
    {
        public FileInfo[] FileInfos { get; }
        public SendReceiveEnum ReceiveSend { get; }

        public FilesBeingTransportedEventArgs(FileInfo[] fileInfos, SendReceiveEnum receiveSend)
        {
            FileInfos = fileInfos;
            ReceiveSend = receiveSend;
        }
    }
}
