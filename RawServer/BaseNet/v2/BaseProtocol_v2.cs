using Pool;
using RawServer.Common;
using System;
using System.Text;

namespace RawServer.BaseNet
{
	public sealed partial class BaseProtocol_v2 : OnConnection, IPoolSlotHolder<BaseProtocol>
	{
		private enum MessageTypes : byte
		{
			Welcome = 0b00000001,
			CryptInfo = 0b00000010,
			Data = 0b00010001,
			DataFragment = 0b00011001,
			Ping = 0b10000000,
			Disconnect = 0b11111111
		}

		private BuffConverter buffReader = new BuffConverter();
		private BuffConverter buffWriter = new BuffConverter();

		private Guid ConnectionID { get; set; }
		private Version ProtoVersion { get; set; }

		private bool IsStartingDisconnect { get; set; }
		private bool IsFragmentPack { get; set; }


		/// <summary>
		/// Клиент подключен и прошел валидацию
		/// </summary>
		public bool IsConnected { get; private set; }
		public ulong TotalBytesTransmitted { get; private set; }
		public ulong TotalBytesReceived { get; private set; }
		public ulong PacketNumber { get; private set; }


		public BaseProtocol_v2()
		{
			//IsInitConnection = false;

			base.ClientReceiveCommand += BaseConnection_ClientReceiveCommand;
			base.ClientClosed += BaseProtocol_ClientDisconnected;

			InitTimeouts();

			ProtoVersion = new Version(0, 0, 0, 2);
		}

		private void BaseConnection_ClientReceiveCommand(ClientEventArgs fcCommand)
		{
			switch (fcCommand.Command)
			{
				case ClientActions.Connected:
					TimeoutsWatcher_Connected();

					ConnectionID = Guid.NewGuid();

					SendWelcome();
					break;
				case ClientActions.Receive:
					TimeoutsWatcher_Receive();

					TotalBytesReceived += (uint)fcCommand.ReceiveBufferLength;

					if (IsConnected)
					{
						if (HandleMessage(fcCommand.ReceiveBuffer, fcCommand.ReceiveBufferLength) == false)
							DisconnectByClient();
					}
					else
					{
						if (HandleWelcome(fcCommand.ReceiveBuffer, fcCommand.ReceiveBufferLength) == false)
							DisconnectByClient();
					}

					break;
				case ClientActions.SendCompleted:
					TimeoutsWatcher_SendCompleted();

					TotalBytesTransmitted += (uint)fcCommand.ReceiveBufferLength;
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

		public bool HandleMessage(byte[] buffer, int length)
		{
			buffReader.SetBuffer(false, buffer, length);

			return true;
		}

		public void DisconnectByClient()
		{
			TimeoutsWatcher_Disconnect();

			if (IsStartingDisconnect)
				return;

			IsStartingDisconnect = true;
			TimeoutsWatcher_Disconnecting();
			//base.Send(AssemblyRawPack(LowCommandsRequest.Disconnect));
		}

		private void BaseProtocol_ClientDisconnected(OnConnection clientConnection)
		{
			TimeoutsWatcher_Disconnected();
		}

		public override void ServerActions(ToServerCommand Command)
		{
			if (Command == null) return;
			if (Command.Client != null && ((BaseProtocol_v2)Command.Client).ConnectionID != this.ConnectionID)
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

		private void WriteHeader(MessageTypes msgType)
		{
			buffWriter.WriteUInt8((byte)msgType);
			buffWriter.WriteUInt64(PacketNumber);
		}

		private void WriteCRC()
		{
			buffWriter.WriteUInt8(CRC8.ComputeChecksum(0, (int)buffWriter.IncomingBytesUnread, buffWriter.ReadBytes(-1)));
		}

		#region Pool
		PoolSlot<BaseProtocol> IPoolSlotHolder<BaseProtocol>.PoolSlot { get; set; }

		/// <summary>
		/// Осуществляет очистку объекта, перед возвратом в пулл.
		/// </summary>
		public override void CleanUp()
		{
			IsConnected = false;
			IsCrypting = false;
			IsStartingDisconnect = false;

			TotalBytesTransmitted = 0;
			TotalBytesReceived = 0;
			PacketNumber = 0;

			ConnectionID = Guid.Empty;

			TimeoutsWatcher_Stop();
			ResetTimeouts();

			buffReader.Clear();
			buffWriter.Clear();

			base.CleanUp();
		}
		#endregion
	}
}
