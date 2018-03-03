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
using System.Net;
using Manager.SockServer.Common;
using Manager.SockClient.Common;
using Manager.SockServer;
using Manager.SockClient;

namespace AppProxy
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{

		Manager.SockServer.Server ServerModule;

		public MainWindow()
		{
			InitializeComponent();

			int Port = 85;
			ServerModule = new Manager.SockServer.Server();

			try
			{
				ServerModule.Start(Port);
			}
			catch
			{
				return;
			}
			ServerModule.ClientsReceiveCommand += new OnClientsReceiveCommand(ServerModule_ClientReceiveCommand);

			//LB_Users.ItemsSource = ServerModule.Clients;
			LB_Users.DisplayMemberPath = "ClientInfo";
		}

		void ServerModule_ClientReceiveCommand(FromClientCommand Command)
		{
			if (Command.Command == ClientActions.Disconnected || Command.Command == ClientActions.Shutdown)
			{
				TB_Messages.AppendText(Command.Client.ClientEndPoint + " Server Message: " + Command.Command + "\n");
				LB_Users.Items.Remove(Command.Client);

				Clients.Remove(Command.Client.ClientEndPoint);
			}
			else
			{
				if (Command.Command == ClientActions.Connected)
				{
					TB_Messages.AppendText(Command.Client.ClientEndPoint + " Server Message: " + Command.Command + "\n");
					LB_Users.Items.Add(Command.Client);

					Manager.SockClient.Client ClientModule = new Manager.SockClient.Client();
					ClientModule.ReceiveBufferSize = 65535;
					ClientModule.ServerReceiveCommand += new OnServerReceiveCommand(ClientModule_ServerReceiveCommand);
					ClientModule.UserToken = Command.Client.ClientEndPoint;

					Clients.Add(Command.Client.ClientEndPoint, new object[] { Command.Client, ClientModule });
					// Создаем новое подключение для клиента и заносим его в список клиентов
				}
				else
				{
					if (Command.Command == ClientActions.Receive)
					{
						byte[] buf = new byte[Command.ReceiveBufferLength];
						Array.Copy(Command.ReceiveBuffer, 0, buf, 0, Command.ReceiveBufferLength);

						if (!((Manager.SockClient.Client)Clients[Command.Client.ClientEndPoint][1]).isConnected)
						{
							((Manager.SockClient.Client)Clients[Command.Client.ClientEndPoint][1]).Connect(new DnsEndPoint("192.168.0.132", 1348), buf);
						}

						//TB_Messages.AppendText(Command.Client.ClientInfo + " Server Message: " + Command.Command + ", Buffer: " + Encoding.ASCII.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");
						TB_Messages.AppendText(Command.Client.ClientEndPoint + " Server Message: " + Command.Command + "\n");
						// По приходу сообщения от клиента, находим его подключение в списке клиентов и отправляем его туда

					}
					else
					{
						//TB_Messages.AppendText(Command.Client.ClientInfo + " Server Message: " + Command.Command + ", Buffer: " + Encoding.ASCII.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");
					}
				}
			}
			TB_Messages.ScrollToEnd();
		}

		void ClientModule_ServerReceiveCommand(FromServerCommand Command)
		{
			if (Command.Action == ServerActions.AlreadyConnected)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.Connected)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.ConnectedOverProxy)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.ConnectedToProxy)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.Connecting)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.ConnectingToProxy)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.ConnectionFailed)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.Disconnected)
			{
				try
				{
					TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
					ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection>() { ((OnServerClientConnection)Clients[(EndPoint)Command.UserToken][0]) } });
				}
				catch { }
			}
			else if (Command.Action == ServerActions.ProxyAuthFailed)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.Receive)
			{
				byte[] buf = new byte[Command.ReceiveBufferLength];
				Array.Copy(Command.ReceiveBuffer, 0, buf, 0, Command.ReceiveBufferLength);

				try
				{
					ServerModule.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = buf, ToClient = new List<OnServerClientConnection>() { ((OnServerClientConnection)Clients[(EndPoint)Command.UserToken][0]) } });
				}
				catch { }
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.SendCompleted)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
			else if (Command.Action == ServerActions.Shutdown)
			{
				TB_Messages.AppendText(Command.UserToken.ToString() + " Message: " + Command.Action + "\n");
			}
		}

		Dictionary<EndPoint, object[]> Clients = new Dictionary<EndPoint, object[]>();
	}
}
