using RawServer;
using System.Collections.Generic;

namespace AppRS
{
	public class ServModule
	{
		Server<BaseProtocol> serv = null;
		SConfiguration sConfig = new SConfiguration();

		public List<BaseProtocol> Clients { get; private set; }

		public ServModule()
		{
			this.Clients = new List<BaseProtocol>();

			sConfig.Port = 9520;

			serv = new Server<BaseProtocol>(sConfig);
			serv.ClientConnected += Serv_AcceptConnectionClient;

		}

		private void Serv_AcceptConnectionClient(BaseProtocol acceptClient)
		{
			acceptClient.ClientClosed += AcceptClient_ClientDisconnected;
			Clients.Add(acceptClient);
		}

		private void AcceptClient_ClientDisconnected(OnConnection clientConnection)
		{
			BaseProtocol ci = (BaseProtocol)clientConnection;
			ci.ClientClosed -= AcceptClient_ClientDisconnected;
			Clients.Remove(ci);
		}

		public void Start()
		{
			serv.Start();
		}

		public void Stop()
		{
			serv.Stop();
		}
	}
}
