namespace P2PShare.Libs.Models
{
    public class FilePartTransportedEventArgs : EventArgs
    {
        public byte AmountOfFiles { get; }
        public byte CurrentFile { get; }
        public byte Part { get; }
        public SendReceiveEnum SendReceive { get; }

        public FilePartTransportedEventArgs(byte amountOfFiles, byte currentFile, byte part, SendReceiveEnum sendReceive)
        {
            AmountOfFiles = amountOfFiles;
            CurrentFile = currentFile;
            Part = part;
            SendReceive = sendReceive;
        }
    }
}
