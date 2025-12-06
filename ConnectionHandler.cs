using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionHandler : IDisposable
    {
        private static readonly int _initialPort = 57001;
        private static readonly byte[] y = Encoding.UTF8.GetBytes("y"), n = Encoding.UTF8.GetBytes("n");

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly byte _encryptionDataSize = (byte)(EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize);

        private TcpClient? _client;
        private NetworkStream? _netStream;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Queue<KeyValuePair<string, long>>? _filesAndSizes;

        public event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        public async Task<Queue<KeyValuePair<string, long>>> ReceiveInviteAsync(IPAddress ip)
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;
            byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], buffer;

            _encryptionSymmetrical = new();

            buffer = new byte[modulusLength];

            try
            {
                EncryptorAsymmetrical encryptor;
                byte port;
                bool check;

                await ReceiveTcpClientAsync(ip, (byte)_initialPort);

                _netStream = _client?.GetStream();

                await _netStream!.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                encryptor = new(modulus, exponent);

                await _netStream.WriteAsync(encryptor.Encrypt(_encryptionSymmetrical.Key), _cancellationTokenSource.Token);

                do
                {
                    // receive port number
                    await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                    port = byte.Parse(Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer)));

                    check = IsPortAvailable(ip, port);

                    if (!check) await _netStream.WriteAsync(n, _cancellationTokenSource.Token);
                }
                while (!check);

                // change the port management. Receive invite will send new port number. And all traffic except file transfer will be encrypted asymmetrically

                _netStream?.Dispose();
                _client?.Dispose();

                await ReceiveTcpClientAsync(ip, port);
                _netStream = _client?.GetStream();
                _filesAndSizes = [];

                do
                {
                    buffer = new byte[1024];

                    read = await _netStream!.ReadAsync(buffer, _cancellationTokenSource.Token);

                    if (read > 0) files += Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer));
                }
                while (read > 0);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch
            {
                _netStream?.Dispose();
                _client?.Dispose();

                throw new Exception("Receiving invite failed.");
            }

            filesSplit = files.Split();
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(':');

                _filesAndSizes.Enqueue(new(file.Substring(0, index), long.Parse(file.Substring(index + 1))));
            }

            return _filesAndSizes;
        }

        public async Task AcceptFilesAsync(string dictionaryPath)
        {
            byte amountOfFiles = (byte)_filesAndSizes!.Count;
            int fileNum = 0;

            try
            {
                await _netStream!.WriteAsync(y, _cancellationTokenSource.Token);

                while (_filesAndSizes!.Count > 0)
                {
                    var fileAndSize = _filesAndSizes.Dequeue();

                    fileNum++;

                    using (FileStream fileStream = new($"{dictionaryPath}\\{fileAndSize.Key}", FileMode.Create))
                    {
                        int totalBytesRead = 0;

                        while (totalBytesRead < fileAndSize.Value)
                        {
                            byte[] buffer = new byte[Math.Min(8192, fileAndSize.Value - totalBytesRead) + _encryptionDataSize];

                            await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                            totalBytesRead += buffer.Length - _encryptionDataSize;

                            OnFilePartTransported(amountOfFiles, (byte)fileNum, (byte)((100 / fileAndSize.Value) * totalBytesRead), SendReceiveEnum.Receive);

                            await fileStream.WriteAsync(_encryptionSymmetrical?.Decrypt(buffer), _cancellationTokenSource.Token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch
            {
                throw new Exception("Receiving file(s) failed.");
            }
        }

        public async void SendAsync(IPAddress ipRemote, IPAddress ipLocal, FileInfo[] files)
        {
            EncryptionSymmetrical encryption;
            DecryptorAsymmetrical decryptor = new();
            Random random = new();
            byte[] buffer = new byte[decryptor.PublicKey.Modulus!.Length];
            byte port;

            await ConnectAsync(ipRemote, (byte)_initialPort);
            _netStream = _client?.GetStream();

            // send public key
            await _netStream!.WriteAsync(decryptor.PublicKey.Modulus!.Concat(decryptor.PublicKey.Exponent!).ToArray(), 0, EncryptionAsymmetrical.GetPublicKeyLength(out _, out _), _cancellationTokenSource.Token);

            // receive aes key
            await _netStream.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

            encryption = new(decryptor.Decrypt(buffer));

            do
            {
                do
                {
                    port = (byte)random.Next(49152, 65536);
                }
                while (!IsPortAvailable(ipLocal, port));

                await _netStream.WriteAsync(encryption.Encrypt(Encoding.UTF8.GetBytes(port.ToString())), _cancellationTokenSource.Token);
                await 
            }
            while ();
        }

        private async Task ReceiveTcpClientAsync(IPAddress ip, byte port)
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

            _client = client;
        }

        private async Task ConnectAsync(IPAddress ip, byte port)
        {
            TcpClient client = new();

            while (!client.Connected)
            {
                try
                {
                    await client.ConnectAsync(ip, port, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new OperationCanceledException();
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            _client = client;
        }

        private void OnFilePartTransported(byte amountOfFiles, byte currentFile, byte part, SendReceiveEnum sendReceive)
        {
            FilePartTransported?.Invoke(this, new FilePartTransportedEventArgs(amountOfFiles, currentFile, part, sendReceive));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _netStream?.Dispose();
            _client?.Dispose();
        }

        private bool IsPortAvailable(IPAddress ip, byte port)
        {
            TcpListener? listener = null;

            try
            {
                listener = new(ip, port);

                listener.Start();

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                listener?.Stop();
                listener?.Dispose();
            }
        }
    }
}
