using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public abstract class ConnectionHandler : IDisposable
    {
        public static event EventHandler<FilePartTransportedEventArgs>? FilePartTransported;

        public static string InviteErrorMessage { get; } = "Receiving invite failed.";
        public static char FileSeparator { get; } = '|';

        protected static readonly int _initialPort = 57001, _initialServerPort = _initialPort + 1;

        protected int _publicKeyLength, _modulusLength, _exponentLength;
        protected IPAddress? _ipLocal, _ipRemote;

        private static readonly int _encryptionDataSize = EncryptionSymmetrical.TagSize + EncryptionSymmetrical.NonceSize, _bufferSize = 8192;
        private static readonly byte[] _y = Encoding.UTF8.GetBytes("y"), _n = Encoding.UTF8.GetBytes("n");
        private static readonly char _inviteSeparator = ':';

        private CancellationToken _cancellationToken;

        private TcpClient? _client;
        private NetworkStream? _netStream;
        private DecryptorAsymmetrical? _decryptorAsymmetrical;
        private EncryptorAsymmetrical? _encryptorAsymmetrical;
        private EncryptionSymmetrical? _encryptionSymmetrical;

        protected TcpClient Client
        {
            get => _client!;
            set
            {
                _client = value;
                _netStream = _client.GetStream();
            }
        }

        protected int CalculatePercentage(long fileLength, long bytesProcessed) => (int)((100 / fileLength) * bytesProcessed);

        public void Dispose() => _client?.Dispose();

        protected ConnectionHandler(IPAddress ipLocal, CancellationToken cancellationToken) => AssignCompulsoryFields(ipLocal, cancellationToken);

        protected ConnectionHandler(IPAddress ipLocal, IPAddress ipRemote, CancellationToken cancellationToken)
        {
            AssignCompulsoryFields(ipLocal, cancellationToken);
            _ipRemote = ipRemote;
        }

        private void AssignCompulsoryFields(IPAddress ipLocal, CancellationToken cancellationToken)
        {
            _ipLocal = ipLocal;
            _cancellationToken = cancellationToken;
            _publicKeyLength = EncryptionAsymmetrical.GetPublicKeyLength(out _modulusLength, out _exponentLength);
        }

        protected void OnFilePartTransported(int amountOfFiles, int currentFile, int part, SendReceive sendReceive) => FilePartTransported?.Invoke(this, new FilePartTransportedEventArgs(amountOfFiles, currentFile, part, sendReceive));

        protected bool IsPortAvailable(IPAddress ip, int port)
        {
            if (port == _initialPort || port == _initialServerPort) return false;

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

        protected async Task SendEncryptionKeyAsync()
        {
            byte[] buffer = new byte[_publicKeyLength];

            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

            _encryptorAsymmetrical = new(buffer[0.._modulusLength], buffer[_modulusLength.._exponentLength]);

            await _netStream!.WriteAsync(_encryptorAsymmetrical!.Encrypt((_encryptionSymmetrical = new EncryptionSymmetrical()).Key), _cancellationToken);
        }

        protected async Task ReceiveEncryptionKeyAsync()
        {
            byte[] buffer = new byte[_modulusLength];

            _decryptorAsymmetrical = new();

            await _netStream!.WriteAsync(_decryptorAsymmetrical.PublicKey.Modulus!.Concat(_decryptorAsymmetrical.PublicKey.Exponent!).ToArray(), _cancellationToken);

            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

            _encryptionSymmetrical = new(_decryptorAsymmetrical!.Decrypt(buffer));
        }

        protected async Task<bool> SendInviteAsync(FileInfo[] files, bool encrypted)
        {
            byte[] bufferInvite, bufferInviteLength;
            string invite = String.Empty;

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];

                invite += $"{file.Name}{_inviteSeparator}{file.Length}";
                if (i < files.Length - 1) invite += FileSeparator;
            }

            bufferInvite = Encoding.UTF8.GetBytes(invite.Trim());
            if (encrypted) bufferInvite = _encryptionSymmetrical!.Encrypt(bufferInvite);
            bufferInviteLength = Encoding.UTF8.GetBytes(bufferInvite.Length.ToString());

            // send invite length
            await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(bufferInviteLength) : bufferInviteLength, _cancellationToken);

            // ack
            await YNReceiveAsync(encrypted);

            // send invite
            await _netStream.WriteAsync(bufferInvite, _cancellationToken);

            return await YNReceiveAsync(encrypted);
        }

        protected async Task<int> SendPortAsync(bool encrypted)
        {
            Random random = new();
            byte[] bufferPort;
            int port;

            do
            {
                do
                {
                    port = random.Next(49152, 65536);
                }
                while (!IsPortAvailable(_ipLocal!, port));

                bufferPort = Encoding.UTF8.GetBytes(port.ToString());

                await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(bufferPort) : bufferPort, _cancellationToken);
            }
            while (!await YNReceiveAsync(encrypted));

            return port;
        }

        protected async Task<int> ReceivePortAsync(bool encrypted)
        {
            int port;
            bool check;
            var portBytesLength = Encoding.UTF8.GetBytes(_initialPort.ToString()).Length;
            var buffer = new byte[encrypted ? portBytesLength + _encryptionDataSize : portBytesLength];

            do
            {
                await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

                port = int.Parse(Encoding.UTF8.GetString(encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer));

                check = IsPortAvailable(_ipLocal!, port);

                await YNSendAsync(check, encrypted);
            }
            while (!check);

            return port;
        }

        protected async Task SendFilesAsync(FileInfo[] files, bool encrypted)
        {
            for (int i = 0; i < files.Length; i++)
            {
                using (FileStream fileStream = new(files[i].FullName, FileMode.Open))
                {
                    for (int j = 0; j < files[i].Length;)
                    {
                        byte[] buffer = new byte[Math.Min(_bufferSize, files[i].Length - j)];

                        j += await fileStream.ReadAsync(buffer, _cancellationToken);
                        await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(buffer) : buffer, _cancellationToken);
                        OnFilePartTransported(files.Length, i + 1, CalculatePercentage(files[i].Length, j), SendReceive.Send);
                    }
                }
            }
        }

        protected async Task YNSendAsync(bool encrypted, bool yn)
        {
            byte[] responseBuffer = yn ? _y : _n;

            await _netStream!.WriteAsync(encrypted ? _encryptionSymmetrical?.Encrypt(responseBuffer) : responseBuffer, _cancellationToken);
        }

        protected async Task YNSendAsync(bool encrypted) => await YNSendAsync(true, encrypted);

        protected async Task<bool> YNReceiveAsync(bool encrypted)
        {
            var buffer = new byte[encrypted ? _y.Length + _encryptionDataSize : _y.Length];

            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

            return (encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer).SequenceEqual(_y);
        }

        protected async Task<T> ReceiveInviteAsync<T>(bool encrypted)
        {
            if (typeof(T) != typeof(string[]) && typeof(T) != typeof(Dictionary<string, long>)) throw new NotImplementedException();

            int read;
            byte inviteLength;
            string[] filesSplit;
            byte[] buffer = new byte[_bufferSize];
            Dictionary<string, long> filesAndSizes;

            // receive invite length
            read = await _netStream!.ReadAsync(buffer, _cancellationToken);

            // ack
            await YNSendAsync(encrypted);

            buffer = buffer[0..read];
            if (encrypted) buffer = _encryptionSymmetrical!.Decrypt(buffer);
            if (!byte.TryParse(Encoding.UTF8.GetString(buffer), out inviteLength)) throw new FormatException(InviteErrorMessage);

            // receive invite
            await _netStream!.ReadExactlyAsync(buffer = new byte[inviteLength], _cancellationToken);

            filesSplit = Encoding.UTF8.GetString(encrypted ? _encryptionSymmetrical!.Decrypt(buffer) : buffer).Split(FileSeparator);

            if (typeof(T) == typeof(string[])) return (T)(object)filesSplit;

            filesAndSizes = [];
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                filesAndSizes[file.Substring(0, index)] = long.Parse(file.Substring(index + 1));
            }

            return (T)(object)filesAndSizes;
        }

        protected async Task<string[]> ReceiveFilesAsync(Dictionary<string, long> filesAndSizes, string dictionaryPath, bool encrypted)
        {
            List<string> savedFiles = new();

            try
            {
                for (int i = 1; i <= filesAndSizes.Count; i++)
                {
                    var fileAndSize = filesAndSizes.ElementAt(i - 1);
                    var dotIndex = fileAndSize.Key.LastIndexOf('.');
                    string fileName = fileAndSize.Key.Substring(0, dotIndex), fileExt = fileAndSize.Key.Substring(dotIndex + 1), file = $"{fileName}.{fileExt}", path = $"{dictionaryPath}\\{file}";

                    for (int j = 0; File.Exists(path); j++)
                    {
                        file = $"{fileName} ({j}).{fileExt}";
                        path = $"{dictionaryPath}\\{file}";
                    }

                    using (FileStream fileStream = new(path, FileMode.Create))
                    {
                        var totalBytesRead = 0;

                        while (totalBytesRead < fileAndSize.Value)
                        {
                            var bufferSize = Math.Min(_bufferSize, fileAndSize.Value - totalBytesRead);
                            var buffer = new byte[encrypted ? bufferSize + _encryptionDataSize : bufferSize];

                            await _netStream!.ReadExactlyAsync(buffer, _cancellationToken);

                            totalBytesRead += encrypted ? buffer.Length - _encryptionDataSize : buffer.Length;

                            OnFilePartTransported(filesAndSizes.Count, i, CalculatePercentage(fileAndSize.Value, totalBytesRead), SendReceive.Receive);

                            await fileStream.WriteAsync(encrypted ? _encryptionSymmetrical?.Decrypt(buffer) : buffer, _cancellationToken);
                        }
                    }

                    savedFiles.Add(file);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Receiving file(s) failed.", ex);
            }

            return savedFiles.ToArray();
        }

        protected async Task<TcpClient> ReceiveTcpClientAsync(IPAddress ip, int port)
        {
            using (TcpListener listener = new(ip, port))
            {
                listener.Start();

                return await listener.AcceptTcpClientAsync(_cancellationToken);
            }
        }

        protected async Task<TcpClient> ConnectAsync(int port, bool connectingToServer)
        {
            TcpClient client = new();

            try
            {
                bool connected;

                client.Client.Bind(new IPEndPoint(_ipLocal!, 0));

                using (var timer = connectingToServer ? Task.Run(async () => await Task.Delay(10000)) : null)
                {
                    do
                    {
                        try
                        {
                            await client.ConnectAsync(_ipRemote!, port, _cancellationToken);

                            connected = client.Connected;
                        }
                        catch
                        {
                            connected = false;
                        }
                    }
                    while (!connected && (!timer?.IsCompleted ?? false));
                }
            }
            catch (Exception ex)
            {
                client.Dispose();
                throw new ConnectionFailedException("Could not connect.", ex);
            }

            return client;
        }
    }
}
