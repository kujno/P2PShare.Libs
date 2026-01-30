namespace P2PShare.Libs.Models
{
    public class CouldNotOpenFileException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
