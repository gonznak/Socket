using System.ComponentModel;
using System.Threading;

namespace RawServer
{
	public delegate void OnTimerLapse();

	public class TimeoutWatcher
	{
		public event OnTimerLapse TimeLapse;
		public bool IsPaused { get; private set; }

		private AsyncOperation _AsyncOperation;
		private int LapseTime = 0;
		private Timer tTL = null;


		/// <summary>
		/// .ctor
		/// </summary>
		public TimeoutWatcher()
		{
			_AsyncOperation = AsyncOperationManager.CreateOperation(null);
		}

		public void Dispose()
		{
			if (tTL != null)
				tTL.Dispose();
			tTL = null;
			_AsyncOperation = null;
		}

		/// <summary>
		/// Запуск ожидания
		/// </summary>
		/// <param name="time">Время ожидания в секундах</param>
		public void Start(int time, bool isPause)
		{
			IsPaused = isPause;
			if (tTL == null)
			{
				LapseTime = time * 1000;
				tTL = new Timer(TLCallback, this, LapseTime, LapseTime);
			}

			if (LapseTime != (time * 1000))
			{
				LapseTime = time * 1000;
				tTL.Change(LapseTime, LapseTime);
			}
		}

		public void Pause()
		{
			IsPaused = true;
		}

		public void Reset()
		{
			if (tTL == null)
				return;

			IsPaused = !tTL.Change(LapseTime, LapseTime);
		}

		public void Stop()
		{
			if (tTL == null)
				return;

			tTL.Dispose();
			tTL = null;
		}

		private static void TLCallback(object state)
		{
			((TimeoutWatcher)state).SendEvent();
		}

		private void SendEvent()
		{
			if (TimeLapse != null && !IsPaused)
			{
				SendOrPostCallback cb = state => TimeLapse(); ;
				_AsyncOperation.Post(cb, null);
			}
		}
	}
}
