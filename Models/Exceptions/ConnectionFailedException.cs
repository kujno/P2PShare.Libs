namespace P2PShare.Libs.Models.Exceptions
{
    public class ConnectionFailedException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}