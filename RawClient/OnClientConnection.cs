using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using RawClient.Common;

namespace RawClient
{
	public class OnClientConnection
	{
		#region Внутренние события

		private event OnClientCommand ClientRequestCommand;


		/// <summary>
		/// Принятый пакет данных от клиента, отправляется серверу на обработку
		/// </summary>
		public event OnServerCommand ServerReceiveCommand;
		#endregion

		private bool fDisconnecting = false;

		private Socket Sock;
		private SocketAsyncEventArgs SockAsyncEventArgs_Send;
		private SocketAsyncEventArgs SockAsyncEventArgs_Receive;
		private SocketAsyncEventArgs SockAsyncEventArgs_Disconnect;

		private int m_ReceiveBufferSize = 8192;	// Максимальный размер входящего буфера

		#region Свойства
		private EndPoint m_ServerInfo;

		public EndPoint ServerInfo
		{
			get { return m_ServerInfo; }
		}

		public int ReceiveBufferSize
		{
			get { return m_ReceiveBufferSize; }
		}

		/// <summary>
		/// Пользовательская переменная
		/// </summary>
		private object UserToken { get; set; }
		#endregion

		#region Внутренние команды
		/// <summary>
		/// Выполнить команду на сервере
		/// </summary>
		/// <param name="Command">Объект команды</param>
		public void Command(ToClientCommand Command)
		{
			if (ClientRequestCommand != null)
			{
				ClientRequestCommand(Command);
			}
		}

		/// <summary>
		/// Принятое событие от сервера
		/// </summary>
		/// <param name="Command">Объект команды</param>
		private void ServerAction(FromServerCommand Command)
		{
			if (ServerReceiveCommand != null)
			{
				ServerReceiveCommand(Command);
			}
		}
		#endregion

		private bool UsingProxy = false;
		private bool ConnectionOverProxyConfirmed = false;

		private byte[] OnProxyConnectBuffer = new byte[0];

		#region .ctor Connection
		public OnClientConnection(Socket AcceptedSocket, bool UseProxy, byte[] ProxyConnectBuffer, int ReceiveBufferSize, object UserToken)
		{
			this.UserToken = UserToken;
			UsingProxy = UseProxy;
			OnProxyConnectBuffer = ProxyConnectBuffer;
			m_ReceiveBufferSize = ReceiveBufferSize;

			m_ServerInfo = AcceptedSocket.RemoteEndPoint;

			// Создание прерывания на обработку входящих команд от сервера
			ClientRequestCommand += new OnClientCommand(HandlingServerCommands);

			Sock = AcceptedSocket;

			// После подключения встаем на прослушку сообщений клиента
			SockAsyncEventArgs_Receive = new SocketAsyncEventArgs();
			SockAsyncEventArgs_Receive.Completed += SockAsyncEventArgs_Completed;
			SockAsyncEventArgs_Receive.SetBuffer(new byte[m_ReceiveBufferSize], 0, m_ReceiveBufferSize);
			ReceiveAsync(SockAsyncEventArgs_Receive);
		}
		#endregion

		#region Handling server commands
		void HandlingServerCommands(ToClientCommand Command)
		{
			switch (Command.Command)
			{
				case ClientCommands.Send:
					ActionSend(Command);
					break;
				case ClientCommands.Disconnect:
					ActionDisconnect(Command);
					break;
				default:
					break;
			}
		}

		/// <summary>
		/// Отправка сообщения клиентам
		/// </summary>
		/// <param name="Command">Объект сообщения, если параметр ToClient не определен, сообщение отправляется всем подключенным клиентам </param>
		private void ActionSend(ToClientCommand Command)
		{
			Send(Command.ReceiveBuffer);
		}

