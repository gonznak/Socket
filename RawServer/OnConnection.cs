using RawServer.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RawServer
{
	public delegate void OnClientCommand(ClientEventArgs clientCommand);
	public delegate void OnClientClosed(OnConnection clientConnection);

	public class OnConnection : IConnection
	{
		#region Events
		/// <summary>
		/// Принятый пакет данных от клиента, отправляется серверу на обработку
		/// </summary>
		protected event OnClientCommand ClientReceiveCommand;
		public event OnClientClosed ClientClosed;
		#endregion EndEvents

		#region Variables
		private ManualResetEventSlim mre_WaitLock;

		private Socket _socket { get; set; }
		private SocketAsyncEventArgs _sReceiveEventArgs;
		private SocketAsyncEventArgs _sSendEventArgs;
		private SocketAsyncEventArgs _sCloseArgs;

		private bool isPendingReceiveIO = false;
		private bool isPendingCloseIO = false;

		private AsyncOperation _aOperation;
		#endregion EndVariables

		#region Properties
		/// <summary>
		/// Адрес, с которого подключился клиент
		/// </summary>
		public EndPoint ClientEndPoint
		{
			get { return _socket?.RemoteEndPoint; }
		}

		/// <summary>
		/// Возвращает объем буфера входящих данных
		/// </summary>
		public int ReceiveBufferSize
		{
			get { return _sReceiveEventArgs.Buffer.Length; }
		}

		/// <summary>
		/// Возвращает статус подключения клиента
		/// </summary>
		public ClientStatuses ClientStatus { get; private set; }
		#endregion EndProperties

		#region .ctor Connection
		/// <summary>
		/// Инциализирует объект класса
		/// </summary>
		protected OnConnection()
		{
			ClientStatus = ClientStatuses.ClientFree;
			_aOperation = AsyncOperationManager.CreateOperation(null);                             // Доступ из параллельного потока

			mre_WaitLock = new ManualResetEventSlim(true, 10);

			_sReceiveEventArgs = new SocketAsyncEventArgs();
			_sReceiveEventArgs.Completed += ArgsHandler_Completed;

			_sSendEventArgs = new SocketAsyncEventArgs();
			_sSendEventArgs.Completed += ArgsHandler_Completed;

			_sCloseArgs = new SocketAsyncEventArgs();
			_sCloseArgs.Completed += ArgsHandler_Completed;
		}

		/// <summary>
		/// Разрешает дальнейшую работу с подключением
		/// </summary>
		protected void StartConnection()
		{
			ClientStatus = ClientStatuses.Connected;
			ClientAction(new ClientEventArgs() { Command = ClientActions.Connected, Connection = this });
		}

		/// <summary>
		/// Устанавливает точку подключения клиента
		/// </summary>
		/// <param name="AcceptedSocket">Сокет подключения клиента</param>
		internal void SetAcceptSocket(Socket AcceptedSocket)
		{
			if (ClientStatus != ClientStatuses.ClientFree)
				throw new InvalidOperationException("Установка нового сокета подключения невозможно, т.к. текущее подключение занято другим клиентом");

			ClientStatus = ClientStatuses.Connecting;
			_socket = AcceptedSocket ?? throw new ArgumentNullException("AcceptedSocket");

			ClientAction(new ClientEventArgs() { Command = ClientActions.Connecting, Connection = this });
		}

		private void ArgsHandler_Completed(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Disconnect:
					ProcessClose(e);
					return;
				case SocketAsyncOperation.Receive:
					if (!isPendingCloseIO)
						ProcessReceive(e);
					break;
				case SocketAsyncOperation.Send:
					ProcessSend(e);
					break;
			}
		}
		#endregion End.ctor

		#region Receive
		/// <summary>
		/// Запуск приема сообщение от клиента
		/// </summary>
		protected void RunReceive(int bufferSize)
		{
			if (isPendingReceiveIO || ClientStatus != ClientStatuses.Connected) return;

			try
			{
				_sReceiveEventArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
				ReceiveAsync(_sReceiveEventArgs);
			}
			catch (InvalidOperationException)
			{
				Console.WriteLine("RunReceive -> InvalidOperationException");
				return;
			}
			catch (Exception ex)
			{
				Console.WriteLine("RunReceive -> Exception");
				throw ex;
			}
		}

		private void ReceiveAsync(SocketAsyncEventArgs e)
		{
			isPendingReceiveIO = _socket.ReceiveAsync(e);

			if (!isPendingReceiveIO)
				ProcessReceive(e);
		}

		private void ProcessReceive(SocketAsyncEventArgs e)
		{
			isPendingReceiveIO = false;

			if (_socket == null || e.BytesTransferred == 0 && _socket.Available == 0)
			{
				Close();
				return;
			}

			switch (e.SocketError)
			{
				case SocketError.Success:
					if (e.BytesTransferred == 0)
					{
						_sReceiveEventArgs.SetBuffer(new byte[_socket.Available], 0, _socket.Available);
						ReceiveAsync(_sReceiveEventArgs);
					}
					else
						ClientAction(new ClientEventArgs() { Command = ClientActions.Receive, Connection = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
					break;
				default:
					Debug.WriteLine("{0}: Необработанное исключение. Событие {1}", "ProcessReceive", e.SocketError.ToString());
					break;
			}
		}
		#endregion

		#region Send
		public bool Send(byte[] data)
		{
			if (ClientStatus != ClientStatuses.Connected) return false;

			if (data == null || data.Length == 0)
			{
				ClientAction(new ClientEventArgs() { Command = ClientActions.ZeroBuffer });
				return false;
			}

			mre_WaitLock.Wait();
			mre_WaitLock.Reset();

			_sSendEventArgs.SetBuffer(data, 0, data.Length);
			SendAsync(_sSendEventArgs);

			return true;
		}

		private void SendAsync(SocketAsyncEventArgs e)
		{
			if (!_socket.SendAsync(e))
				ProcessSend(e);
		}

		private void ProcessSend(SocketAsyncEventArgs e)
		{
			mre_WaitLock.Set();

			switch (e.SocketError)
			{
				case SocketError.Success:
					ClientAction(new ClientEventArgs() { Command = ClientActions.SendCompleted, Connection = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
					break;
				case SocketError.NotConnected:
					Close();
					break;
				case SocketError.ConnectionAborted:
					ClientAction(new ClientEventArgs() { Command = ClientActions.Aborted, Connection = this });
					break;
				case SocketError.ConnectionReset:
					ClientAction(new ClientEventArgs() { Command = ClientActions.Shutdown, Connection = this });
					break;
				default:
					ClientAction(new ClientEventArgs() { Command = ClientActions.UnknownSend, Connection = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
					Debug.WriteLine("{0}: Необработанное исключение. Событие {1}", "ProcessSend", e.SocketError.ToString());
					break;
			}
		}
		#endregion

		#region Disconnect
		/// <summary>
		/// Отправка команды на отключение клиента от сервера
		/// </summary>
		/// <param name="Command">Объект сообщения,если параметр ToClient не определен, сообщение отправляется всем подключенным клиентам </param>
		public void Close()
		{
			if (isPendingCloseIO || ClientStatus == ClientStatuses.ClientFree) return;
			CloseAsync(_sCloseArgs);
		}

		private void CloseAsync(SocketAsyncEventArgs e)
		{
			try
			{
				isPendingCloseIO = _socket.DisconnectAsync(e);

				if (!isPendingCloseIO)
					ProcessClose(e);
			}
			catch {
				Console.WriteLine("CloseAsync");
			}
		}

		private void ProcessClose(SocketAsyncEventArgs e)
		{
			_socket.Shutdown(SocketShutdown.Receive);
			_socket.Close(100);
			_socket.Dispose();

			switch (e.SocketError)
			{
				case SocketError.Success:
					ClientAction(new ClientEventArgs() { Command = ClientActions.Disconnected, Connection = this });
					break;
				default:
					Debug.WriteLine("{0}: Необработанное исключение. Событие {1}", "ProcessClose", e.SocketError.ToString());
					break;
			}

			ClientClosed?.Invoke(this);

			isPendingCloseIO = false;
		}
		#endregion EndDisconnect

		#region Handling server commands
		/// <summary>
		/// Принятое событие от клиента
		/// </summary>
		/// <param name="Command">Объект команды</param>
		private void ClientAction(ClientEventArgs Command)
		{
			if (ClientReceiveCommand != null)
			{
				// выполняется в основном потоке
				SendOrPostCallback cb = state => ClientReceiveCommand(Command);
				_aOperation.Post(cb, null);
			}
		}

		public virtual void ServerActions(ToServerCommand Command)
		{
			throw new Exception("Need override function ServerActions in base class OnConnection");
		}
		#endregion End Handling

		#region Pool
		public virtual void CleanUp()
		{
			ClientStatus = ClientStatuses.ClientFree;

			isPendingReceiveIO = false;
			//isPendingSendIO = false;
			isPendingCloseIO = false;

			mre_WaitLock.Set();

			_socket = null;
			isPendingCloseIO = false;
		}
		#endregion End Pool
	}
}
