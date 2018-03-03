namespace RawServer.Common
{
	/// <summary>
	/// Допустимые входящие события клиента
	/// </summary>
	public enum ClientActions
	{
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
		/// Нет активных подключений для выполнения действия
		/// </summary>
		NoConnections,
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

	public class FromClientCommand
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
		public OnConnection Client { get; set; }

		/// <summary>
		/// Возвращает значение, указывающее количество полученных из сети и доступных для чтения данных
		/// </summary>
		public int Available { get; set; }
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
