using System;
using System.Net;

namespace RawServer
{
	public class SConfiguration
	{
		IPAddress m_Address = null;
		int m_Port = 0;
		int m_PoolConnectionSize = 0;
		int m_PoolInitializedSize = 0;

		public SConfiguration()
		{
			Address = IPAddress.Any;
			Port = 5555;
			PoolConnectionSize = 10;
			PoolInitializedSize = 5;
			PoolConnectionWait = false;
      }

		/// <summary>
		/// Назначает конечную локальную точку сервера
		/// </summary>
		public IPAddress Address
		{
			get { return m_Address; }
			set
			{
				if (value == null) throw new ArgumentNullException("It can not be null");
				m_Address = value;
			}
		}

		/// <summary>
		/// Порт сервера
		/// </summary>
		public int Port
		{
			get { return m_Port; }
			set
			{
				if (value <= 0 || value > 65535) throw new ArgumentException("Must be within 1-65535");
				m_Port = value;
			}
		}

		/// <summary>
		/// Максимальное количество подключений, для клиентов, в пуле
		/// </summary>
		public int PoolConnectionSize
		{
			get { return m_PoolConnectionSize; }
			set
			{
				if (value <= 0 || value > 65535) throw new ArgumentException("Must be within 1-65535");
				m_PoolConnectionSize = value;
			}
		}

		/// <summary>
		/// Количество инициализированных подключений, для клиентов, в пуле.
		/// Свойство <see cref = "PoolInitializedSize" /> должно быть меньше <see cref = "PoolConnectionSize"/>
		/// </summary>
		public int PoolInitializedSize
		{
			get { return m_PoolInitializedSize; }
			set
			{
				if (value < 0) throw new ArgumentException("It can not be less than 0");
				if (value > PoolConnectionSize) throw new ArgumentException("Must be less than the value of said variable \"PoolConnectionSize\"");
				m_PoolInitializedSize = value;
			}
		}

		/// <summary>
		/// Определяет поведение сервера, если в пуле нет свободного подключения для клиента.
		/// Если значение установлено в TRUE, клиент останется подключенным, но его запросы обработаны не будут.
		/// В противном случае клиент сразу отключается.
		/// </summary>
		public bool PoolConnectionWait { get; set; }
	}
}
