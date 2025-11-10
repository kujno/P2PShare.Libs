using System.Net.Sockets;

namespace P2PShare.Libs
{
    class ClientHandling
    {
        public static NetworkStream[] GetStreamsFromTcpClients(TcpClient[] clients)
        {
            NetworkStream[] streams = new NetworkStream[clients.Length];

            for (int i = 0; i < streams.Length; i++)
            {
                streams[i] = clients[i].GetStream();
            }

            return streams;
        }
    }
}
