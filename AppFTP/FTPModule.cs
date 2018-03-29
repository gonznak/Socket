using Pool;
using RawServer;
using RawServer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFTP
{
	public sealed partial class FTPModule : OnConnection, IPoolSlotHolder<FTPModule>
	{
		private BuffConverter buffReader = new BuffConverter();
		private BuffConverter buffWriter = new BuffConverter();

		private Guid ConnectionID { get; set; }

		private bool IsStartingDisconnect { get; set; }


		/// <summary>
		/// Клиент подключен и прошел валидацию
		/// </summary>
		public ulong TotalBytesTransmitted { get; private set; }
		public ulong TotalBytesReceived { get; private set; }
		public ulong PacketNumber { get; private set; }

		public FTPModule()
		{
			base.ClientReceiveCommand += FTPModule_ClientReceiveCommand;
			base.ClientClosed += FTPModule_ClientClosed;

			InitTimeouts();
			FTP_Init();
		}

		public void AcceptClient()
		{
			this.StartConnection();
			FTP_Welcome();
		}

		private void FTPModule_ClientReceiveCommand(ClientEventArgs clientCommand)
		{
			switch (clientCommand.Command)
			{
				case ClientActions.Connecting:
					TimeoutsWatcher_Connected();

					ConnectionID = Guid.NewGuid();
					break;
				case ClientActions.Receive:
					TimeoutsWatcher_Receive();

					TotalBytesReceived += (uint)clientCommand.ReceiveBufferLength;

					if (!FTP_UserCommands(clientCommand.ReceiveBuffer, clientCommand.ReceiveBufferLength))
						DisconnectByClient();

					break;
				case ClientActions.SendCompleted:
					TimeoutsWatcher_SendCompleted();
					FTP_Sended();

					TotalBytesTransmitted += (uint)clientCommand.ReceiveBufferLength;
					PacketNumber++;

					base.RunReceive(0);
					break;

				case ClientActions.NoConnections:
					break;
				case ClientActions.UnknownSend:
					break;
				case ClientActions.ZeroBuffer:
					break;

				case ClientActions.Aborted:
					base.Close();
					break;
				case ClientActions.Shutdown:
					base.Close();
					break;
				case ClientActions.Disconnected:
					base.Close();
					break;

				default:
					break;
			}
		}

		public void DisconnectByClient()
		{
			TimeoutsWatcher_Disconnect();

			if (IsStartingDisconnect)
				return;

			IsStartingDisconnect = true;
			TimeoutsWatcher_Disconnecting();
		}

		private void FTPModule_ClientClosed(OnConnection clientConnection)
		{
			TimeoutsWatcher_Disconnected();
		}

		public override void ServerActions(ToServerCommand Command)
		{
			if (Command == null) return;
			if (Command.Client != null && ((FTPModule)Command.Client).ConnectionID != this.ConnectionID)
				return;

			switch (Command.Action)
			{
				case ServerCommands.Disconnect:
					DisconnectByClient();
					break;

				default:
					break;
			}
		}

		#region Pool
		PoolSlot<FTPModule> IPoolSlotHolder<FTPModule>.PoolSlot { get; set; }

		/// <summary>
		/// Осуществляет очистку объекта, перед возвратом в пулл.
		/// </summary>
		public override void CleanUp()
		{
			IsStartingDisconnect = false;

			TotalBytesTransmitted = 0;
			TotalBytesReceived = 0;
			PacketNumber = 0;

			ConnectionID = Guid.Empty;

			TimeoutsWatcher_Stop();
			ResetTimeouts();
			FTP_Init();

			buffReader.Clear();
			buffWriter.Clear();

			base.CleanUp();
		}
		#endregion
	}
}
