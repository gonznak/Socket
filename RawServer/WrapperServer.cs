using Pool;
using RawServer.Common;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RawServer
{
	public delegate void OnAcceptClient<T>(T acceptClient);
	public delegate void OnServerCommand(ToServerCommand Command);


	public class Server<T> where T : OnConnection, IPoolSlotHolder<T>
	{
		#region Events
		/// <summary>
		/// Происходит при удачном подключении клиента к серверу
		/// </summary>
		public event OnAcceptClient<T> Status;                                  // События сервера
		private event OnServerCommand _eServerCommand;                          // Событие отправки данных клиенту
		#endregion

		#region Variables
		private Socket _socket;                                                 // Сокет сервера
		private SocketAsyncEventArgs _sAcceptEvent;                             // Асинхронный приемник подключений
		private AsyncOperation _aOperation;                                     // Для много поточности
		private ConnectionPool<T> _clientPool = null;
		private CancellationTokenSource _cancelToken = null;
		#endregion

		#region Properties
		/// <summary>
		/// Статус сервера
		/// </summary>
		/// 
		public bool IsStarted { get; private set; }

		/// <summary>
		/// Параметры работы сервера
		/// </summary>
		public SConfiguration Configuration { get; private set; }
		#endregion

		#region .ctor
		/// <summary>
		/// Создание объекта сервера
		/// </summary>
		public Server(SConfiguration config)
		{
			if (config == null) throw new ArgumentException("Argument \"config\" is NULL");

			this.Configuration = config;

			_aOperation = AsyncOperationManager.CreateOperation(null);          // Доступ из параллельного потока
		}
		#endregion

		#region Start | Stop
		/// <summary>
		/// Запустить сервер
		/// </summary>
		public void Start()
		{
			if (IsStarted)
				return;

			ApplySettings();

			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_socket.Bind(new IPEndPoint(Configuration.Address, Configuration.Port));
			_socket.Listen(100);

			_cancelToken = new CancellationTokenSource();

			_sAcceptEvent = new SocketAsyncEventArgs();
			_sAcceptEvent.Completed += AcceptCompleted;

			if (_cancelToken.IsCancellationRequested) return;
			AcceptAsync(_sAcceptEvent);

			IsStarted = true;
		}

		/// <summary>
		/// Остановить сервер
		/// </summary>
		public void Stop()
		{
			if (!IsStarted)
				return;

			_cancelToken.Cancel();

			_eServerCommand?.Invoke(new ToServerCommand() { Action = ServerCommands.Disconnect });

			_socket.Close();
			_socket.Dispose();
			_socket = null;

			_sAcceptEvent.Completed -= AcceptCompleted;

			_clientPool.WaitAll();

			_sAcceptEvent.Dispose();
			_sAcceptEvent = null;

			IsStarted = false;
		}
		#endregion

		#region Configuration
		private void ApplySettings()
		{
			_clientPool = new ConnectionPool<T>(Configuration.PoolInitializedSize, Configuration.PoolConnectionSize);
		}
		#endregion

		#region Обработка входящих подключение
		private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
		{
			var client = e;

			switch (e.SocketError)
			{
				case SocketError.Success:
					if (_clientPool.CurrentCount == 0 && Configuration.PoolConnectionWait == false)
					{
						e.AcceptSocket.Close();
					}
					else
					{
						var recieveArgs = _clientPool.TakeObject();
						recieveArgs.ClientClosed += ClientConnection_ClientDisconnected;
						recieveArgs.SetAcceptSocket(e.AcceptSocket);
						this._eServerCommand += recieveArgs.ServerActions;

						OpenInternal(recieveArgs);
					}
					break;
				case SocketError.OperationAborted:
					break;
				default:
					break;
			}
			e.AcceptSocket = null;
			AcceptAsync(_sAcceptEvent);
		}

		void ClientConnection_ClientDisconnected(OnConnection clientConnection)
		{
			this._eServerCommand -= clientConnection.ServerActions;
			clientConnection.ClientClosed -= ClientConnection_ClientDisconnected;
			_clientPool.Release((T)clientConnection);
		}

		/// <summary>
		/// Начало приема нового подключения
		/// </summary>
		/// <param name="e">Приемник подключений</param>
		private void AcceptAsync(SocketAsyncEventArgs e)
		{
			try
			{
				bool willRaiseEvent = _socket.AcceptAsync(e);
				if (!willRaiseEvent)
					AcceptCompleted(_socket, e);
			}
			catch { }
		}
		#endregion

		#region Взаимодействие с клиентами
		/// <summary>
		/// Производит отправку сообщения всем подключенным клиентам
		/// </summary>
		/// <param name="Command"></param>
		public void Send(ToServerCommand Command)
		{
			// ОТКЛЮЧЕНИЕ
			// При команде на отключение, включить таймер ожидания на отключение (один для всех) (привязка один для многих),
			// если через заданный промежуток времени не происходит отключение, разрывать связь принудительно

			if (IsStarted)
			{
				_eServerCommand?.Invoke(Command);
			}
		}
		#endregion

		#region AsyncEvent
		private void OpenInternal(T acceptClient)
		{
			if (Status != null)
			{
				// выполняется в основном потоке
				SendOrPostCallback cb = state => Status(acceptClient);
				_aOperation.Post(cb, null);
			}
		}
		#endregion
	}
}
