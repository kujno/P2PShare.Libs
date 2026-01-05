using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionTranscieverHandler : ConnectionHandler
    {
        public static event EventHandler<IPAddress>? Contacted;

        public ConnectionTranscieverHandler(CancellationToken cancellationToken) : base(cancellationToken)
        {
        }

        private void OnContacted(IPAddress ip) => Contacted?.Invoke(this, ip);

        public async Task SendAsync(IPAddress ipRemote, IPAddress ipLocal, FileInfo[] files, bool encrypted)
        {
            try
            {
                if (!files.All(x => x.Exists)) throw new FileNotFoundException("One or more files to send were not found.");

                EncryptionSymmetrical? encryption = null;
                DecryptorAsymmetrical? decryptor = null;
                Random random = new();
                byte[] bufferSend;
                byte[] bufferAsymmetrical;
                int port, modulusLength, publicKeyLength = EncryptionAsymmetrical.GetPublicKeyLength(out modulusLength, out _);
                string invite = String.Empty;

                if (encrypted) decryptor = new();

                bufferAsymmetrical = new byte[encrypted ? modulusLength : _y.Length];

                OnContacted(ipRemote);
                _client = await ConnectAsync(ipRemote, ipLocal, _initialPort);
                _netStream = _client!.GetStream();

                if (encrypted)
                {
                    // send encryption status
                    await _netStream.WriteAsync(_y, _cancellationToken);

                    // send public key
                    await _netStream.WriteAsync(decryptor!.PublicKey.Modulus!.Concat(decryptor.PublicKey.Exponent!).ToArray(), 0, publicKeyLength, _cancellationToken);

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

                // send invite
                await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(bufferSend) : bufferSend, _cancellationToken);
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

        private async Task<TcpClient> ConnectAsync(IPAddress ipRemote, IPAddress ipLocal, int port)
        {
            TcpClient client = new();

            try
            {
                client.Client.Bind(new IPEndPoint(ipLocal, 0));

                while (!client.Connected)
                {
                    await client.ConnectAsync(ipRemote, port, _cancellationToken);
                }
            }
            catch
            {
                client.Dispose();
                throw;
            }

            return client;
        }
    }
}
