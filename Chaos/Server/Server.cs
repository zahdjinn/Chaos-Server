// ****************************************************************************
// This file belongs to the Chaos-Server project.
// 
// This project is free and open-source, provided that any alterations or
// modifications to any portions of this project adhere to the
// Affero General Public License (Version 3).
// 
// A copy of the AGPLv3 can be found in the project directory.
// You may also find a copy at <https://www.gnu.org/licenses/agpl-3.0.html>
// ****************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chaos
{
    /// <summary>
    /// Represents the game server, the network interface of the game.
    /// </summary>
    internal sealed class Server
    {
        private static readonly object SyncWrite = new object();
        private readonly object Sync = new object();
        private readonly Dictionary<Socket, Client> Clients;
        private readonly Task OutboundController;

        internal static int NextID = 1;
        internal static bool Running { get; private set; }
        internal static StreamWriter LogWriter { get; private set; }
        internal static DateTime Today { get; private set; }

        internal IPEndPoint ServerEndPoint { get; }
        internal Socket LobbySocket { get; }
        internal Socket LoginSocket { get; }
        internal Socket WorldSocket { get; }
        internal byte[] Table { get; }
        internal uint TableCheckSum { get; }
        internal byte[] LoginMessage { get; }
        internal uint LoginMessageCheckSum { get; }
        internal DataBase DataBase { get; }
        internal List<Redirect> Redirects { get; }
        internal List<MetaFile> MetaFiles { get; }

        internal static GameTime ServerTime => GameTime.Now;
        internal static LightLevel LightLevel => ServerTime.TimeOfDay;
        internal List<Client> WorldClients
        {
            get
            {
                lock (Sync)
                    return Clients.Values.Where(client => client.ServerType == ServerType.World && client.User != null).ToList();
            }
        }

        internal bool doWalking = false;

        internal Server(IPAddress ip)
        {
            if (!Directory.Exists(Paths.LogFiles))
                Directory.CreateDirectory(Paths.LogFiles);

            //inialize logger
            LogWriter = new StreamWriter(new FileStream($@"{Paths.LogFiles}{DateTime.UtcNow.ToString("MMM dd yyyy")}.log", FileMode.OpenOrCreate))
            {
                AutoFlush = true
            };
            Today = DateTime.MinValue;
            WriteLogAsync("Initializing server...");

            //initialize server
            ServerEndPoint = new IPEndPoint(ip, CONSTANTS.LOBBY_PORT);
            Clients = new Dictionary<Socket, Client>();
            DataBase = new DataBase(this);
            Redirects = new List<Redirect>();
            MetaFiles = new List<MetaFile>();

            byte[] notif = Encoding.GetEncoding(949).GetBytes($@"{{={(char)MessageColor.Orange}Welcome to the Cogadh project!");
            LoginMessageCheckSum = Crypto.Generate32(notif);

            using (MemoryStream compressor = ZLIB.Compress(notif))
                LoginMessage = compressor.ToArray();

            var tableStream = new MemoryStream();
            using (var writer = new BinaryWriter(tableStream))
            {
                writer.Write((byte)1);
                writer.Write((byte)0);
                writer.Write(ServerEndPoint.Address.GetAddressBytes());
                writer.Write((byte)(ServerEndPoint.Port / 256));
                writer.Write((byte)(ServerEndPoint.Port % 256));
                writer.Write(Encoding.GetEncoding(949).GetBytes("Chaos;Under Construction\0"));
                writer.Write(notif);
                writer.Flush();

                TableCheckSum = Crypto.Generate32(tableStream.ToArray());
                using (MemoryStream table = ZLIB.Compress(tableStream.ToArray()))
                    Table = table.ToArray();
            }

            foreach (string name in Directory.GetFiles(Paths.MetaFiles))
                MetaFiles.Add(MetaFile.Load(new FileInfo(name).Name));

            Running = true;
            Game.Intialize(this);

            //start the server
            LobbySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            LoginSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            WorldSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Task.Run(() =>
            {
                LobbySocket.Bind(new IPEndPoint(IPAddress.Any, CONSTANTS.LOBBY_PORT));
                LobbySocket.Listen(25);
                LobbySocket.BeginAccept(new AsyncCallback(EndAccept), LobbySocket);
            });

            Task.Run(() =>
            {
                LoginSocket.Bind(new IPEndPoint(IPAddress.Any, CONSTANTS.LOGIN_PORT));
            LoginSocket.Listen(25);
            LoginSocket.BeginAccept(new AsyncCallback(EndAccept), LoginSocket);
            });

            Task.Run(() =>
            {
            WorldSocket.Bind(new IPEndPoint(IPAddress.Any, CONSTANTS.WORLD_PORT));
            WorldSocket.Listen(25);
            WorldSocket.BeginAccept(new AsyncCallback(EndAccept), WorldSocket);
            });

            OutboundController = Task.Run(() => FlushSendQueueAsync()); //Task.Run(FlushSendQueueAsync);
            WriteLogAsync($"Server is ready on {ServerEndPoint.Address}");
        }

        ~Server()
        {
            Running = false;
            LogWriter.Dispose();
            LobbySocket.Dispose();
            LoginSocket.Dispose();
            WorldSocket.Dispose();
        }

        internal async Task FlushSendQueueAsync()
        {
            var rate = new RateController(100);

            while (Running)
            {
                lock (Sync)
                    foreach (KeyValuePair<Socket, Client> kvp in Clients.ToList())
                        kvp.Value.FlushSendQueue();

                await rate.ThrottleAsync();
            }
        }

        internal bool TryAddClient(Client client)
        {
            lock (Sync)
            {
                if (!Clients.ContainsKey(client.ClientSocket))
                {
                    Clients.Add(client.ClientSocket, client);
                    return true;
                }
            }
            return false;
        }

        internal bool TryRemoveClient(Client client)
        {
            lock (Sync)
            {
                if (Clients.ContainsKey(client.ClientSocket))
                {
                    Clients.Remove(client.ClientSocket);
                    return true;
                }
            }

            return false;
        }

        internal void EndAccept(IAsyncResult ar)
        {
            ServerType serverType = ServerType.Lobby;
            var serverSocket = (Socket)ar.AsyncState;
            Socket clientSocket = serverSocket.EndAccept(ar);
            serverSocket.BeginAccept(new AsyncCallback(EndAccept), serverSocket);

            if(clientSocket.LocalEndPoint is IPEndPoint ipEndPoint)
            {
                switch(ipEndPoint.Port)
                {
                    case CONSTANTS.LOBBY_PORT:
                        serverType = ServerType.Lobby;
                        break;
                    case CONSTANTS.LOGIN_PORT:
                        serverType = ServerType.Login;
                        break;
                    case CONSTANTS.WORLD_PORT:
                        serverType = ServerType.World;
                        break;
                }
            }

            if (clientSocket.Connected)
            {
                //create the user, and add them to the userlist
                var newClient = new Client(this, clientSocket)
                {
                    ServerType = serverType
                };

                WriteLogAsync($@"Incoming connection", newClient);
                newClient.Connect();
            }
        }

        internal static Task WriteLogAsync(string message, Client client = null) => Task.Run(() =>
        {
            lock (SyncWrite)
            {
                message = (client == null) ? $@"[{DateTime.UtcNow.ToString("HH:mm")}] Server: {message}" : $@"[{DateTime.UtcNow.ToString("HH:mm")}] {client.IpAddress}: {message}";

                Console.WriteLine(message);

                if (DateTime.UtcNow.Day != Today.Day)
                {
                    if (!File.Exists($@"{Paths.LogFiles}{DateTime.UtcNow.ToString("MMM dd yyyy")}.log"))
                    {
                        LogWriter.Dispose();
                        LogWriter = new StreamWriter(new FileStream($@"{Paths.LogFiles}{DateTime.UtcNow.ToString("MMM dd yyyy")}.log", FileMode.OpenOrCreate));
                    }
                    Today = DateTime.UtcNow;
                }

                LogWriter.Write($@"{message}{Environment.NewLine}");
            }
        });

        internal bool TryGetUser(string name, out User user)
        {
            user = WorldClients.FirstOrDefault(client => client.User.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.User;

            return user != null;
        }
        internal bool TryGetUser(int id, out User user)
        {
            user = WorldClients.FirstOrDefault(client => client.User.ID == id).User;

            return user != null;
        }

        internal static int GetPort(ServerType serverType)
        {
            switch(serverType)
            {
                case ServerType.Lobby:
                    return CONSTANTS.LOBBY_PORT;
                case ServerType.Login:
                    return CONSTANTS.LOGIN_PORT;
                case ServerType.World:
                    return CONSTANTS.WORLD_PORT;
            }

            return 0;
        }
    }
}
