using RawServer.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RawServer
{
	public delegate void OnClientCommand(FromClientCommand Command);
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
		private ManualResetEventSlim mre_waiterLock;

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
		/// Возвращает статус клиента
		/// </summary>
		public bool IsAccepted
		{
			get { return ClientEndPoint != null; }
		}

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
		#endregion EndProperties

		#region .ctor Connection
		/// <summary>
		/// Инциализирует объект класса
		/// </summary>
		protected OnConnection()
		{
			_aOperation = AsyncOperationManager.CreateOperation(null);                             // Доступ из параллельного потока

			mre_waiterLock = new ManualResetEventSlim(true, 10);

			_sReceiveEventArgs = new SocketAsyncEventArgs();
			_sReceiveEventArgs.Completed += ArgsHandler_Completed;

			_sSendEventArgs = new SocketAsyncEventArgs();
			_sSendEventArgs.Completed += ArgsHandler_Completed;

			_sCloseArgs = new SocketAsyncEventArgs();
			_sCloseArgs.Completed += ArgsHandler_Completed;
		}

		/// <summary>
		/// Устанавливает точку подключения клиента
		/// </summary>
		/// <param name="AcceptedSocket">Сокет подключения клиента</param>
		internal void SetAcceptSocket(Socket AcceptedSocket)
		{
			if (AcceptedSocket == null)
				throw new ArgumentNullException("AcceptedSocket");

			_socket = AcceptedSocket;

			ClientAction(new FromClientCommand() { Command = IsAccepted ? ClientActions.Connected : ClientActions.NoConnections, Client = this });
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
		protected void StartReceive(int bufferSize)
		{
			if (isPendingReceiveIO || !IsAccepted) return;

			try
			{
				_sReceiveEventArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
				ReceiveAsync(_sReceiveEventArgs);
			}
			catch (InvalidOperationException)
			{
				return;
			}
			catch (Exception ex)
			{
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
						ClientAction(new FromClientCommand() { Command = ClientActions.Receive, Client = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
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
			if (!IsAccepted) return false;

			if (data == null || data.Length == 0)
			{
				ClientAction(new FromClientCommand() { Command = ClientActions.ZeroBuffer });
				return false;
			}

			mre_waiterLock.Wait();
			mre_waiterLock.Reset();

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
			mre_waiterLock.Set();

			switch (e.SocketError)
			{
				case SocketError.Success:
					ClientAction(new FromClientCommand() { Command = ClientActions.SendCompleted, Client = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
					break;
				case SocketError.NotConnected:
					Close();
					break;
				case SocketError.ConnectionAborted:
					ClientAction(new FromClientCommand() { Command = ClientActions.Aborted, Client = this });
					break;
				case SocketError.ConnectionReset:
					ClientAction(new FromClientCommand() { Command = ClientActions.Shutdown, Client = this });
					break;
				default:
					ClientAction(new FromClientCommand() { Command = ClientActions.UnknownSend, Client = this, ReceiveBuffer = e.Buffer, ReceiveBufferLength = e.BytesTransferred, Available = _socket.Available });
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
			if (isPendingCloseIO || !IsAccepted) return;
			CloseAsync(_sCloseArgs);
		}

		private void CloseAsync(SocketAsyncEventArgs e)
		{
			isPendingCloseIO = _socket.DisconnectAsync(e);

			if (!isPendingCloseIO)
				ProcessClose(e);
		}

		private void ProcessClose(SocketAsyncEventArgs e)
		{
			_socket.Shutdown(SocketShutdown.Receive);
			_socket.Close(100);
			_socket.Dispose();

			switch (e.SocketError)
			{
				case SocketError.Success:
					ClientAction(new FromClientCommand() { Command = ClientActions.Disconnected, Client = this });
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
		private void ClientAction(FromClientCommand Command)
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
			isPendingReceiveIO = false;
			//isPendingSendIO = false;
			isPendingCloseIO = false;

			mre_waiterLock.Set();

			_socket = null;
			isPendingCloseIO = false;
		}
		#endregion End Pool
	}
}
