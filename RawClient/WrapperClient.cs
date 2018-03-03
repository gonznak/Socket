using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RawClient.Common;

namespace RawClient
{
	public delegate void OnServerReceiveCommand(FromServerCommand Command);
	public delegate void OnClientCommand(ToClientCommand Command);
	public delegate void OnServerCommand(FromServerCommand Command);

	public class Client
	{
		#region 1
		private AsyncOperation _AsyncOperation;

		private Socket Sock;
		private SocketAsyncEventArgs AcceptAsyncArgs;

		private OnClientConnection ClientConnection;


		/// <summary>
		/// Событие входящих сообщений от сервера
		/// </summary>
		public event OnServerReceiveCommand ServerReceiveCommand;


		private bool localDisconnect = false;
		private bool localBlockConnect = false;
		private bool isSetBuffer = false;
		#endregion

		#region Свойства
		private IPEndPoint m_RemotePoint;
		private bool m_isConnected = false;
		private byte[] m_OnConnectBuffer = new byte[0];
		private int m_ReceiveBufferSize = 0xFA00;


		/// <summary>
		/// Удаленная точка, к которой идет подключение
		/// </summary>
		public IPEndPoint RemotePoint
		{
			get { return m_RemotePoint; }
			private set { m_RemotePoint = value; }
		}

		/// <summary>
		/// Возвращает состояние подключения
		/// </summary>
		public bool isConnected
		{
			get { return m_isConnected; }
			set
			{
				if (m_isConnected == value)
					return;
				m_isConnected = value;
				if (value)
				{
					ClientConnection.ServerReceiveCommand += new OnServerCommand(ServerConnection_ServerReceiveCommand);
				}
				else
				{
					ClientConnection.ServerReceiveCommand -= new OnServerCommand(ServerConnection_ServerReceiveCommand);
				}
			}
		}

		/// <summary>
		/// Возвращает или задает буффер данных, который будет отправлен при успешном подключении
		/// </summary>
		public byte[] OnConnectBuffer
		{
			get { return this.m_OnConnectBuffer; }
			private set { this.m_OnConnectBuffer = value; }
		}

		/// <summary>
		/// Возвращает смещение (в байтах) в буфере данных, при котором начинается операция.
		/// </summary>
		public int OnConnectBufferOffset
		{
			get;
			private set;
		}

		/// <summary>
		/// Возвращает максимальное количество данных (в байтах), которое может быть отправлено или получено в буфере.
		/// </summary>
		public int OnConnectBufferCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Максимальный размер входящего буфера
		/// </summary>
		public int ReceiveBufferSize
		{
			get
			{
				if (isConnected)
					return ClientConnection.ReceiveBufferSize;
				else
					return m_ReceiveBufferSize;
			}
			set { m_ReceiveBufferSize = value; }
		}

		/// <summary>
		/// Пользовательская переменная
		/// </summary>
		public object UserToken { get; set; }
		#endregion

		#region Proxy свойства
		private bool m_ProxyUse = false;
		private IPEndPoint m_ProxyPoint = null;
		private string m_ProxyLogin = String.Empty;
		private string m_ProxyPassword = String.Empty;
		private bool m_ProxyAuth = false;

		/// <summary>
		/// Возвращает или задает флаг применения прокси сервера для установки подключения
		/// </summary>
		public bool ProxyUse
		{
			get { return m_ProxyUse; }
			set { m_ProxyUse = value; }
		}

		/// <summary>
		/// Адрес прокси сервера
		/// </summary>
		public IPEndPoint ProxyPoint
		{
			get { return m_ProxyPoint; }
			set { m_ProxyPoint = value; }
		}

		/// <summary>
		/// Логин авторизации на прокси сервере
		/// </summary>
		public string ProxyLogin
		{
			get { return m_ProxyLogin; }
			set { m_ProxyLogin = value; }
		}

		/// <summary>
		/// Пароль авторизации на прокси сервере
		/// </summary>
		public string ProxyPassword
		{
			get { return m_ProxyPassword; }
			set { m_ProxyPassword = value; }
		}

		/// <summary>
		/// Использовать авторизацию для прокси сервера
		/// </summary>
		public bool ProxyAuth
		{
			get { return m_ProxyAuth; }
			set { m_ProxyAuth = value; }
		}
		#endregion