		/// <summary>
		/// Отправка команды на отключение клиента от сервера
		/// </summary>
		/// <param name="Command">Объект сообщения,если параметр ToClient не определен, сообщение отправляется всем подключенным клиентам </param>
		private void ActionDisconnect(ToClientCommand Command)
		{
			if (!fDisconnecting)
			{
				ClientRequestCommand -= new OnClientCommand(HandlingServerCommands);
				Disconnect();
				/*try
				{
					Sock.Shutdown(SocketShutdown.Both);
				}
				catch { }*/

				// Завершаем асинхронную операцию чтения
				if (SockAsyncEventArgs_Receive != null)
				{
					SockAsyncEventArgs_Receive.Completed -= SockAsyncEventArgs_Completed;
					SockAsyncEventArgs_Receive.Dispose();
				}
				// Завершаем асинхронную операцию передачи
				if (SockAsyncEventArgs_Send != null)
				{
					SockAsyncEventArgs_Send.Completed -= SockAsyncEventArgs_Completed;
					SockAsyncEventArgs_Send.Dispose();
				}

				// Начинаем операцию асинхронного отключения клиента
				if (SockAsyncEventArgs_Disconnect != null)
				{
					SockAsyncEventArgs_Disconnect.Completed -= SockAsyncEventArgs_Completed;
					SockAsyncEventArgs_Disconnect.Dispose();
				}

				// Обнуляем переменные
				SockAsyncEventArgs_Receive = null;
				SockAsyncEventArgs_Send = null;
				SockAsyncEventArgs_Disconnect = null;

				//Sock.Close();
				//Sock.Dispose();
			}
		}
		#endregion End Handling

		private void SockAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Disconnect:
					if (fDisconnecting)
					{
						//ProcessDisconnect(e);
					}
					return;

