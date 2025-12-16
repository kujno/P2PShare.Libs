using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionReceiverHandler : ConnectionHandler
    {
        private EncryptorAsymmetrical? _encryptor;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Dictionary<string, long>? _filesAndSizes;
        private bool _encrypted;

        public IPAddress LocalIP { get; }

        public ConnectionReceiverHandler(IPAddress localIP, CancellationToken cancellationToken) : base(cancellationToken) => LocalIP = localIP;

        public async Task<Dictionary<string, long>> ReceiveInviteAsync() // should also return if its encrypted
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;
            List<byte> invite = new();

            _encryptionSymmetrical = new();

            try
            {
                byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], encryptionBuffer = new byte[_y.Length], inviteArr;

                _client = await ReceiveTcpClientAsync(LocalIP, _initialPort);

                _netStream = _client!.GetStream();

                await _netStream.ReadExactlyAsync(encryptionBuffer, _cancellationToken);
                _encrypted = encryptionBuffer.SequenceEqual(_y);

                if (_encrypted)
                {
                    await _netStream.ReadExactlyAsync(rsaKey, _cancellationToken);

                    Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                    Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                    _encryptor = new(modulus, exponent);

                    await _netStream.WriteAsync(_encryptor.Encrypt(_encryptionSymmetrical.Key), _cancellationToken);
                }

                _filesAndSizes = [];

                byte[] buffer = new byte[_bufferSize];

                read = await _netStream!.ReadAsync(buffer, _cancellationToken);

                invite.AddRange(buffer);
                inviteArr = invite.ToArray();
                files = Encoding.UTF8.GetString(_encrypted ? _encryptionSymmetrical.Decrypt(inviteArr) : inviteArr);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new Exception("Receiving invite failed.");
            }

            filesSplit = files.Split(FileSeparator);
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                _filesAndSizes[file.Substring(0, index)] = long.Parse(file.Substring(index + 1));
            }

            return _filesAndSizes;
        }

        public async Task<string[]> AcceptFilesAsync(string dictionaryPath) // this should throw exception with a message for user
        {
            List<string> savedFiles = new();

            try
            {
                bool check;
                int amountOfFiles = _filesAndSizes!.Count, port;
                long bufferSize = _initialPort.ToString().Length;
                byte[] buffer;

                buffer = new byte[_encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                await _netStream!.WriteAsync(_encrypted ? _encryptor?.Encrypt(_y) : _y, _cancellationToken);

                // receive port number
                do
                {
                    await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

                    port = int.Parse(Encoding.UTF8.GetString(_encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer));

                    check = IsPortAvailable(LocalIP, port);

                    if (!check) await _netStream.WriteAsync(_encrypted ? _encryptor?.Encrypt(_n) : _n, _cancellationToken);
                    else await _netStream.WriteAsync(_encrypted ? _encryptor?.Encrypt(_y) : _y, _cancellationToken);
                }
                while (!check);

                Dispose();

                using (_client = await ReceiveTcpClientAsync(LocalIP, port))
                {
                    using (_netStream = _client.GetStream())
                    {
                        for (int i = 1; i <= _filesAndSizes.Count; i++)
                        {
                            var fileAndSize = _filesAndSizes.ElementAt(i - 1);
                            string file = fileAndSize.Key, path = $"{dictionaryPath}\\{file}";

                            for (int j = 0; File.Exists(path); j++)
                            {
                                file = $"{fileAndSize.Key} ({j})";
                                path = $"{dictionaryPath}\\{file}";
                            }
                            savedFiles.Add(file);

                            using (FileStream fileStream = new(path, FileMode.Create))
                            {
                                int totalBytesRead = 0;

                                while (totalBytesRead < fileAndSize.Value)
                                {
                                    bufferSize = Math.Min(_bufferSize, fileAndSize.Value - totalBytesRead);
                                    buffer = new byte[_encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                                    await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

                                    totalBytesRead += _encrypted ? buffer.Length - _encryptionDataSize : buffer.Length;

                                    OnFilePartTransported(amountOfFiles, i, CalculatePercentage(fileAndSize.Value, totalBytesRead), SendReceive.Receive);

                                    await fileStream.WriteAsync(_encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer, _cancellationToken);
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new Exception("Receiving file(s) failed.");
            }

            return savedFiles.ToArray();
        }

        public async Task DenyFilesAsync()
        {
            try
            {
                await _netStream!.WriteAsync(_encrypted ? _encryptor?.Encrypt(_n) : _n, _cancellationToken);
            }
            catch
            {
            }
        }

        private async Task<TcpClient> ReceiveTcpClientAsync(IPAddress ip, int port)
        {
            TcpClient? client = null;

            try
            {
                using (TcpListener listener = new(ip, port))
                {
                    listener.Start();
                    do
                    {
                        client = await listener.AcceptTcpClientAsync(_cancellationToken);
                    }
                    while (!client.Connected);
                }
            }
            catch
            {
                client?.Dispose();
                throw;
            }

            return client;
        }
    }
}