		#region 2
		/// <summary>
		/// Устанавливает буфер, который будет отправлен при подключении
		/// </summary>
		/// <param name="buffer">Буффер данных, который будет отправлен при успешном подключении</param>
		/// <param name="offset">Смещение (в байтах) в буфере данных, при котором начинается операция.</param>
		/// <param name="count">Максимальное количество данных (в байтах), которое может быть отправлено или получено в буфере.</param>
		public void SetConnectBuffer(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentException("Неоднозначное указание буфера");

			if (((offset + count) > buffer.Length) || offset < 0 || count < 0)
				throw new ArgumentOutOfRangeException("Смещение offset и count не должны превышать размер буфера или быть отрицательными");

			this.OnConnectBuffer = buffer;
			this.OnConnectBufferOffset = offset;
			this.OnConnectBufferCount = count;

			isSetBuffer = true;
		}

		/// <summary>
		/// Создание объекта сервера
		/// </summary>
		public Client()
		{
			Sock = CreateSocket();
			AcceptAsyncArgs = new SocketAsyncEventArgs();
			AcceptAsyncArgs.Completed += AcceptCompleted;
			// Обеспечивает доступ из параллельного потока
			_AsyncOperation = AsyncOperationManager.CreateOperation(null);
		}

		private Socket CreateSocket()
		{
			return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		}

		/// <summary>
		/// Начало установки соединения с удаленной точкой
		/// </summary>
		public void Connect()
		{
			if (m_RemotePoint == null)
				throw new NotImplementedException("Переменная RemotePoint не объявлена");

			Connect(m_RemotePoint, m_OnConnectBuffer);
		}

		/// <summary>
		/// Начало установки соединения с удаленной точкой
		/// </summary>
		/// <param name="RemotePoint"></param>
		public void Connect(IPEndPoint RemotePoint)
		{
			Connect(RemotePoint, m_OnConnectBuffer);
		}

		/// <summary>
		/// Начало установки соединения с удаленной точкой
		/// </summary>
		/// <param name="RemotePoint">Адрес удаленной точки, куда следует подключиться</param>
		/// <param name="ConnectBuffer">Буфер, который будет отправлен при подключении</param>
		public void Connect(IPEndPoint RemotePoint, byte[] ConnectBuffer)
		{
			if (isSetBuffer)
				Connect(RemotePoint, ConnectBuffer, this.OnConnectBufferOffset, this.OnConnectBufferCount);
			else
				Connect(RemotePoint, ConnectBuffer, 0, ConnectBuffer.Length);
		}

