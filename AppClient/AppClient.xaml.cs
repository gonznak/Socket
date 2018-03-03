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
using RawClient;
using RawClient.Common;

namespace AppClient
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		Client ClientModule;
		public MainWindow()
		{
			InitializeComponent();

			ClientModule = new RawClient.Client();
			ClientModule.ReceiveBufferSize = 65535;
			ClientModule.ServerReceiveCommand += new OnServerReceiveCommand(client_ServerReceiveCommand);

			TB_MessageField.IsEnabled = false;
		}

		void client_ServerReceiveCommand(FromServerCommand Command)
		{
			if (Command.Action == ServerActions.SendCompleted || Command.Action == ServerActions.Connected)
				TB_MessageField.IsEnabled = true;

			if (Command.ReceiveBuffer == null)
			{
				TB_Messages.AppendText(DateTime.Now.ToString("HH:mm:ss.f") + " Message: " + Command.Action + "\n");
			}
			else
			{
				TB_Messages.AppendText(DateTime.Now.ToString("HH:mm:ss.f") + " Message: " + Command.Action + ", Buffer: " + Encoding.UTF8.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength) + "\n");
			}
			TB_Messages.ScrollToEnd();
		}

		private void MessageField_KeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if ((e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) > 0) || e.Key == Key.Enter)
				{
					int result = 0;

					if (int.TryParse(TB_MessageField.Text, out result) && result > 0)
					{
						ClientModule.SendCommand(new ToClientCommand() { Command = ClientCommands.Send, ReceiveBuffer = BitConverter.GetBytes(result) });
					}
					else
					{
						switch (TB_MessageField.Text)
						{
							case "disc":
								ClientModule.SendCommand(new ToClientCommand() { Command = ClientCommands.Disconnect });
								break;
							case "cls":
								TB_Messages.Clear();
								break;
							default:
								ClientModule.SendCommand(new ToClientCommand() { Command = ClientCommands.Send, ReceiveBuffer = Encoding.UTF8.GetBytes(TB_MessageField.Text) });
								TB_MessageField.IsEnabled = false;
								break;
						}
					}
				}
			}
			catch
			{ }
		}

		private void B_Connect_Click(object sender, RoutedEventArgs e)
		{
			//ClientModule.ProxyUse = true;
			//ClientModule.ProxyPoint = new DnsEndPoint("192.168.0.102", 8080);
			//ClientModule.Connect(new System.Net.DnsEndPoint("46.4.179.80", 7000));
			if (ClientModule.isConnected)
				ClientModule.SendCommand(new ToClientCommand(){Command = ClientCommands.Disconnect});
			else
				//ClientModule.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9520));
				ClientModule.Connect(new IPEndPoint(IPAddress.Parse("192.168.0.5"), 23));

		}
	}
}
