using RawServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppFTP
{
	class Program
	{
		static Server<FTPModule> serv = null;
		static SConfiguration sConfig = new SConfiguration();

		public static List<FTPModule> Clients { get; private set; }

		static void Main(string[] args)
		{
			Program.Clients = new List<FTPModule>();

#if DEBUG
			sConfig.Port = 2121;
#else
			sConfig.Port = 21;
#endif

			serv = new Server<FTPModule>(sConfig);
			serv.ClientConnected += Serv_ClientConnected;

			serv.Start();

			Thread.Sleep(-1);
		}

		private static void Serv_ClientConnected(FTPModule client)
		{
			client.ClientClosed += AcceptClient_ClientDisconnected;
			Program.Clients.Add(client);

			client.AcceptClient();
		}

		private static void AcceptClient_ClientDisconnected(OnConnection clientConnection)
		{
			FTPModule ci = (FTPModule)clientConnection;
			ci.ClientClosed -= AcceptClient_ClientDisconnected;
			Program.Clients.Remove(ci);
		}
	}
}
