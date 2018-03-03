
namespace RawClient.Common
{
	/// <summary>
	/// Допустимые действия сервера
	/// </summary>
	public enum ServerActions
	{
		/// <summary>
		/// Клиент уже подключен к удаленной точке
		/// </summary>
		AlreadyConnected,
		/// <summary>
		/// Клиент подключен к серверу
		/// </summary>
		Connected,
		/// <summary>
		/// Успешное подключение к прокси серверу
		/// </summary>
		ConnectedToProxy,
		/// <summary>
		/// Клиент успешно подключен к удаленной точке через прокси сервер
		/// </summary>
		ConnectedOverProxy,
		/// <summary>
		/// Идет процесс подключения к удаленной точке
		/// </summary>
		Connecting,
		/// <summary>
		/// Идет процесс подключения к прокси серверу
		/// </summary>
		ConnectingToProxy,
		/// <summary>
		/// Подключение завершилось неудачей
		/// </summary>
		ConnectionFailed,
		/// <summary>
		/// Клиент отключен от сервера
		/// </summary>
		Disconnected,
		/// <summary>
		/// Идет процесс подключения к удаленной точке
		/// </summary>
		ProcessConnection,
		/// <summary>
		/// Ошибка авторизации на прокси сервере
		/// </summary>
		ProxyAuthFailed,
		/// <summary>
		/// Входящий буфер от сервера
		/// </summary>
		Receive,
		/// <summary>
		/// Отправленное клиентом сообщение принято сервером
		/// </summary>
		SendCompleted,
		/// <summary>
		/// Сервер завершил работу (отключился)
		/// </summary>
		Shutdown,
		/// <summary>
		/// Неизвестное действие прокси сервера при подключении к удаленной точке
		/// </summary>
		UnknownProxyRecevie,
		/// <summary>
		/// Неизвестное действие сервера при приеме сообщения
		/// </summary>
		UnknownRecevie,
		/// <summary>
		/// Неизвестное действие сервера при отправке сообщения
		/// </summary>
		UnknownSend
	}

	/// <summary>
	/// Допустимые команды клиента
	/// </summary>
	public enum ClientCommands
	{
		/// <summary>
		/// Отправить сообщение серверу
		/// </summary>
		Send,
		/// <summary>
		/// Сервер отключил клиента
		/// </summary>
		Disconnect
	}

	public class FromServerCommand
	{
		/// <summary>
		/// Действие сервера
		/// </summary>
		public ServerActions Action { get; set; }

		/// <summary>
		/// Входящий буфер от сервера
		/// </summary>
		public byte[] ReceiveBuffer { get; set; }

		/// <summary>
		/// Колличество получаемых байтов от сервера
		/// </summary>
		public int ReceiveBufferLength { get; set; }
		
		/// <summary>
		/// Пользовательская переменная
		/// </summary>
		public object UserToken { get; set; }
	}

	public class ToClientCommand
	{
		/// <summary>
		/// Команда клиента.
		/// </summary>
		public ClientCommands Command { get; set; }

		/// <summary>
		/// Исходящий буфер для сервера
		/// </summary>
		public byte[] ReceiveBuffer { get; set; }
	}
}
