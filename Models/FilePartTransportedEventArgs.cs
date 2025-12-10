namespace P2PShare.Libs.Models
{
    public class FilePartTransportedEventArgs : EventArgs
    {
        public int AmountOfFiles { get; }
        public int CurrentFile { get; }
        public int Part { get; }
        public SendReceive SendReceive { get; }

        public FilePartTransportedEventArgs(int amountOfFiles, int currentFile, int part, SendReceive sendReceive)
        {
            AmountOfFiles = amountOfFiles;
            CurrentFile = currentFile;
            Part = part;
            SendReceive = sendReceive;
        }
    }
}