		/// <summary>
		/// Начало установки соединения с удаленной точкой
		/// </summary>
		/// <param name="RemotePoint">Адрес удаленной точки, куда следует подключиться</param>
		/// <param name="ConnectBuffer">Буфер, который будет отправлен при подключении</param>
		/// <param name="bufferOffset">Смещение (в байтах) в буфере данных, при котором начинается операция.</param>
		/// <param name="bufferCount">Максимальное количество данных (в байтах), которое может быть отправлено или получено в буфере.</param>
		public void Connect(IPEndPoint RemotePoint, byte[] ConnectBuffer, int bufferOffset, int bufferCount)
		{
			if (!isConnected && !localBlockConnect)
			{
				localBlockConnect = true;
				LingerOption LOpt = new LingerOption(true, 0);
				Sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
				Sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, (object)LOpt);

				this.RemotePoint = RemotePoint;
				this.OnConnectBuffer = ConnectBuffer;
				this.OnConnectBufferOffset = bufferOffset;
				this.OnConnectBufferCount = bufferCount;

				IPEndPoint mEndPoint;
				byte[] Buffer;
				int BufferOffset = 0;
				int BufferCount = 0;
				SelectConnection(out mEndPoint, out Buffer, out BufferOffset, out BufferCount);

				AcceptAsyncArgs.RemoteEndPoint = mEndPoint;
				AcceptAsyncArgs.SetBuffer(Buffer, BufferOffset, BufferCount);
				AcceptAsyncArgs.UserToken = UserToken;

				ConnectAsync(AcceptAsyncArgs);
				ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = m_ProxyUse ? ServerActions.ConnectingToProxy : ServerActions.Connecting, UserToken = AcceptAsyncArgs.UserToken });
			}
			else
			{
				if (isConnected)
				{
					ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = ServerActions.AlreadyConnected, UserToken = AcceptAsyncArgs.UserToken });
				}
				else if (localBlockConnect)
				{
					ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = ServerActions.ProcessConnection, UserToken = AcceptAsyncArgs.UserToken });
				}
			}
		}

		private void SelectConnection(out IPEndPoint EndPoint, out byte[] Buffer, out int BufferOffset, out int BufferCount)
		{
			if (m_ProxyUse && m_ProxyPoint != null)
			{
				string Connect = "CONNECT " + m_RemotePoint.Address + ":" + m_RemotePoint.Port + " HTTP/1.0\r\n";
				string Agent = "User-Agent: Mozilla/4.08 [en] (WinNT; U)\r\n";
				string Proxy_Authorization = String.Empty;

				// Если используем авторизацию на прокси сервере
				if (m_ProxyAuth && (!String.IsNullOrEmpty(m_ProxyLogin) && !String.IsNullOrEmpty(ProxyPassword)))
				{
					Proxy_Authorization = "Proxy-Authorization: Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(m_ProxyLogin + ":" + m_ProxyPassword)) + "\r\n";
				}

				string sConnectProxyBuffer = Connect + Agent + Proxy_Authorization + "\r\n";
				byte[] abConnectProxyBuffer = System.Text.Encoding.ASCII.GetBytes(sConnectProxyBuffer);

				EndPoint = m_ProxyPoint;
				Buffer = abConnectProxyBuffer;
				BufferOffset = 0;
				BufferCount = abConnectProxyBuffer.Length;
			}
			else
			{
				EndPoint = m_RemotePoint;
				Buffer = m_OnConnectBuffer;
				BufferOffset = this.OnConnectBufferOffset;
				BufferCount = this.OnConnectBufferCount;
			}
		}
		#endregion

		private void ConnectAsync(SocketAsyncEventArgs e)
		{
			if (!Sock.ConnectAsync(e))
			{
				ProcessConnect(e);
			}
		}

		private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Connect:
					ProcessConnect(e);
					return;
			}
		}


		private void ProcessConnect(SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				ClientConnection = new OnClientConnection(e.ConnectSocket, m_ProxyUse, m_OnConnectBuffer, m_ReceiveBufferSize, e.UserToken);

				if (m_ProxyUse)
				{
					ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = ServerActions.ConnectedToProxy, UserToken = e.UserToken });
				}
				else
				{
					ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = ServerActions.Connected, UserToken = e.UserToken });
				}
			}
			else
			{
				ServerConnection_ServerReceiveCommand(new FromServerCommand() { Action = ServerActions.ConnectionFailed, ReceiveBuffer = System.Text.Encoding.ASCII.GetBytes(e.SocketError.ToString()), ReceiveBufferLength = e.SocketError.ToString().Length, UserToken = e.UserToken });
				//OpenInternal(new FromServerCommand() { Action = FromServerCommand.Actions.ConnectionFailed, ReceiveBuffer = System.Text.Encoding.ASCII.GetBytes(e.SocketError.ToString()), ReceiveBufferLength = e.SocketError.ToString().Length });
			}
			localBlockConnect = false;
		}

		#region 3
		/// <summary>
		/// Отправить сообщение на сервер
		/// </summary>
		/// <param name="Command"></param>
		public void SendCommand(ToClientCommand Command)
		{
			if (ClientConnection != null)
			{
				if (Command.Command == ClientCommands.Disconnect)
				{
					localDisconnect = true;
					isSetBuffer = false;
					OnConnectBuffer = new byte[] { };
					OnConnectBufferCount = 0;
					OnConnectBufferOffset = 0;
				}
				ClientConnection.Command(Command);
			}
		}

		/// <summary>
		/// Внутренняя обработка списка подключенных клиентов
		/// </summary>
		/// <param name="Command">Входящее сообщение от килента</param>
		private void ServerConnection_ServerReceiveCommand(FromServerCommand Command)
		{
			if ((Command.Action == ServerActions.Disconnected || Command.Action == ServerActions.ConnectionFailed) && localDisconnect)
			{
				localDisconnect = false;

				Sock.Close();
				//Sock.Dispose();

				Sock = CreateSocket();
			}

			OpenInternal(Command);
			switch (Command.Action)
			{
				case ServerActions.Connected:
				case ServerActions.ConnectedToProxy:
					isConnected = true;
					break;
				case ServerActions.ConnectionFailed:
				case ServerActions.Disconnected:
				case ServerActions.ProxyAuthFailed:
				case ServerActions.Shutdown:
					isConnected = false;
					ClientConnection = null;
					break;
				default:
					break;
			}
		}

		private void OpenInternal(FromServerCommand Command)
		{
			if (ServerReceiveCommand != null)
			{
				// выполняется в основном потоке
				SendOrPostCallback cb = state => ServerReceiveCommand(Command);
				_AsyncOperation.Post(cb, null);
			}
		}
		#endregion
	}
}