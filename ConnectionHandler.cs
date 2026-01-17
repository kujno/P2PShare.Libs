using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public abstract class ConnectionHandler : IDisposable
    {
        public static event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        public static char FileSeparator { get; } = '|';

        protected int _publicKeyLength, _modulusLength, _exponentLength;

        private static readonly int _encryptionDataSize = EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize, _initialPort = 57001, _bufferSize = 8192;
        private static readonly byte[] _y = Encoding.UTF8.GetBytes("y"), _n = Encoding.UTF8.GetBytes("n");
        private static readonly char _inviteSeparator = ':';

        private readonly CancellationToken _cancellationToken;
        private readonly IPAddress _ipRemote, _ipLocal;

        private TcpClient? _client;
        private NetworkStream? _netStream;
        private DecryptorAsymmetrical? _decryptor;
        private EncryptorAsymmetrical? _encryptor;
        private bool _encrypted;

        protected int CalculatePercentage(long fileLength, long bytesProcessed) => (int)((100 / fileLength) * bytesProcessed);

        public void Dispose() => _client?.Dispose();

        public ConnectionHandler(CancellationToken cancellationToken, IPAddress ipRemote, IPAddress ipLocal)
        {
            _cancellationToken = cancellationToken;
            _ipRemote = ipRemote;
            _ipLocal = ipLocal;
        }

        protected void OnFilePartTransported(int amountOfFiles, int currentFile, int part, SendReceive sendReceive) => FilePartTransported?.Invoke(this, new FilePartTransportedEventArgs(amountOfFiles, currentFile, part, sendReceive));

        protected bool IsPortAvailable(IPAddress ip, int port)
        {
            try
            {
                using (TcpListener listener = new(ip, port))
                {
                    listener.Start();

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        protected async Task SendEncryptionStatusAsync(bool encrypted) => await _netStream!.WriteAsync(encrypted ? _y : _n, _cancellationToken);

        protected async Task ReceiveEncryptionStatusAsync()
        {
            byte[] buffer = new byte[_y.Length];

            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

            _encrypted = buffer.SequenceEqual(_y);
        }

        protected async Task SendPublicKeyAsync()
        {
            _decryptor = new();

            await _netStream!.WriteAsync(_decryptor.PublicKey.Modulus!.Concat(_decryptor.PublicKey.Exponent!).ToArray(), _cancellationToken);
        }

        protected async Task ReceivePublicKeyAsync()
        {
            byte[] buffer = new byte[_publicKeyLength];

            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

            _encryptor = new(buffer[0.._modulusLength], buffer[_modulusLength.._exponentLength]);
        }
        
        public async Task SendAsync(IPAddress ipRemote, IPAddress ipLocal, FileInfo[] files, bool encrypted)
        {
            try
            {
                if (!files.All(x => x.Exists)) throw new FileNotFoundException("One or more files to send were not found.");

                EncryptionSymmetrical? encryption = null;
                DecryptorAsymmetrical? decryptor = null;
                Random random = new();
                byte[] bufferSend, bufferSendLength;
                byte[] bufferAsymmetrical;
                int port, modulusLength, publicKeyLength = EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out _);
                string invite = String.Empty;

                if (encrypted) decryptor = new();

                bufferAsymmetrical = new byte[encrypted ? modulusLength : _y.Length];

                if (encrypted)
                {
                    // send encryption status
                    await _netStream.WriteAsync(_y, _cancellationToken);

                    // send public key
                    

                    // receive aes key
                    await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationToken);

                    encryption = new(decryptor.Decrypt(bufferAsymmetrical));
                }
                // send encryption status
                else await _netStream.WriteAsync(_n, _cancellationToken);

                for (int i = 0; i < files.Length; i++) // todo: check if invite is not too long
                {
                    var file = files[i];

                    invite += $"{file.Name}{_inviteSeparator}{file.Length}";
                    if (i < files.Length - 1) invite += FileSeparator;
                }

                bufferSend = Encoding.UTF8.GetBytes(invite.Trim());
                if (encrypted) bufferSend = encryption!.Encrypt(bufferSend);
                bufferSendLength = Encoding.UTF8.GetBytes(bufferSend.Length.ToString());

                // send invite length
                await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(bufferSendLength) : bufferSendLength, _cancellationToken);

                // ack
                await _netStream.ReadExactlyAsync(new byte[_y.Length], _cancellationToken);

                // send invite
                await _netStream.WriteAsync(bufferSend, _cancellationToken);
                await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationToken);

                if (!(encrypted ? decryptor?.Decrypt(bufferAsymmetrical) : bufferAsymmetrical).SequenceEqual(_y)) throw new FileTransportDeniedException("File transport was denied.");

                do
                {
                    do
                    {
                        port = random.Next(49152, 65536);
                    }
                    while (!IsPortAvailable(ipLocal, port));

                    bufferSend = Encoding.UTF8.GetBytes(port.ToString());

                    await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(bufferSend) : bufferSend, _cancellationToken);
                    await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationToken);
                }
                while (!(encrypted ? decryptor?.Decrypt(bufferAsymmetrical) : bufferAsymmetrical).SequenceEqual(_y));

                Dispose();

                using (_client = await ConnectAsync(ipRemote, ipLocal, port))
                {
                    using (_netStream = _client.GetStream())
                    {
                        for (int i = 0; i < files.Length; i++)
                        {
                            using (FileStream fileStream = new(files[i].FullName, FileMode.Open))
                            {
                                for (int j = 0; j < files[i].Length;)
                                {
                                    byte[] buffer = new byte[Math.Min(_bufferSize, files[i].Length - j)];

                                    j += await fileStream.ReadAsync(buffer, _cancellationToken);
                                    await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(buffer) : buffer, _cancellationToken);
                                    OnFilePartTransported(files.Length, i + 1, CalculatePercentage(files[i].Length, j), SendReceive.Send);
                                }
                            }
                        }
                    }
                }
            }
            // refactor this
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (FileTransportDeniedException)
            {
                throw;
            }
            catch
            {
                throw new Exception("Sending file(s) failed.");
            }
        }

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

        private async Task ReceiveTcpClientAsync(IPAddress ip, int port)
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

            _client = client;
        }

        protected async Task ConnectAsync(int port, bool connectingToServer)
        {
            TcpClient client = new();

            try
            {
                bool connected;
                Task? timer = connectingToServer ? Task.Run(async () => await Task.Delay(10000)) : null;

                client.Client.Bind(new IPEndPoint(_ipLocal, 0));

                do
                {
                    try
                    {
                        await client.ConnectAsync(_ipRemote, port, _cancellationToken);

                        connected = client.Connected;
                    }
                    catch
                    {
                        connected = false;
                    }
                }
                while (!connected && (!timer?.IsCompleted ?? false) && connectingToServer);

                timer?.Dispose();
            }
            catch
            {
                client.Dispose();
                throw;
            }

            _client = client;
            _netStream = _client.GetStream();
        }
    }
}
