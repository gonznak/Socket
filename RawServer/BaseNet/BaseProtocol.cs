using Pool;
using RawServer.Common;
using System;
using System.Text;

namespace RawServer
{
	public sealed class BaseProtocol : OnConnection, IPoolSlotHolder<BaseProtocol>
	{
		private enum LowCommandsRequest : byte
		{
			/// <summary>
			/// Пустая посылка, для поддержания соединения
			/// </summary>
			Ping = 0,            // ответ через System, в данных Ping
										/// <summary>
										/// Посылка содержит данные
										/// </summary>
			Data,                // ответ при ошибке через System, в данных DataBroken или ErrorCRC
										/// <summary>
										/// Запрос версии
										/// </summary>
			Version,             // ответ через System, в данных Version
										/// <summary>
										/// Как работает, сервис или просто
										/// </summary>
			RunType,             // ответ через System, в данных RunType
										/// <summary>
										/// Команда на отключение
										/// </summary>
			Disconnect           // Без ответа
		}

		private enum LowCommandsResponse : byte
		{
			/// <summary>
			/// Ответ на системные команды
			/// </summary>
			System = 0,
			/// <summary>
			/// Посылка содержит данные
			/// </summary>
			Data,
			/// <summary>
			/// Указывает, что принятый пакет пришел не полностью
			/// </summary>
			DataBroken,
			/// <summary>
			/// Ошибка контрольной суммы данных
			/// </summary>
			ErrorCRC,
			/// <summary>
			/// Не поддерживаемая команда
			/// </summary>
			NotSupported
		}

		private enum DataAction : byte
		{
			Update = 0,
			Shot,
			None = 255
		}

		public bool IsConnected { get; private set; }
		public ulong TotalBytesTransmitted { get; private set; }
		public ulong TotalBytesReceived { get; private set; }


		private BuffConverter buffReader = new BuffConverter();
		private BuffConverter buffWriter = new BuffConverter();

		private int pingTimeOut = 0;
		private int receiveTimeOut = 0;
		private int disconnectTimeOut = 0;

		private TimeoutWatcher pingTimer;
		private TimeoutWatcher receiveTimer;
		private TimeoutWatcher disconnectTimer;

		private bool IsInitConnection { get; set; }
		private bool IsStartingDisconnect { get; set; }
		private Guid ConnectionID { get; set; }
		private Version ProtoVersion { get; set; }

		public BaseProtocol()
		{
			IsInitConnection = false;

			pingTimeOut = 40;
			receiveTimeOut = 10;
			disconnectTimeOut = 5;

			pingTimer = new TimeoutWatcher();
			receiveTimer = new TimeoutWatcher();
			disconnectTimer = new TimeoutWatcher();

			base.ClientReceiveCommand += BaseConnection_ClientReceiveCommand;
			base.ClientClosed += BaseProtocol_ClientDisconnected;

			pingTimer.TimeLapse += new OnTimerLapse(Ping_TimeLapse);
			receiveTimer.TimeLapse += new OnTimerLapse(Receive_TimeLapse);
			disconnectTimer.TimeLapse += new OnTimerLapse(Disconnect_TimeLapse);

			ProtoVersion = new Version(0, 0, 0, 1);
		}

