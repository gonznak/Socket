using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SocketWrapper.ClientCommon;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net;
using System.Threading;

namespace SocketWrapper
{
	public class Client222
	{
		#region Событие
		private AsyncOperation _AsyncOperation;
		public delegate void OnClientCommand(FromServerCommand Command);

		/// <summary>
		/// Принятый пакет данных от сервера, отправляется клиенту на обработку
		/// </summary>
		public event OnClientCommand ServerReceiveCommand;
		#endregion

		private Socket Sock;
		private SocketAsyncEventArgs SockAsyncEventArgs_Connect;
		private SocketAsyncEventArgs SockAsyncEventArgs_Disconnect;
		private SocketAsyncEventArgs SockAsyncEventArgs_Send;
		private SocketAsyncEventArgs SockAsyncEventArgs_Receive;

		private bool fDisconnecting = false;
		private bool fConnecting = false;
		private bool isConnected = false;
		private bool ConnectingOverProxy = false;


		#region Proxy свойства
		private bool m_ProxyUse = false;
		private EndPoint m_ProxyPoint = null;
		private string m_ProxyLogin = String.Empty;
		private string m_ProxyPassword = String.Empty;
		private bool m_ProxyAuth = false;

		/// <summary>
		/// Возвращает или задает флаг применения прокси сервера для установки подключения
		/// </summary>
		public bool ProxyUse
		{
			get
			{
				return m_ProxyUse;
			}
			set
			{
				m_ProxyUse = value;
			}
		}

		/// <summary>
		/// Адрес прокси сервера
		/// </summary>
		public EndPoint ProxyPoint
		{
			get
			{
				return m_ProxyPoint;
			}
			set
			{
				m_ProxyPoint = value;
			}
		}

		/// <summary>
		/// Логин авторизации на прокси сервере
		/// </summary>
		public string ProxyLogin
		{
			get
			{
				return m_ProxyLogin;
			}
			set
			{
				m_ProxyLogin = value;
			}
		}

		/// <summary>
		/// Пароль авторизации на прокси сервере
		/// </summary>
		public string ProxyPassword
		{
			get
			{
				return m_ProxyPassword;
			}
			set
			{
				m_ProxyPassword = value;
			}
		}

		/// <summary>
		/// Использовать авторизацию для прокси сервера
		/// </summary>
		public bool ProxyAuth
		{
			get
			{
				return m_ProxyAuth;
			}
			set
			{
				m_ProxyAuth = value;
			}
		}
		#endregion


		#region Свойства
		private DnsEndPoint m_RemotePoint;
		private byte[] _setOnConnectBuffer = new byte[0];
		private byte[] abBuffer = new byte[8192];

		/// <summary>
		/// Возвращает или задает буффер данных, который будет отправлен при успешном подключении
		/// </summary>
		public byte[] OnConnectBuffer
		{
			get
			{
				return this._setOnConnectBuffer;
			}
			set
			{
				if (this._setOnConnectBuffer == value) return;

				if (this._setOnConnectBuffer.Length != this.abBuffer.Length)
				{
					Array.Copy(value, this._setOnConnectBuffer, this.abBuffer.Length);
				}
				else
				{
					this._setOnConnectBuffer = value;
				}
			}
		}

		/// <summary>
		/// Удаленная точка, к которой идет подключение
		/// </summary>
		public DnsEndPoint RemotePoint
		{
			get
			{
				return m_RemotePoint;
			}
			private set
			{
				m_RemotePoint = value;
			}
		}
		#endregion

		#region Handling client commands
		public void SendCommand(ToClientCommand Command)
		{
			HandlingServerCommands(Command);
		}

		void HandlingServerCommands(ToClientCommand Command)
		{
			switch (Command.Command)
			{
				case ToClientCommand.Commands.Send:
					ActionSend(Command);
					break;
				case ToClientCommand.Commands.Disconnect:
					ActionDisconnect(Command);
					break;
				default:
					break;
			}
		}

