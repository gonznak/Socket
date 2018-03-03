using Pool;
using RawServer.Common;
using System;
using System.Text;

namespace RawServer
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

		/// <summary>
		/// Клиент подключен и прошел валидацию
		/// </summary>
		public bool IsConnected { get; private set; }
		public ulong TotalBytesTransmitted { get; private set; }
		public ulong TotalBytesReceived { get; private set; }

		private sbyte _pingInterval = 5;
		public sbyte PingInterval
		{
			get { return _pingInterval; }
			private set
			{
				if (value > PingTimeOut - 3)
					throw new ArgumentException("New value PingInterval > PingTimeOut - 3");
				else
					_pingInterval = value < 5 ? (sbyte)5 : value;
			}
		}

		private sbyte _pingTimeOut = 8;
		public sbyte PingTimeOut
		{
			get { return _pingTimeOut; }
			private set
			{
				if (value < PingInterval + 3)
					throw new ArgumentException("New value PingTimeOut < PingInterval + 3");
				else
					_pingTimeOut = value < 8 ? (sbyte)8 : value;
			}
		}

		private sbyte _receiveTimeOut = 3;
		public sbyte ReceiveTimeOut
		{
			get => _receiveTimeOut;
			private set => _pingTimeOut = value < 3 ? (sbyte)3 : value;
		}

		private sbyte _disconnectTimeOut = 5;
		public sbyte DisconnectTimeOut
		{
			get => _disconnectTimeOut;
			private set => _pingTimeOut = value < 5 ? (sbyte)5 : value;
		}

		public ulong PacketNumber { get; private set; }



		private BuffConverter buffReader = new BuffConverter();
		private BuffConverter buffWriter = new BuffConverter();

		private TimeoutWatcher pingTimer;
		private TimeoutWatcher receiveTimer;
		private TimeoutWatcher disconnectTimer;

		private bool IsInitConnection { get; set; }
		private bool IsStartingDisconnect { get; set; }
		private Guid ConnectionID { get; set; }
		private Version ProtoVersion { get; set; }
		private bool IsFragmentPack { get; set; }

		public BaseProtocol_v2()
		{
			IsInitConnection = false;

			pingTimer = new TimeoutWatcher();
			receiveTimer = new TimeoutWatcher();
			disconnectTimer = new TimeoutWatcher();

			base.ClientReceiveCommand += BaseConnection_ClientReceiveCommand;
			base.ClientClosed += BaseProtocol_ClientDisconnected;

			pingTimer.TimeLapse += new OnTimerLapse(Ping_TimeLapse);
			receiveTimer.TimeLapse += new OnTimerLapse(Receive_TimeLapse);
			disconnectTimer.TimeLapse += new OnTimerLapse(Disconnect_TimeLapse);

			ProtoVersion = new Version(0, 0, 0, 2);
		}

		private void BaseConnection_ClientReceiveCommand(FromClientCommand fcCommand)
		{
			switch (fcCommand.Command)
			{
				case ClientActions.Connected:
					receiveTimer.Start(ReceiveTimeOut, true);
					pingTimer.Start(PingInterval, true);

					ConnectionID = Guid.NewGuid();

					SendWelcome();
					break;
				case ClientActions.Receive:
					receiveTimer.Pause();
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
					receiveTimer.Reset();
					TotalBytesTransmitted += (uint)fcCommand.ReceiveBufferLength;
					PacketNumber++;

					base.StartReceive(0);
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
			pingTimer.Pause();
			receiveTimer.Pause();

			if (IsStartingDisconnect)
				return;

			IsStartingDisconnect = true;
			disconnectTimer.Start(DisconnectTimeOut, false);
			//base.Send(AssemblyRawPack(LowCommandsRequest.Disconnect));
		}

		private void Ping_TimeLapse()
		{
			//base.Send(AssemblyRawPack(LowCommandsRequest.Ping));
		}

		private void Receive_TimeLapse()
		{
			receiveTimer.Pause();
			DisconnectByClient();
		}

		private void Disconnect_TimeLapse()
		{
			disconnectTimer.Pause();
			base.Close();
		}

		private void BaseProtocol_ClientDisconnected(OnConnection clientConnection)
		{
			pingTimer.Pause();
			receiveTimer.Pause();
			disconnectTimer.Pause();
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

			pingTimer.Stop();
			receiveTimer.Stop();
			disconnectTimer.Stop();

			buffReader.Clear();
			buffWriter.Clear();

			TotalBytesTransmitted = 0;
			TotalBytesReceived = 0;

			_pingInterval = 5;
			_pingTimeOut = 8;
			_receiveTimeOut = 3;
			_disconnectTimeOut = 5;

			PacketNumber = 0;

			IsInitConnection = false;
			IsStartingDisconnect = false;
			ConnectionID = Guid.Empty;
			base.CleanUp();
		}
		#endregion
	}
}
