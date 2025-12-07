namespace P2PShare.Libs.Models
{
    public class FileTransportDeniedException : Exception
    {
        public FileTransportDeniedException()
        {
        }
        public FileTransportDeniedException(string message) : base(message)
        {
        }
    }
}
