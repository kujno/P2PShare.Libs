namespace P2PShare.Libs.Models
{
    public class FilePartTransportedEventArgs : EventArgs
    {
        public required int AmountOfFiles { get; init; }
        public required int CurrentFile { get; init; }
        public required int Part { get; init; }
        public required SendReceive SendReceive { get; init; }
    }
}
