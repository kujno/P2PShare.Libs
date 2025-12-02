using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConncectionHandler
    {
        private static readonly int _initialPort = 57001;

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private TcpClient? _client;
        private NetworkStream? _stream;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Dictionary<string, long>? _filesAndSizes;

        public EventHandler<Dictionary<string, long>>? InviteReceived;
        public EventHandler? Cancelled;
        public EventHandler<string?>? Failed;

        public ConncectionHandler(IPAddress ipLocal)
        {
        }

        private async Task ConnectAsync()
        {
        }

        private async Task ReceiveInviteAsync(IPAddress ip)
        {
            int modulusLength, exponentLength, read;
            string files = String.Empty;
            string[] filesSplit;
            byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], buffer;

            _encryptionSymmetrical = new();

            buffer = new byte[_initialPort.ToString().Length + _encryptionSymmetrical.TagSize + _encryptionSymmetrical.NonceSize];

            try
            {
                EncryptorAsymmetrical encryptorAsymmetrical;

                _client = await GetTcpClientAsync(ip);

                _stream = _client.GetStream();

                await _stream.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                encryptorAsymmetrical = new(modulus, exponent);

                await _stream.WriteAsync(encryptorAsymmetrical.Encrypt(_encryptionSymmetrical.Key), _cancellationTokenSource.Token);

                await _stream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                _client = await GetTcpClientAsync(ip, int.Parse(Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer))));
                _stream = _client.GetStream();
                _filesAndSizes = [];

                do
                {
                    buffer = new byte[1024];

                    read = await _stream.ReadAsync(buffer, _cancellationTokenSource.Token);

                    if (read > 0) files += Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer));
                }
                while (read > 0);
            }
            catch (OperationCanceledException)
            {
                OnCancelled();
                return;
            }
            catch
            {
                _stream?.Dispose();
                _client?.Dispose();

                OnFailed(null);
                return;
            }

            filesSplit = files.Split();
            foreach (string file in filesSplit)
            {
                int index = file.IndexOf(':');

                _filesAndSizes.Add(file.Substring(0, index), long.Parse(file.Substring(index + 1)));
            }

            OnInviteReceived(_filesAndSizes);
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
                client?.Dispose();

                throw new Exception(ex.Message);
            }
            finally
            {
                listener?.Stop();
                listener?.Dispose();
            }

            return client;
        }

        private async Task<TcpClient> GetTcpClientAsync(IPAddress ip)
        {
            return await GetTcpClientAsync(ip, _initialPort);
        }

        private void OnInviteReceived(Dictionary<string, long> files)
        {
            InviteReceived?.Invoke(this, files);
        }

        private void OnCancelled()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        private void OnFailed(string? message)
        {
            Failed?.Invoke(this, message);
        }
    }
}