		private void BaseConnection_ClientReceiveCommand(FromClientCommand fcCommand)
		{
			switch (fcCommand.Command)
			{
				case ClientActions.Connected:
					receiveTimer.Start(receiveTimeOut, true);
					pingTimer.Start(pingTimeOut, true);

					IsConnected = true;
					ConnectionID = Guid.NewGuid();

					base.Send(AssemblyRawPack(LowCommandsRequest.Version));
					break;
				case ClientActions.Receive:
					receiveTimer.Pause();
					TotalBytesReceived += (uint)fcCommand.ReceiveBufferLength;

					if (HandleMessage(fcCommand.ReceiveBuffer, fcCommand.ReceiveBufferLength) == false)
						DisconnectByClient();
					break;
				case ClientActions.SendCompleted:
					receiveTimer.Reset();
					TotalBytesTransmitted += (uint)fcCommand.ReceiveBufferLength;

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

		public bool HandleMessage(byte[] packet, int length)
		{
			/*
				0-1	- data length
				2		- LowCommandsRequest
				3		- crc8 (0-2)
				N		- LowCommands
				D		- data
				N+D+1	- data crc8
				*/

			buffReader.SetBuffer(false, packet, length);

			if (buffReader.IncomingBytes < 4) return false;

			int fullLength = buffReader.ReadUInt16();
			if (fullLength != buffReader.IncomingBytes) return false;

			byte defCommand = buffReader.ReadUInt8();

			if (!Enum.IsDefined(typeof(LowCommandsResponse), defCommand)) return false;
			
			LowCommandsResponse command = (LowCommandsResponse)defCommand;

			buffReader.SetPosition(true, 0);
			if (CRC8.ComputeChecksum(0, 3, buffReader.ReadBytes(3)) != buffReader.ReadUInt8()) return false;

			switch (command)
			{
				case LowCommandsResponse.System:
					return this.SystemPacket();
				case LowCommandsResponse.Data:
					break;
				case LowCommandsResponse.DataBroken:
					break;
				case LowCommandsResponse.ErrorCRC:
					break;
				case LowCommandsResponse.NotSupported:
					break;
				default:
					break;
			}

			return true;
		}

		private bool SystemPacket()
		{
			LowCommandsRequest lCommand = (LowCommandsRequest)buffReader.ReadUInt8();
			//bool crc8 = CRC8.ComputeChecksum(0, message.Length - 1, message) == message[message.Length - 1];

			switch (lCommand)
			{
				case LowCommandsRequest.Ping:
					if (!IsInitConnection)
						return false;
					break;
				case LowCommandsRequest.Version:
					if (IsInitConnection)
						return false;

					Version clientProtoVer = new Version(Encoding.ASCII.GetString(buffReader.ReadBytes((int)(buffReader.IncomingBytesUnread - 1))));
					if (this.ProtoVersion != clientProtoVer)
						return false;

					pingTimer.Reset();

					IsInitConnection = true;
					break;
				case LowCommandsRequest.RunType:
					int runType = buffReader.ReadInt32();
					break;
				default:
					return false;
			}

			return true;
		}

		public void DisconnectByClient()
		{
			pingTimer.Pause();
			receiveTimer.Pause();

			if (IsStartingDisconnect)
				return;

			IsStartingDisconnect = true;
			disconnectTimer.Start(disconnectTimeOut, false);
			base.Send(AssemblyRawPack(LowCommandsRequest.Disconnect));
		}

		private void Ping_TimeLapse()
		{
			base.Send(AssemblyRawPack(LowCommandsRequest.Ping));
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
			if (Command.Client != null && ((BaseProtocol)Command.Client).ConnectionID != this.ConnectionID)
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
		PoolSlot<BaseProtocol> IPoolSlotHolder<BaseProtocol>.PoolSlot { get; set; }

		/// <summary>
		/// Осуществляет очистку объекта, перед возвратом в пулл.
		/// </summary>
		public override void CleanUp()
		{
			pingTimer.Stop();
			receiveTimer.Stop();
			disconnectTimer.Stop();

			buffReader.Clear();
			buffWriter.Clear();

			TotalBytesTransmitted = 0;
			TotalBytesReceived = 0;

			IsInitConnection = false;
			IsStartingDisconnect = false;
			ConnectionID = Guid.Empty;
			base.CleanUp();
		}
		#endregion

		#region Assembly protocol package
		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <returns></returns>
		private byte[] AssemblyRawPack(LowCommandsRequest lowCommand)
		{
			return AssemblyRawPack(lowCommand, null);
		}

		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <param name="data">Данные, усекается до 65529</param>
		/// <returns></returns>
		private byte[] AssemblyRawPack(LowCommandsRequest lowCommand, byte[] data)
		{
			buffWriter.Clear();

			buffWriter.WriteUInt16((ushort)(2 + 1 + 1 + (data == null ? 0 : data.Length > 65530 ? 65530 : data.Length + 1)));
			buffWriter.WriteUInt8((byte)lowCommand);
			buffWriter.WriteUInt8(CRC8.ComputeChecksum(0, 3, buffWriter.ReadBytes(3)));

			if (data != null)
			{
				buffWriter.WriteBytes(data);
				buffWriter.WriteUInt8(CRC8.ComputeChecksum(0, data.Length, data));
			}

			return buffWriter.ToByteArray();
		}
		#endregion
	}
}
