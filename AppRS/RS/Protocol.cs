using System;
using System.Text;

namespace AppRS.RS
{
	public enum LowCommandsRequest : byte
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

	internal enum LowCommandsResponse : byte
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

	public enum DataAction : byte
	{
		Update = 0,
		Shot,
		None = 255
	}

	public delegate void OnSendMessage(byte[] message);
	public delegate void OnDisconnect();

	public class Protocol
	{
		public event OnSendMessage SendMessage;
		public event OnDisconnect Disconnected;

		TimeoutWatcher pingTimer = new TimeoutWatcher();

		public bool IsValid { get; private set; }

		public Protocol()
		{
			pingTimer.TimeLapse += new OnTimerLapse(ping_TimeLapse);
		}

		public void HandleMessage(byte[] packet, int length)
		{
			pingTimer.Reset();

			if (length < 2 + 2 || true)
			{
				//Disconnected();
				return;
			}

			int dataLength = BitConverter.ToUInt16(packet, 0);
			LowCommandsResponse command = (LowCommandsResponse)packet[2];
			bool crc8 = CRC8.ComputeChecksum(0, 3, packet) == packet[3];

			byte[] message = new byte[dataLength - 4];

			/*
			0-1	- data length
			2		- LowCommandsRequest
			3		- crc8 (0-2)
			N		- LowCommands
			D		- data
			N+D+1	- data crc8
			*/

			if (crc8 == false)
			{
				Disconnect();
				return;
			}

			switch (command)
			{
				case LowCommandsResponse.System:
					Array.Copy(packet, 4, message, 0, message.Length);
					this.SystemPacket(message);
					break;
				case LowCommandsResponse.Data:
					{ }
					break;
				case LowCommandsResponse.DataBroken:
					{ }
					break;
				case LowCommandsResponse.ErrorCRC:
					{ }
					break;
				case LowCommandsResponse.NotSupported:
					{ }
					break;
				default:
					{ }
					break;
			}
		}

		private void SystemPacket(byte[] message)
		{
			LowCommandsRequest lCommand = (LowCommandsRequest)message[0];
			bool crc8 = CRC8.ComputeChecksum(0, message.Length - 1, message) == message[message.Length - 1];

			switch (lCommand)
			{
				case LowCommandsRequest.Ping:
					this.Send(Protocol.AssemblyRawPack(LowCommandsRequest.Version));
					break;
				case LowCommandsRequest.Version:
					string version = Encoding.ASCII.GetString(message, 1, message.Length - 2);

					this.Send(Protocol.AssemblyRawPack(LowCommandsRequest.RunType));
					break;
				case LowCommandsRequest.RunType:
					int runType = BitConverter.ToInt32(message, 1);
					break;
				default:
					this.Disconnect();
					break;
			}
		}

		public void DataPacket(DataAction action, byte[] data)
		{
			byte[] result = new byte[data.Length + 1];
			result[0] = (byte)action;
			Array.Copy(data, 0, result, 1, data.Length);

			this.Send(Protocol.AssemblyRawPack(LowCommandsRequest.Data, result));
		}

		public void DoSended()
		{

		}

		public void Connected()
		{
			this.Send(Protocol.AssemblyRawPack(LowCommandsRequest.Ping));
			pingTimer.Start(40);
		}

		void ping_TimeLapse()
		{
			this.Send(Protocol.AssemblyRawPack(LowCommandsRequest.Ping));
		}

		void Send(byte[] message)
		{
			SendMessage?.Invoke(message);
		}

		public void Disconnect()
		{
			Disconnected?.Invoke();

			pingTimer.Reset();
			pingTimer.Stop();
		}

		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <returns></returns>
		public static byte[] AssemblyRawPack(LowCommandsRequest lowCommand)
		{
			return AssemblyRawPack(lowCommand, null);
		}

		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <param name="data">Данные, усекается до 65529</param>
		/// <returns></returns>
		public static byte[] AssemblyRawPack(LowCommandsRequest lowCommand, params byte[] data)
		{
			int length = 0;
			byte[] pack = null;

			if (data != null)
			{
				int dataLength = 0;
				dataLength = data.Length > 65530 ? 65530 : data.Length;
				length = 2 + 1 + 1 + dataLength + 1;
				pack = new byte[length];
				System.Buffer.BlockCopy(data, 0, pack, 4, dataLength);
				pack[pack.Length - 1] = CRC8.ComputeChecksum(4, dataLength, pack);
			}
			else
			{
				length = 2 + 1 + 1;
				pack = new byte[length];
			}

			System.Buffer.BlockCopy(BitConverter.GetBytes(length), 0, pack, 0, 2);
			pack[2] = (byte)lowCommand;
			pack[3] = CRC8.ComputeChecksum(0, 3, pack);
			return pack;
		}
	}
}
