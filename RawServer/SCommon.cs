using System;

namespace RawServer.Common
{
	public enum ClientStatuses
	{
		/// <summary>
		/// Клиент в статусе подключения
		/// </summary>
		Connecting,
		/// <summary>
		/// Клиенту утверждено подключение
		/// </summary>
		Connected,
		/// <summary>
		/// Клиент отключен
		/// </summary>
		ClientFree
	}

	/// <summary>
	/// Допустимые входящие события клиента
	/// </summary>
	public enum ClientActions
	{
		/// <summary>
		/// Нет активных подключений для выполнения действия
		/// </summary>
		NoConnections,
		/// <summary>
		/// Осуществляется подключение клиента
		/// </summary>
		Connecting,
		/// <summary>
		/// Команда о подключении клиента
		/// </summary>
		Connected,
		/// <summary>
		/// Команда об отключении клиента
		/// </summary>
		Disconnected,
		/// <summary>
		/// Входящее сообщение от клиента
		/// </summary>
		Receive,
		/// <summary>
		/// Отправленное сервером сообщение принято клиентом
		/// </summary>
		SendCompleted,
		/// <summary>
		/// Соединение азорвано удаленным пользователем
		/// </summary>
		Aborted,
		/// <summary>
		/// Соединение с клиентом потеряно
		/// </summary>
		Shutdown,
		/// <summary>
		/// Нельзя отправить пустой буффер
		/// </summary>
		ZeroBuffer,
		/// <summary>
		/// Неизвестное действие клиента при отправке сообщения
		/// </summary>
		UnknownSend
	}

	/// <summary>
	/// Допустимые исходящие команды сервера.
	/// </summary>
	public enum ServerCommands
	{
		/// <summary>
		/// Команда отправляет установленный буффер данных клиенту
		/// </summary>
		Send,
		/// <summary>
		/// Команда отключает клиента от сервера
		/// </summary>
		Disconnect
	}

	public class ClientEventArgs : EventArgs, IDisposable
	{
		/// <summary>
		/// Команда клиента.
		/// </summary>
		public ClientActions Command { get; set; }

		/// <summary>
		/// Полученное сообщения от клиента.
		/// </summary>
		public byte[] ReceiveBuffer { get; set; }

		/// <summary>
		/// Колличество получаемых байтов от сервера
		/// </summary>
		public int ReceiveBufferLength { get; set; }

		/// <summary>
		/// Объект клиента, от которого пришло событие
		/// </summary>
		public OnConnection Connection { get; set; }

		/// <summary>
		/// Возвращает значение, указывающее количество данных доступных для чтения
		/// </summary>
		public int Available { get; set; }

		public void Dispose()
		{
			ReceiveBuffer = null;
			ReceiveBufferLength = 0;
			Available = 0;

			Connection = null;
		}
	}

	public class ToServerCommand
	{
		/// <summary>
		/// Команда сервера.
		/// </summary>
		public ServerCommands Action { get; set; }

		/// <summary>
		/// Отправляемый буфер вместе с сообщением. Не обязательный параметр, при Actions.Disconnect.
		/// </summary>
		public byte[] ReceiveBuffer { get; set; }

		/// <summary>
		/// Установка списка клиентов, для которых предназначена команда. Не обязательный параметр, при этом команда будет отправлена всем клиентам.
		/// </summary>
		public OnConnection Client { get; set; }
	}
}
