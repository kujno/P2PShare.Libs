namespace P2PShare.Libs.Models
{
    public class ConnectionFailedException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}