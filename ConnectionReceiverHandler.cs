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
        private Queue<KeyValuePair<string, long>>? _filesAndSizes;

        private readonly IPAddress _localIP;

        public ConnectionReceiverHandler(IPAddress localIP) => _localIP = localIP;

        public async Task<Queue<KeyValuePair<string, long>>> ReceiveInviteAsync()
        {
            int modulusLength, exponentLength, read;
            var files = String.Empty;
            string[] filesSplit;

            _encryptionSymmetrical = new();

            try
            {
                byte[] rsaKey = new byte[EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out exponentLength)], modulus = new byte[modulusLength], exponent = new byte[exponentLength];

                await ReceiveTcpClientAsync(_localIP, (byte)_initialPort);

                _netStream = _client?.GetStream();

                await _netStream!.ReadExactlyAsync(rsaKey, _cancellationTokenSource.Token);

                Array.Copy(rsaKey, 0, modulus, 0, modulusLength);
                Array.Copy(rsaKey, modulusLength, exponent, 0, exponentLength);

                _encryptor = new(modulus, exponent);

                await _netStream.WriteAsync(_encryptor.Encrypt(_encryptionSymmetrical.Key), _cancellationTokenSource.Token);

                _filesAndSizes = [];

                do
                {
                    byte[] buffer = new byte[_inviteBufferSize];

                    read = await _netStream!.ReadAsync(buffer, _cancellationTokenSource.Token);

                    if (read > 0) files += Encoding.UTF8.GetString(_encryptionSymmetrical.Decrypt(buffer));
                }
                while (read > 0);
            }
            catch (OperationCanceledException)
            {
                Dispose();

                throw new OperationCanceledException();
            }
            catch
            {
                Dispose();

                throw new Exception("Receiving invite failed.");
            }

            filesSplit = files.Split();
            foreach (var file in filesSplit)
            {
                var index = file.IndexOf(_inviteSeparator);

                _filesAndSizes.Enqueue(new(file.Substring(0, index), long.Parse(file.Substring(index + 1))));
            }

            return _filesAndSizes;
        }

        public async Task<List<string>> AcceptFilesAsync(string dictionaryPath)
        {
            List<string> savedFiles = new();

            try
            {
                bool check;
                byte amountOfFiles = (byte)_filesAndSizes!.Count, port;
                byte[] buffer = new byte[_initialPort.ToString().Length + _encryptionDataSize];

                await _netStream!.WriteAsync(_y, _cancellationTokenSource.Token);

                // receive port number
                do
                {
                    await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                    port = byte.Parse(Encoding.UTF8.GetString(_encryptionSymmetrical!.Decrypt(buffer)));

                    check = IsPortAvailable(_localIP, port);

                    if (!check) await _netStream.WriteAsync(_encryptor?.Encrypt(_n), _cancellationTokenSource.Token);
                    else await _netStream.WriteAsync(_encryptor?.Encrypt(_y), _cancellationTokenSource.Token);
                }
                while (!check);

                DisposeClient();

                await ReceiveTcpClientAsync(_localIP, port);
                _netStream = _client?.GetStream();

                for (int i = 1; _filesAndSizes!.Count > 0; i++)
                {
                    var fileAndSize = _filesAndSizes.Dequeue();
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
                            buffer = new byte[Math.Min(_fileTransportBufferSize, fileAndSize.Value - totalBytesRead) + _encryptionDataSize];

                            await _netStream!.ReadExactlyAsync(buffer, _cancellationTokenSource.Token);

                            totalBytesRead += buffer.Length - _encryptionDataSize;

                            OnFilePartTransported(amountOfFiles, (byte)i, (byte)((100 / fileAndSize.Value) * totalBytesRead), SendReceiveEnum.Receive);

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
            finally
            {
                Dispose();
            }

            return savedFiles;
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
                client?.Dispose();

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
    }
}
