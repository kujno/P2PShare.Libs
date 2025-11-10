using P2PShare.Libs.Models;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class FileHandling
    {
        public static async Task CreateFile(NetworkStream networkStream, string filePath, int fileLength, EncryptionSymmetrical encryption, EncryptionEnum encryptionEnum)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.CreateNew))
            {
                int totalBytesRead = 0;

                while (totalBytesRead < fileLength)
                {
                    int bufferSize = Math.Min(FileTransport.BufferSize, fileLength - totalBytesRead);
                    byte[] buffer;
                    byte[]? decryptedBuffer = null;

                    if (encryptionEnum == EncryptionEnum.Enabled) bufferSize += encryption.TagSize + encryption.NonceSize;

                    buffer = new byte[bufferSize];

                    await networkStream.ReadExactlyAsync(buffer, 0, buffer.Length);

                    if (encryptionEnum == EncryptionEnum.Enabled) decryptedBuffer = encryption.Decrypt(buffer);

                    if (decryptedBuffer is not null) buffer = decryptedBuffer;
                    else if (encryptionEnum == EncryptionEnum.Enabled && decryptedBuffer is null) buffer = Array.Empty<byte>();

                    await fileStream.WriteAsync(buffer, 0, buffer.Length);
                    totalBytesRead += buffer.Length;

                    FileTransport.OnFilePartTransported(CalculatePercentage(fileLength, totalBytesRead));
                }
            }
        }

        public static int CalculatePercentage(long total, long part)
        {
            return (int)(part / (total / 100));
        }
    }
}
