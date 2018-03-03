using Pool;
using System;

namespace RawServer
{
	public class ConnectionPool<T> : PoolEx<T>
		where T : IPoolSlotHolder<T>
	{
		/// <summary>
		/// Инициализация пула подключений
		/// </summary>
		/// <param name="initialCount">Количество инициализированных объектов. Должно быть меньше размера пула</param>
		/// <param name="maxCapacity">Максимальный размер пула</param>
		public ConnectionPool(int initialCount, int maxCapacity)
			: base(maxCapacity)
		{
			if (initialCount < 0 || maxCapacity < initialCount)
				throw new ArgumentOutOfRangeException("initialCount", "The initial count has invalid value");

			TryAllocatePush(initialCount);
		}


		protected override T ObjectConstructor()
		{
			T ClientConnection = (T)Activator.CreateInstance(typeof(T));
			return ClientConnection;
		}

		protected override void CleanUp(T @object)
		{
			((IConnection)@object.PoolSlot.Object).CleanUp();
		}
	}
}
