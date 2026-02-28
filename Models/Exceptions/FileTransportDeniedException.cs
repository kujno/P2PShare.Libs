namespace P2PShare.Libs.Models.Exceptions
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
