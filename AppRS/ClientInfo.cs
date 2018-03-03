using AppRS.RS;
using Pool;
using RawServer;
using System;

namespace AppRS
{
	public class ClientInfo : OnConnection, IPoolSlotHolder<ClientInfo>
	{
		Protocol proto = new Protocol();

		public bool IsConnected { get { return proto.IsValid; } }

		public Version GetVersion { get { return new Version(""); } }

		public Guid ConnectionID { get; private set; }

		public int GetSessionID { get { return 0; } }


		public ClientInfo()
		{
			Init_Network();
		}

		#region Network
		void Init_Network()
		{
			base.ClientReceiveCommand += ClientInfo_ClientReceiveCommand;
			proto.SendMessage += Proto_SendMessage;
			proto.Disconnected += Proto_Disconnect;
		}

		void ClientInfo_ClientReceiveCommand(RawServer.Common.FromClientCommand Command)
		{
			switch (Command.Command)
			{
				case RawServer.Common.ClientActions.Connected:
					ConnectionID = Guid.NewGuid();
					proto.Connected();
					break;
				case RawServer.Common.ClientActions.Receive:
					proto.HandleMessage(Command.ReceiveBuffer, Command.ReceiveBufferLength);
					break;
				case RawServer.Common.ClientActions.SendCompleted:
					proto.DoSended();
					this.StartReceive();
					break;

				case RawServer.Common.ClientActions.NoConnections:
					break;
				case RawServer.Common.ClientActions.UnknownSend:
					break;
				case RawServer.Common.ClientActions.ZeroBuffer:
					break;

				case RawServer.Common.ClientActions.Aborted:
					proto.Disconnect();
					break;
				case RawServer.Common.ClientActions.Shutdown:
					proto.Disconnect();
					break;
				case RawServer.Common.ClientActions.Disconnected:
					proto.Disconnect();
					break;

				default:
					break;
			}
		}

		void StartReceive()
		{
			base.StartReceive(0);
		}

		private void Proto_SendMessage(byte[] message)
		{
			base.Send(message);
		}

		private void Proto_Disconnect()
		{
			base.Disconnect();
		}

		public override void ServerActions(RawServer.Common.ToServerCommand Command)
		{
			if (Command.Client == null)
			{
				switch (Command.Action)
				{
					case RawServer.Common.ServerCommands.Disconnect:
						Proto_Disconnect();
						break;
					default:
						break;
				}
			}
			else if (((ClientInfo)Command.Client).ConnectionID == this.ConnectionID)
			{
				switch (Command.Action)
				{
					case RawServer.Common.ServerCommands.Disconnect:
						Proto_Disconnect();
						break;
					default:
						break;
				}
			}
		}
		#endregion

		public void SendFile(byte[] dataFile)
		{
			proto.DataPacket(DataAction.Update, dataFile);
		}

		#region Pool
		PoolSlot<ClientInfo> IPoolSlotHolder<ClientInfo>.PoolSlot { get; set; }

		/// <summary>
		/// Осуществляет очистку объекта, перед возвратом в пулл.
		/// </summary>
		public override void CleanUp()
		{
			ConnectionID = Guid.Empty;
			base.CleanUp();
		}
		#endregion
	}
}
