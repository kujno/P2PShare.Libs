namespace P2PShare.Libs.Models.Exceptions
{
    public class CouldNotOpenFileException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
