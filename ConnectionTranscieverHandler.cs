using P2PShare.Libs.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PShare.Libs
{
    public class ConnectionTranscieverHandler : ConnectionHandler
    {
        public async Task SendAsync(IPAddress ipRemote, IPAddress ipLocal, FileInfo[] files, bool encrypted)
        {
            try
            {
                if (!files.All(x => x.Exists)) throw new FileNotFoundException();

                EncryptionSymmetrical? encryption = null;
                DecryptorAsymmetrical? decryptor = null;
                Random random = new();
                byte[] bufferSend;
                byte[] bufferAsymmetrical;
                int port;
                string invite = String.Empty;

                if (encrypted) decryptor = new();

                bufferAsymmetrical = new byte[encrypted ? decryptor?.PublicKey.Modulus?.Length ?? throw new ArgumentNullException("Encryption failed.") : _y.Length];

                await ConnectAsync(ipRemote, ipLocal, _initialPort);
                _netStream = _client!.GetStream();

                if (encrypted)
                {
                    // send encryption status
                    await _netStream.WriteAsync(_y, _cancellationTokenSource.Token);

                    // send public key
                    await _netStream.WriteAsync(decryptor!.PublicKey.Modulus!.Concat(decryptor.PublicKey.Exponent!).ToArray(), 0, EncryptionAsymmetrical.GetPublicKeyLength(out _, out _), _cancellationTokenSource.Token);

                    // receive aes key
                    await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationTokenSource.Token);

                    encryption = new(decryptor.Decrypt(bufferAsymmetrical!));
                }
                // send encryption status
                else await _netStream.WriteAsync(_n, _cancellationTokenSource.Token);

                for (int i = 0; i < files.Length; i++)
                {
                    var file = files[i];

                    invite += $"{file.Name}{_inviteSeparator}{file.Length}";
                    if (i < files.Length - 1) invite += FileSeparator;
                }

                bufferSend = Encoding.UTF8.GetBytes(invite.Trim());

                // send invite
                await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(bufferSend) : bufferSend, _cancellationTokenSource.Token);
                await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationTokenSource.Token);

                if ((encrypted ? decryptor?.Decrypt(bufferAsymmetrical) : bufferAsymmetrical) != _y) throw new FileTransportDeniedException();

                do
                {
                    do
                    {
                        port = random.Next(49152, 65536);
                    }
                    while (!IsPortAvailable(ipLocal, port));

                    bufferSend = Encoding.UTF8.GetBytes(port.ToString());

                    await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(bufferSend) : bufferSend, _cancellationTokenSource.Token);
                    await _netStream.ReadExactlyAsync(bufferAsymmetrical, _cancellationTokenSource.Token);
                }
                while ((encrypted ? decryptor?.Decrypt(bufferAsymmetrical) : bufferAsymmetrical) != _y);

                DisposeClient();

                await ConnectAsync(ipRemote, ipLocal, port);

                for (int i = 0; i < files.Length; i++)
                {
                    int bytesRead = 0;

                    using (FileStream fileStream = new(files[i].FullName, FileMode.Open))
                    {
                        byte[] buffer = new byte[Math.Min(_fileTransportBufferSize, files[i].Length - bytesRead)];

                        bytesRead += await fileStream.ReadAsync(buffer, _cancellationTokenSource.Token);
                        await _netStream.WriteAsync(encrypted ? encryption?.Encrypt(buffer) : buffer, _cancellationTokenSource.Token);
                        OnFilePartTransported(files.Length, i, CalculatePercentage(files[i].Length, bytesRead), SendReceive.Send);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException("One or more files to send were not found.");
            }
            catch (FileTransportDeniedException)
            {
                throw new FileTransportDeniedException("File transport was denied.");
            }
            catch
            {
                throw new Exception("Sending file(s) failed.");
            }
        }

        private async Task ConnectAsync(IPAddress ipRemote, IPAddress ipLocal, int port)
        {
            TcpClient client = new();

            try
            {
                client.Client.Bind(new IPEndPoint(ipLocal, port));

                while (!client.Connected)
                {
                    await client.ConnectAsync(ipRemote, port, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            _client = client;
        }
    }
}
