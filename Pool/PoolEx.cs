using System;

namespace Pool
{
	public abstract class PoolEx<T> : Pool<T>
		 where T : IPoolSlotHolder<T>
	{
		protected PoolEx(int maxCapacity)
			: base(maxCapacity)
		{
		}

		/// <summary>
		/// Возвращает доступный объект из пула или создает новый.
		/// </summary>
		/// <returns>Pool slot</returns>
		public T TakeObject()
		{
			return TakeSlot().Object;
		}

		/// <summary>
		/// Переводит переданный объект в пул.
		/// </summary>
		/// <param name="item"></param>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
		/// <exception cref="InvalidOperationException" />
		public void Release(T item)
		{
			if (item == null)
				throw new ArgumentNullException("item");
			Release(item.PoolSlot);
		}

		protected sealed override void HoldSlotInObject(T @object, PoolSlot<T> slot)
		{
			@object.PoolSlot = slot;
		}
	}
}
