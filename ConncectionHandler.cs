using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConncectionHandler
    {
        private static int _initialPort = 57001;

        private CancellationTokenSource _cancellationTokenSource = new();

        public ConncectionHandler(IPAddress ipLocal)
        {
        }

        private async Task ConnectAsync()
        {
        }

        private async Task ReceiveInviteAsync(IPAddress ip)
        {
            EncryptionSymmetrical encryption = new();
            TcpClient client = await GetTcpClientAsync(ip, await GetPortAsync(ip, encryption));
        }

        private async Task<byte> GetPortAsync(IPAddress ip, EncryptionSymmetrical encryptionSymmetrical)
        {
            TcpClient? client = null;
            NetworkStream stream;
            int modulusLength, exponentLength;
            EncryptorAsymmetrical encryptorAsymmetrical;
            byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength];
            byte[] buffer = new byte[_initialPort.ToString().Length + encryptionSymmetrical.TagSize + encryptionSymmetrical.NonceSize];

            try
            {
                client = await GetTcpClientAsync(ip);

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
                client?.Dispose();
            }
        }

        private async Task<TcpClient> GetTcpClientAsync(IPAddress ip, int port)
        {
            TcpListener? listener = null;
            TcpClient? client = null;


            try
            {
                listener = new TcpListener(ip, port);
                listener.Start();
                do
                {
                    client = await listener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
                }
                while (!client.Connected);
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

            return client;
        }

        private async Task<TcpClient> GetTcpClientAsync(IPAddress ip)
        {
            return await GetTcpClientAsync(ip, _initialPort);
        }
    }
}
