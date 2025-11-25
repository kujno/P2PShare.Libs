using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class TCPConnection
    {
        private static int _initialPort = 57001;

        private CancellationTokenSource _cancellationTokenSource = new();

        public TCPConnection(IPAddress ipLocal)
        {
        }

        private async Task connect()
        {
        }

        private async Task receiveInvite(IPAddress ip) // put the whole code to a method in a try-catch
        {
            EncryptionSymmetrical encryption = new();
        }

        private async Task<byte> getPort(IPAddress ip, EncryptionSymmetrical encryptionSymmetrical)
        {
            TcpListener listener = new TcpListener(ip, _initialPort);
            TcpClient client;
            NetworkStream stream;
            int modulusLength, exponentLength;
            EncryptorAsymmetrical encryptorAsymmetrical;
            byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength];
            byte[] buffer = new byte[_initialPort.ToString().Length + encryptionSymmetrical.TagSize + encryptionSymmetrical.NonceSize];

            listener.Start();

            do
            {
                client = await listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
            }
            while (!client.Connected);

            try
            {
                stream = client.GetStream();

                await stream.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                encryptorAsymmetrical = new(modulus, exponent);

                await stream.WriteAsync(encryptorAsymmetrical.Encrypt(encryptionSymmetrical.Key), _cancellationTokenSource.Token);

                await stream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                return byte.Parse(Encoding.UTF8.GetString(encryptionSymmetrical.Decrypt(buffer)));
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                listener?.Stop();
                listener?.Dispose();
                client?.Dispose();
            }
        }
    }
}