				case SocketAsyncOperation.Receive:
					if (!fDisconnecting)				// Если не отключаемся
					{
						ProcessReceive(e);
					}
					break;
				case SocketAsyncOperation.Send:
					ProcessSend(e);
					break;
			}
		}

		#region Send
		private void Send(byte[] data)
		{
			if (data.Length > 0)
			{
				try
				{
					if (SockAsyncEventArgs_Send == null)
					{
						SockAsyncEventArgs_Send = new SocketAsyncEventArgs();
						SockAsyncEventArgs_Send.UserToken = "CSocketAsyncEventArgs_Send";
						SockAsyncEventArgs_Send.Completed += SockAsyncEventArgs_Completed;
					}
					SockAsyncEventArgs_Send.SetBuffer(data, 0, data.Length);
					SendAsync(SockAsyncEventArgs_Send);
				}
				catch (Exception ex)
				{
					throw ex;
				}
			}
		}

		private void SendAsync(SocketAsyncEventArgs e)
		{
			try
			{
				if (!Sock.SendAsync(e))
				{
					ProcessSend(e);
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		private void ProcessSend(SocketAsyncEventArgs e)
		{
			try
			{
				if (e.SocketError == SocketError.Success)
				{
					ServerAction(new FromServerCommand() { Action = ServerActions.SendCompleted, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, UserToken = this.UserToken });
				}
				else if (e.SocketError == SocketError.NotConnected)
				{
					ActionDisconnect(new ToClientCommand() { Command = ClientCommands.Disconnect });
				}
				else
				{
					ServerAction(new FromServerCommand() { Action = ServerActions.UnknownSend, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, UserToken = this.UserToken });
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}
		#endregion

		#region Receive
		private void ReceiveAsync(SocketAsyncEventArgs e)
		{
			try
			{
				if (!Sock.ReceiveAsync(e))
				{
					ProcessReceive(e);
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		private void ProcessReceive(SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred == 0)
			{
				// Задание: создать клиента, который будет отсылать сообщение длиной 0 байт, проверить на "отключился"

				// Проверка на разрыв соединения клиентом
				//if (Sock.Poll(10000, SelectMode.SelectRead))
				//{
					ActionDisconnect(new ToClientCommand() { Command = ClientCommands.Disconnect });
				//}
				//else
				//{
				//	ServerAction(new FromServerCommand() { Action = ServerActions.UnknownRecevie, UserToken = this.UserToken });
					// На всякий случай остаемся на связи и слушаем клиента
				//	ReceiveAsync(SockAsyncEventArgs_Receive);
				//}
			}
			else
			{
				if (e.SocketError == SocketError.Success)
				{
					byte[] abReceiveBuffer = new byte[e.BytesTransferred];
					Array.Copy(e.Buffer, abReceiveBuffer, e.BytesTransferred);

					if (UsingProxy && !ConnectionOverProxyConfirmed)
					{
						ValidateProxyResponse(abReceiveBuffer);
					}
					else
					{
						ServerAction(new FromServerCommand() { Action = ServerActions.Receive, ReceiveBuffer = abReceiveBuffer, ReceiveBufferLength = abReceiveBuffer.Length, UserToken = this.UserToken });
					}

					// Встаем на прием нового сообщения
					ReceiveAsync(SockAsyncEventArgs_Receive);
				}
				else
				{
					Console.Write("{0}: Необработанное исключение", "Client.ProcessReceive");
				}
			}
		}

		private void ValidateProxyResponse(byte[] ProxyResponse)
		{
			string sBuffer = Encoding.ASCII.GetString(ProxyResponse);
			GroupCollection ProxyState = ProxyStateResponseParser(sBuffer);

			if (ProxyState != null)
			{
				switch (ProxyState["status"].ToString())
				{
					case "200":		// Подключение удачно
						ConnectionOverProxyConfirmed = true;
						ServerAction(new FromServerCommand() { Action = ServerActions.ConnectedOverProxy, UserToken = this.UserToken });
						Send(OnProxyConnectBuffer);
						break;
					case "404":		// Невозможно установить соединение
						ConnectionOverProxyConfirmed = false;
						ServerAction(new FromServerCommand() { Action = ServerActions.ConnectionFailed, UserToken = this.UserToken });
						Disconnect();
						break;
					case "407":		// Необхордимо авторизация на прокси сервере
						ConnectionOverProxyConfirmed = false;
						ServerAction(new FromServerCommand() { Action = ServerActions.ProxyAuthFailed, UserToken = this.UserToken });
						Disconnect();
						break;
					default:		// Неизвестный тип ответа
						ConnectionOverProxyConfirmed = false;
						ServerAction(new FromServerCommand() { Action = ServerActions.UnknownProxyRecevie, ReceiveBuffer = Encoding.UTF8.GetBytes(ProxyState["status"].ToString()), ReceiveBufferLength = ProxyState["status"].Length, UserToken = this.UserToken });
						Disconnect();
						break;
				}
			}

			//if (isConnectingToProxy)
			{
				//isConnectingToProxy = false;
				//Disconnect();
			}
			/*
			 * HTTP/\d.\d\s+(\d+)\s+(.*)
			 * 
			 * 
			HTTP/1.0 404 Not Found
			Pragma: no-cach
			Content-Type: text/html; charset=windows-1251
			 * 
			HTTP/1.0 200 Connection established
			Pragma: no-cach
			Content-Type: text/html; charset=windows-1251
			 * 
			HTTP/1.0 407 Unauthorized
			Proxy-Authenticate: Basic realm="UserGate"
			Pragma: no-cach
			Content-Type: text/html; charset=windows-1251
			 */
		}

		private GroupCollection ProxyStateResponseParser(string source)
		{
			string httpInfo = String.Empty;
			try
			{
				httpInfo = source.Substring(0, source.IndexOf("\r\n\r\n"));
			}
			catch { }

			Regex myReg = new Regex(@"^HTTP/\d.\d\s+(?<status>\d+)\s+(?<message>.+)", RegexOptions.Multiline);
			if (myReg.IsMatch(httpInfo))
			{
				Match m = myReg.Match(httpInfo);
				return m.Groups;
			}
			else
			{
				return null;
			}
		}
		#endregion

		#region Disconnect
		/// <summary>
		/// Отключение клиента от сервера
		/// </summary>
		private void Disconnect()
		{
			if (!fDisconnecting)
			{
				fDisconnecting = true;

				if (SockAsyncEventArgs_Disconnect == null)
				{
					SockAsyncEventArgs_Disconnect = new SocketAsyncEventArgs();
					SockAsyncEventArgs_Disconnect.UserToken = "CSocketAsyncEventArgs_Disconnect";
					SockAsyncEventArgs_Disconnect.Completed += SockAsyncEventArgs_Completed;
					SockAsyncEventArgs_Disconnect.DisconnectReuseSocket = true;
				}
				DisconnectAsync(SockAsyncEventArgs_Disconnect);
			}
		}

		private void DisconnectAsync(SocketAsyncEventArgs e)
		{
			if (Sock.DisconnectAsync(e))
			{
				switch (SockAsyncEventArgs_Receive.SocketError)
				{
					case SocketError.Success:
						ServerAction(new FromServerCommand() { Action = ServerActions.Disconnected, UserToken = this.UserToken });
						break;
					case SocketError.ConnectionReset:
						ServerAction(new FromServerCommand() { Action = ServerActions.Shutdown, UserToken = this.UserToken });
						break;
					default:
						break;
				}
			}

			try
			{
				Sock.Shutdown(SocketShutdown.Both);
			}
			catch { }

			fDisconnecting = false;
		}
		#endregion EndDisconnect
	}
}
