using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionHandler
    {
        private static readonly int _initialPort = 57001;
        private static readonly byte[] y = Encoding.UTF8.GetBytes("y");

        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private TcpClient? _client;
        private NetworkStream? _stream;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Queue<KeyValuePair<string, long>>? _filesAndSizes;

        public EventHandler<Queue<KeyValuePair<string, long>>>? InviteReceived;
        public EventHandler? Cancelled;
        public EventHandler<string?>? Failed;

        public ConnectionHandler(IPAddress ipLocal)
        {
        }

        private async Task ConnectAsync()
        {
        }

        private async Task ReceiveInviteAsync(IPAddress ip)
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
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
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(':');

                _filesAndSizes.Enqueue(new(file.Substring(0, index), long.Parse(file.Substring(index + 1))));
            }

            OnInviteReceived(_filesAndSizes);
        }

        public async Task AcceptFilesAsync(string dictionaryPath)
        {
            try
            {
                await _stream!.WriteAsync(y, _cancellationTokenSource.Token);

                while (_filesAndSizes!.Count > 0)
                {
                    var fileAndSize = _filesAndSizes.Dequeue();


                }
            }
            catch (OperationCanceledException)
            {
                OnCancelled();
            }
            catch (Exception ex)
            {
                OnFailed(ex.Message);
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

        private void OnInviteReceived(Queue<KeyValuePair<string, long>> files)
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