		private void ActionSend(ToClientCommand Command)
		{
			// выполнить только, если клиент подключился к удаленной точке
			//if (Command.ToClient.Contains(this) || Command.ToClient.Count == 0)
			{
				Send(Command.ReceiveBuffer);
			}
		}

		private void ActionDisconnect(ToClientCommand Command)
		{

		}
		#endregion


		#region .ctor
		/// <summary>
		/// Инициализирует новый экземпляр класса ClientEngine
		/// </summary>
		public Client222()
		{
			_AsyncOperation = AsyncOperationManager.CreateOperation(null);

			Sock = CreateSocket();

			SockAsyncEventArgs_Connect = new SocketAsyncEventArgs();
			SockAsyncEventArgs_Connect.UserToken = "CSocketAsyncEventArgs_Connect";
			SockAsyncEventArgs_Connect.Completed += SockAsyncEventArgs_Completed;
			SockAsyncEventArgs_Connect.DisconnectReuseSocket = true;
		}

		private Socket CreateSocket()
		{
			return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}
		#endregion

		private void SockAsyncEventArgs_Completed(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Connect:
					if (!(fDisconnecting || !fConnecting))
					{
						ProcessConnect(e);
					}
					return;

				case SocketAsyncOperation.Disconnect:
					if (!(!fDisconnecting || fConnecting))
					{
						ProcessDisconnect(e);
					}
					return;

				case SocketAsyncOperation.Receive:
					if (!fDisconnecting)				// Если не отключаемся
					{
						ProcessReceive(e);
					}
					return;

				case SocketAsyncOperation.Send:
					ProcessSend(e);
					return;
			}
		}


		#region Connect
		/// <summary>
		/// Начало установки соединения с удаленной точкой
		/// </summary>
		/// <param name="RemotePoint">Адрес удаленной точки, куда следует подключиться</param>
		public void Connect(DnsEndPoint RemotePoint)
		{
			this.m_RemotePoint = RemotePoint;

			fConnecting = true;

			EndPoint EndPoint;
			byte[] ConnectBuffer;
			SelectConnection(out EndPoint, out ConnectBuffer);

			SockAsyncEventArgs_Connect.RemoteEndPoint = EndPoint;
			SockAsyncEventArgs_Connect.SetBuffer(ConnectBuffer, 0, ConnectBuffer.Length);
			ConnectAsync(SockAsyncEventArgs_Connect);
		}

		private void SelectConnection(out EndPoint EndPoint, out byte[] Buffer)
		{
			if (m_ProxyUse && !ConnectingOverProxy)
			{
				// Подключаемся через прокси сервер
				ConnectingOverProxy = true;

				string Connect = "CONNECT " + m_RemotePoint.Host + ":" + m_RemotePoint.Port + " HTTP/1.0\r\n";
				string Agent = "User-Agent: Mozilla/4.08 [en] (WinNT; U)\r\n";
				string Proxy_Authorization = String.Empty;

				// Если используем авторизацию на прокси сервере
				if (m_ProxyAuth && (!String.IsNullOrEmpty(m_ProxyLogin) && !String.IsNullOrEmpty(ProxyPassword)))
				{
					Proxy_Authorization = "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(m_ProxyLogin + ":" + m_ProxyPassword)) + "\r\n";
				}

				string sConnectProxyBuffer = Connect + Agent + Proxy_Authorization + "\r\n";
				byte[] abConnectProxyBuffer = Encoding.ASCII.GetBytes(sConnectProxyBuffer);

				EndPoint = m_ProxyPoint;
				Buffer = abConnectProxyBuffer;
			}
			else
			{
				EndPoint = m_RemotePoint;
				Buffer = _setOnConnectBuffer;
			}
		}

		private void ConnectAsync(SocketAsyncEventArgs e)
		{
			if (!Sock.ConnectAsync(e))
			{
				ProcessConnect(e);
			}
		}

