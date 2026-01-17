using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionReceiverHandler : ConnectionHandler
    {
        public static string InviteErrorMessage { get; } = "Receiving invite failed.";

        private EncryptorAsymmetrical? _encryptor;
        private EncryptionSymmetrical? _encryptionSymmetrical;
        private Dictionary<string, long>? _filesAndSizes;
        private bool _encrypted;

        public IPAddress LocalIP { get; }

        public ConnectionReceiverHandler(IPAddress localIP, CancellationToken cancellationToken) : base(cancellationToken) => LocalIP = localIP;

        public async Task<Dictionary<string, long>> ReceiveInviteAsync()
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;

            _encryptionSymmetrical = new();

            try
            {
                byte inviteLength;
                byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength], encryptionBuffer = new byte[_y.Length], buffer = new byte[1024];

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

                // receive invite length
                read = await _netStream!.ReadAsync(buffer, _cancellationToken);

                // ack
                await _netStream.WriteAsync(_y, _cancellationToken);

                buffer = buffer[0..read];
                if (_encrypted) buffer = _encryptionSymmetrical.Decrypt(buffer);
                if (!byte.TryParse(Encoding.UTF8.GetString(buffer), out inviteLength)) throw new();

                // receive invite
                await _netStream!.ReadExactlyAsync(buffer = new byte[inviteLength], _cancellationToken);

                files = Encoding.UTF8.GetString(_encrypted ? _encryptionSymmetrical.Decrypt(buffer) : buffer);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new Exception(InviteErrorMessage);
            }

            filesSplit = files.Split(FileSeparator);
            _filesAndSizes = [];
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                _filesAndSizes[file.Substring(0, index)] = long.Parse(file.Substring(index + 1));
            }

            return _filesAndSizes;
        }

        public async Task<string[]> AcceptFilesAsync(string dictionaryPath)
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
                            var dotIndex = fileAndSize.Key.LastIndexOf('.');
                            string fileName = fileAndSize.Key.Substring(0, dotIndex), fileExt = fileAndSize.Key.Substring(dotIndex + 1), file = $"{fileName}.{fileExt}", path = $"{dictionaryPath}\\{file}";

                            for (int j = 0; File.Exists(path); j++)
                            {
                                file = $"{fileName} ({j}).{fileExt}";
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
