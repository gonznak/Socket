using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using Manager.SockServer.Common;
using Manager.SockServer;
using System.IO;

namespace AppServer
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private enum LowCommands : byte
		{
			/// <summary>
			/// Пустая посылка, для поддержания соединения
			/// </summary>
			Ping = 0,
			/// <summary>
			/// Посылка содержит данные
			/// </summary>
			Data = 1,
			/// <summary>
			/// Запрос версии
			/// </summary>
			Version = 2,
			/// <summary>
			/// Как работает, сервис или просто
			/// </summary>
			RunType = 3,
			/// <summary>
			/// Данные успешно приняты
			/// </summary>
			DataOk = 250,
			/// <summary>
			/// Указывает, что принятый пакет пришел не полностью
			/// </summary>
			DataBroken = 251,
			/// <summary>
			/// Ошибка контрольной суммы данных
			/// </summary>
			ErrorCRC = 252,
			/// <summary>
			/// Команда переподключение на новый адрес
			/// </summary>
			Reconnect = 253,
			/// <summary>
			/// Команда на отключение
			/// </summary>
			Disconnect = 254,
			/// <summary>
			/// Не поддерживаемая команда
			/// </summary>
			NotSupported = 255
		}


		Server ServerModule;

		Timer timer;
		public MainWindow()
		{
			InitializeComponent();

			int Port = 9520;
			ServerModule = new Server();
			ServerModule.ReceiveBufferSize = 65535;
			try
			{
				ServerModule.Start(Port);
				timer = new Timer(new TimerCallback(TimerNop), ServerModule, 10 * 1000, 10 * 1000);
			}
			catch
			{
				return;
			}
			ServerModule.ClientsReceiveCommand += new OnClientsReceiveCommand(ServerModule_ClientCommand);

			//LB_Users.ItemsSource = ServerModule.Clients;
			LB_Users.DisplayMemberPath = "ClientInfo";
		}

		void ServerModule_ClientCommand(FromClientCommand Command)
		{
			if (Command.Command == ClientActions.Disconnected || Command.Command == ClientActions.Shutdown)
			{
				LB_Users.Items.Remove(Command.Client);
			}
			else if (Command.Command == ClientActions.Connected)
			{
				LB_Users.Items.Add(Command.Client);
			}

			if (Command.ReceiveBufferLength > 0)
			{
				if (Command.Command == ClientActions.Receive)
				{
					if (Command.ReceiveBufferLength >= 4 && Crc8.ComputeChecksum(0, 3, Command.ReceiveBuffer) == Command.ReceiveBuffer[3])
					{
						if (Command.Client == null)
							TB_Messages.AppendText(DateTime.Now.ToString() + ": " + "Message: " + Command.Command + ", Buffer length: " + Command.ReceiveBufferLength + "\n");
						else
						{
							if ((LowCommands)Command.ReceiveBuffer[2] == LowCommands.Version)
							{
								TB_Messages.AppendText(DateTime.Now.ToString() + ": " + Command.Client.ClientEndPoint + " Message: " + Command.Command + ", Buffer: " + Encoding.ASCII.GetString(Command.ReceiveBuffer, 4, Command.ReceiveBufferLength - 4 - 1) + ", Type: " + (LowCommands)Command.ReceiveBuffer[2] + "\n");
							}
							else if ((LowCommands)Command.ReceiveBuffer[2] == LowCommands.RunType)
							{
								TB_Messages.AppendText(DateTime.Now.ToString() + ": " + Command.Client.ClientEndPoint + " Message: " + Command.Command + ", Value: " + BitConverter.ToInt32(Command.ReceiveBuffer, 4) + ", Type: " + (LowCommands)Command.ReceiveBuffer[2] + "\n");
							}
							else
							{
								TB_Messages.AppendText(DateTime.Now.ToString() + ": " + Command.Client.ClientEndPoint + " Message: " + Command.Command + ", Buffer length: " + Command.ReceiveBufferLength + ", Type: " + (LowCommands)Command.ReceiveBuffer[2] + "\n");
							}
						}
					}
					else
					{
						ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection>() { Command.Client } });
					}
				}

				//TB_Messages.AppendText(Command.Client.ClientInfo + " Message: " + Command.Command + ", Buffer: " + Encoding.ASCII.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");

				//TB_Messages.AppendText(Command.Client.ClientInfo + " Message: " + Command.Command + ", Buffer: " + BitConverter.ToString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");
			}
			else
			{
				if (Command.Client == null)
					TB_Messages.AppendText(DateTime.Now.ToString() + ": " + "Message: " + Command.Command + "\n");
				else
					TB_Messages.AppendText(DateTime.Now.ToString() + ": " + Command.Client.ClientEndPoint + " Message: " + Command.Command + "\n");
			}

			if ((TB_Messages.ExtentHeight - TB_Messages.VerticalOffset - TB_Messages.ViewportHeight) < 50)
				TB_Messages.ScrollToEnd();
		}

		static void TimerNop(object state)
		{
			Server srv = (Server)state;
			srv.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Ping) });
		}

		private void MessageField_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if (e.Key == Key.Enter)
				{
					switch (TB_MessageField.Text)
					{
						case "exit":
							ServerModule.Stop();
							break;
						case "disc":
							ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Disconnect) });
							break;
						case "ver":
							ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Version) });
							break;
						case "mode":
							ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.RunType) });
							break;
						case "buf":
							SendBuf();
							break;
						case "cls":
							TB_Messages.Clear();
							break;
						default:
							ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Data, Encoding.UTF8.GetBytes(TB_MessageField.Text)) });
							break;
					}
				}
			}
			catch
			{
			}
		}

		static int bufSize = 65535;
		byte[] FirstBuf = new byte[bufSize];
		byte[] SecondBuf = new byte[bufSize];

		private void SendBuf()
		{
			for (int i = 0; i < bufSize; i++)
			{
				FirstBuf[i] = 65;
				SecondBuf[i] = 66;
			}
			ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Data, FirstBuf) });
			//ServerModule.SendCommand(new ToServerCommand() { Action = ToServerCommand.Actions.Send, ReceiveBuffer = SecondBuf });
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			byte[] message = new byte[0];
			using (BinaryReader br = new BinaryReader(new FileStream(@"d:\test.jpg", FileMode.Open)))
			{
				message = new byte[br.BaseStream.Length + 4];
				Array.Copy(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, 0, message, 0, 4);
				Array.Copy(br.ReadBytes((int)br.BaseStream.Length), 0, message, 4, br.BaseStream.Length);
			}
			ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = MainWindow.AssemblyRawPack(LowCommands.Data, message) });
		}

		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <returns></returns>
		static byte[] AssemblyRawPack(LowCommands lowCommand)
		{
			return AssemblyRawPack(lowCommand, null);
		}

		/// <summary>
		/// Функция сбора пакета
		/// </summary>
		/// <param name="lowCommand">Низкоуровневая команда</param>
		/// <param name="data">Данные, усекается до 65529</param>
		/// <returns></returns>
		static byte[] AssemblyRawPack(LowCommands lowCommand, params byte[] data)
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
				pack[pack.Length - 1] = Crc8.ComputeChecksum(4, dataLength, pack);
			}
			else
			{
				length = 2 + 1 + 1;
				pack = new byte[length];
			}

			System.Buffer.BlockCopy(BitConverter.GetBytes(length), 0, pack, 0, 2);
			pack[2] = (byte)lowCommand;
			pack[3] = Crc8.ComputeChecksum(0, 3, pack);
			return pack;
		}
	}

	public static class Crc8
	{
		static byte[] table = new byte[256];
		// x8 + x7 + x6 + x4 + x2 + 1
		const byte poly = 0xd5;

		public static byte ComputeChecksum(int offset, int count, params byte[] bytes)
		{
			byte crc = 0xff;
			int _offset = 0;
			int _count = 0;

			if (bytes != null && bytes.Length > 0)
			{
				foreach (byte b in bytes)
				{
					_offset += 1;
					if (_offset <= offset)
						continue;

					_count += 1;
					if (_count > count)
						break;

					crc = table[crc ^ b];
				}
			}
			return crc;
		}

		static Crc8()
		{
			for (int i = 0; i < 256; ++i)
			{
				int temp = i;
				for (int j = 0; j < 8; ++j)
				{
					if ((temp & 0x80) != 0)
					{
						temp = (temp << 1) ^ poly;
					}
					else
					{
						temp <<= 1;
					}
				}
				table[i] = (byte)temp;
			}
		}
	}
}