		private void ProcessConnect(SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				if (!m_ProxyUse) isConnected = true;
				SockAsyncEventArgs_Receive = new SocketAsyncEventArgs();
				SockAsyncEventArgs_Receive.UserToken = "CSocketAsyncEventArgs_Receive.ProcessConnect";
				SockAsyncEventArgs_Receive.SetBuffer(abBuffer, 0, abBuffer.Length);
				SockAsyncEventArgs_Receive.Completed += SockAsyncEventArgs_Completed;
				ReceiveAsync(SockAsyncEventArgs_Receive);
				if (!m_ProxyUse)
				{
					OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.Connected });
				}
			}
			else
			{
				isConnected = false;
				OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.ConnectionFailed, ReceiveBuffer = Encoding.ASCII.GetBytes(e.SocketError.ToString()) });
			}
			fConnecting = false;
		}
		#endregion

		#region Disconnect
		/// <summary>
		/// Начинает отключение сокета от удаленной точки
		/// </summary>
		private void Disconnect()
		{
			if (!fDisconnecting && !fConnecting)
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
			if (!Sock.DisconnectAsync(e))
			{
				ProcessDisconnect(e);
			}
			else
			{
				///////ServerReceiveCommand
				//ClientAction(new ClientCommand() { Command = ClientCommand.Commands.Disconnect });
			}
		}

		private void ProcessDisconnect(SocketAsyncEventArgs e)
		{
			fDisconnecting = false;
			isConnected = false;
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
				// Проверка на разрыв соединения с сервером
				if (Sock.Poll(10000, SelectMode.SelectRead))
				{
					OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.Disconnected });
				}
				else
				{
					OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.UnknownRecevie });
					// На всякий случай остаемся на связи и слушаем клиента
					ReceiveAsync(SockAsyncEventArgs_Receive);
				}
			}
			else
			{
				if (e.SocketError == SocketError.Success)
				{
					// Передаем принятое сообщение серверу
					byte[] abReceiveBuffer = new byte[e.BytesTransferred];
					Array.Copy(e.Buffer, abReceiveBuffer, e.BytesTransferred);

					if (ConnectingOverProxy)
					{
						ValidateProxyResponse(abReceiveBuffer);
					}
					else
					{
						OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.Receive, ReceiveBuffer = abReceiveBuffer });
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
						ConnectingOverProxy = false;
						isConnected = true;
						OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.Connected });
						Send(OnConnectBuffer);
						break;
					case "404":		// Невозможно установить соединение
						ConnectingOverProxy = false;
						Disconnect();
						OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.ConnectionFailed });
						break;
					case "407":		// Необхордимо авторизация на прокси сервере
						//ProxyAutorization = true;
						Disconnect();
						OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.ProxyAuthFailed });
						//this.Connect(this.RemotePoint);
						//this.Send(abConnectProxyBuffer);
						break;
					default:		// Неизвестный тип ответа
						ConnectingOverProxy = false;
						Disconnect();
						OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.ConnectionFailed });
						break;
				}
			}

			if (ConnectingOverProxy)
			{
				ConnectingOverProxy = false;
				Disconnect();
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

		#region Send

		private void Send(byte[] data)
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
					//ClientAction(new ClientCommand() { Command = ClientCommand.Commands.SendCompleted, Client = this, ReceiveBuffer = e.Buffer });
				}
				else if (e.SocketError == SocketError.NotConnected)
				{
					//ActionDisconnect(new ServerCommand() { Action = ServerCommand.Actions.Disconnect, ToClient = new List<ClientConnection> { this } });
				}
				else
				{
					//ClientAction(new ClientCommand() { Command = ClientCommand.Commands.UnknownSend, Client = this, ReceiveBuffer = e.Buffer });
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}
		#endregion


		private void OpenInternal(FromServerCommand Command)
		{
			if (ServerReceiveCommand != null)
			{
				// выполняется в основном потоке
				SendOrPostCallback cb = state => ServerReceiveCommand(Command);
				_AsyncOperation.Post(cb, null);
			}
		}
	}
}