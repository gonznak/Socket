using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Manager.SockServer.Common;
using Manager.SockServer;
using System.Text;
using Manager.SockClient;
using Manager.SockClient.Common;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AppProxyServer
{
	/// <summary>
	/// Логика взаимодействия для App.xaml
	/// </summary>
	public partial class App : Application
	{
		Manager.SockServer.Server proxyServer;

		public Dictionary<OnServerClientConnection, string> clients = new Dictionary<OnServerClientConnection, string>();

		public App()
		{
			proxyServer = new Manager.SockServer.Server();
			proxyServer.ClientsReceiveCommand += new OnClientsReceiveCommand(proxyServer_ClientReceiveCommand);
			proxyServer.Start(85);
		}

		void proxyServer_ClientReceiveCommand(FromClientCommand Command)
		{
			if (Command.Command == ClientActions.Receive)
			{
				if (CheckRequestAndSend(Command))
				{
					HttpRequest request = null;
					try
					{
						request = HttpParse.Parse(clients[Command.Client]);
					}
					catch { }

					if (request != null)
					{
						/*// Текущий клиент остается висеть, до тех пор пока сам не отключится или его не отключат
						Manager.SockClient.Client client = new Manager.SockClient.Client();
						client.ServerReceiveCommand += new OnServerReceiveCommand(client_ServerReceiveCommand);
						client.SetConnectBuffer(Encoding.ASCII.GetBytes(clients[Command.Client]), 0, clients[Command.Client].Length);
						client.UserToken = Command.Client;
						if (request.Host.Split(new char[] { ':' }).Length == 2)
							client.Connect(new System.Net.DnsEndPoint(request.Host.Split(new char[] { ':' })[0], int.Parse(request.Host.Split(new char[] { ':' })[1])));
						else
							client.Connect(new System.Net.DnsEndPoint(request.Host, 80));
						 * */

						Socket websocket = null;

						try
						{
							//if (sMessage.Substring(0, 7) != "CONNECT" && iHostNameStart != -1 && iHostNameEnd != -1 && iHostNameEndFirstSlash != -1)
							{
								//This is a socket to the real webserver with the webpage
								websocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
								if (request.Host.Split(new char[] { ':' }).Length == 2)
									websocket.Connect(new System.Net.DnsEndPoint(request.Host.Split(new char[] { ':' })[0], int.Parse(request.Host.Split(new char[] { ':' })[1])));
								else
									websocket.Connect(new System.Net.DnsEndPoint(request.Host, 80));

								websocket.Send(Encoding.ASCII.GetBytes(clients[Command.Client]), clients[Command.Client].Length, 0);
								//Receive data from the webserver
								int ireceivedbyte = 0;
								bool timeout = false;
								for (int i = 0; i < 200; i++)
								{
									Thread.Sleep(10);
									if (websocket.Available != 0)
										break;

									if (i == 200)
									{
										timeout = true;
									}
								}

								if (timeout)
								{
									websocket.Shutdown(SocketShutdown.Both);
									websocket.Close();

									//proxyServer.SendCommand(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection> { Command.Client } });

									//if (clientSocket != null && clientSocket.Connected)
									//	clientSocket.Close();
								}

								while (websocket.Available != 0)
								{
									byte[] receivebuffer = new byte[websocket.Available];
									ireceivedbyte = websocket.Receive(receivebuffer, receivebuffer.Length, 0);
									//Console.WriteLine("Received from webserver socket {0} bytes", +ireceivedbyte);

									//clientSocket.Send(receivebuffer, ireceivedbyte, 0);
									proxyServer.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = receivebuffer, ToClient = new List<OnServerClientConnection> { (OnServerClientConnection)Command.Client } });
								}

								websocket.Shutdown(SocketShutdown.Both);
								websocket.Close();
								//clientSocket.Receive(readbyte, 4096, 0);
							}
							//else
							//{
							//	ireadbytes = 0;
							//}
						}
						catch
						{

						}
						finally
						{
							//ireadbytes = 0;
							//proxyServer.SendCommand(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection> { Command.Client } });

							//if (clientSocket != null && clientSocket.Connected)
							//	clientSocket.Close();
						}



					}
					else
					{
						//proxyServer.SendCommand(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection> { Command.Client } });
						clients.Remove(Command.Client);
					}
				}
			}

			if (Command.Command == ClientActions.Disconnected)
			{
				clients.Remove(Command.Client);
			}

			if (Command.Command == ClientActions.Shutdown)
			{
				clients.Remove(Command.Client);
			}
		}

		bool CheckRequestAndSend(FromClientCommand Command)
		{
			string stmpstring = Encoding.ASCII.GetString(Command.ReceiveBuffer, 0, Command.ReceiveBufferLength);

			bool completeToSend = false;

			if (!clients.ContainsKey(Command.Client))
				clients.Add(Command.Client, "");

			if (stmpstring.IndexOf("\r\n\r\n") != -1)
			{
				clients[Command.Client] += stmpstring.Substring(0, stmpstring.IndexOf("\r\n\r\n") + 4);
				completeToSend = true;
			}
			else
			{
				clients[Command.Client] += (String)stmpstring;
				if (clients[Command.Client].IndexOf("\r\n\r\n") != -1)
				{
					clients[Command.Client] = clients[Command.Client].Substring(0, clients[Command.Client].IndexOf("\r\n\r\n") + 4);
					completeToSend = true;
				}
			}

			return completeToSend;
		}

		void client_ServerReceiveCommand(FromServerCommand Command)
		{
			if (Command.Action == ServerActions.ConnectionFailed)
			{
				proxyServer.Send(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection> { (OnServerClientConnection)Command.UserToken } });
			}
			if (Command.Action == ServerActions.Receive)
			{
				proxyServer.Send(new ToServerCommand() { Action = ServerCommands.Send, ReceiveBuffer = Command.ReceiveBuffer, ToClient = new List<OnServerClientConnection> { (OnServerClientConnection)Command.UserToken } });
			}
			if (Command.Action == ServerActions.Disconnected)
			{
				proxyServer.Send(new ToServerCommand() { Action = ServerCommands.Disconnect, ToClient = new List<OnServerClientConnection> { (OnServerClientConnection)Command.UserToken } });
			}

			if (Command.Action != ServerActions.ConnectionFailed && Command.Action != ServerActions.Receive && Command.Action != ServerActions.Connecting && Command.Action != ServerActions.Connected && Command.Action != ServerActions.Disconnected)
			{

			}
		}
	}

	public class HttpParse
	{

		//  GET http://192.168.0.135/upload/thumbs/shrza_1_0.gif HTTP/1.1
		//  User-Agent: Opera/9.80 (Windows NT 5.1) Presto/2.12.388 Version/12.15
		//  Host: 192.168.0.135
		//  Accept: text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/webp, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1
		//  Accept-Language: ru-RU,ru;q=0.9,en;q=0.8
		//  Accept-Encoding: gzip, deflate
		//  Referer: http://192.168.0.135/upload/thumbs/shrza_1_0.gif
		//  Cookie: site_skin=s3; uid=02D1EDA3-BCCA-4EA4-ACE6-0B69CBD29164
		//  Cache-Control: no-cache
		//  Connection: Keep-Alive

		public static HttpRequest Parse(string request)
		{
			string[] lines = request.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
			{ }

			HttpRequest httpRequest = new HttpRequest();
			string[] firstLine = lines[0].Split(new char[] { ' ' });
			if (firstLine.Length != 2 && firstLine.Length != 3)
				return httpRequest;

			httpRequest.Method = Enum.GetNames(typeof(Methods)).Contains(firstLine[0]) ? (Methods)Enum.Parse(typeof(Methods), firstLine[0]) : Methods.INCORRECT;
			httpRequest.Uri = firstLine[1];
			if (firstLine.Length == 3)
			{
				if (firstLine[2].Length == 8)
				{
					if (firstLine[2].Contains("HTTP/1.0"))
						httpRequest.HttpVersion = HttpVersions.v1_0;
					if (firstLine[2].Contains("HTTP/1.1"))
						httpRequest.HttpVersion = HttpVersions.v1_1;
				}
			}

			for (int i = 1; i < lines.Length; i++)
			{
				if (lines[i].Contains("Host: "))
				{
					httpRequest.Host = lines[i].Split(new char[] { ' ' })[1];
					break;
				}
			}
			return httpRequest;
		}
	}

	public class HttpRequest
	{
		public Methods Method { get; set; }
		public HttpVersions HttpVersion { get; set; }
		public string Host { get; set; }
		public string Uri { get; set; }
	}

	public enum Methods
	{
		INCORRECT,
		GET,
		POST,
		CONNECT
	}

	public enum HttpVersions
	{
		v0_9,
		v1_0,
		v1_1
	}
}
