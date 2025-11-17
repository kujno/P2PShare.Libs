using System.Net;
using System.Net.Sockets;

namespace P2PShare.Libs
{
    public class TCPConnection
    {
        private static int initialPort = 57001;

        private CancellationTokenSource _cancellationTokenSource = new();

        public TCPConnection(IPAddress ipLocal)
        {
        }

        private async Task connect()
        {
        }

        private async Task waitForInvite(IPAddress ip)
        {
            TcpListener _tcpListener = new TcpListener(ip, initialPort);
            TcpClient client;
            NetworkStream stream;
            int modulusLength, exponentLength;
            byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength];
            EncryptorAsymmetrical encryptorAsymmetrical;
            EncryptionSymmetrical encryptionSymmetrical = new();

            _tcpListener.Start();

            do
            {
                client = await _tcpListener.AcceptTcpClientAsync(_cancellationTokenSource.Token);
            }
            while (!client.Connected);

            stream = client.GetStream();

            await stream.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

            Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
            Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

            encryptorAsymmetrical = new(modulus, exponent);

            await stream.WriteAsync(encryptorAsymmetrical.Encrypt(encryptionSymmetrical.)) // TODO: repair - aes key should be created in its own class
        }
    }
}
