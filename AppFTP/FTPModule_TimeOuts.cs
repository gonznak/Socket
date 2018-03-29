using RawServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFTP
{
	public sealed partial class FTPModule
	{
		private sbyte _receiveTimeOut = 30;
		private sbyte _disconnectTimeOut = 5;

		private TimeoutWatcher receiveTimer;
		private TimeoutWatcher disconnectTimer;


		public sbyte ReceiveTimeOut
		{
			get => _receiveTimeOut;
			private set => _receiveTimeOut = value;
		}

		public sbyte DisconnectTimeOut
		{
			get => _disconnectTimeOut;
			private set => _disconnectTimeOut = value;
		}


		private void InitTimeouts()
		{
			ResetTimeouts();

#if DEBUG
			ReceiveTimeOut = 10;
#endif

			receiveTimer = new TimeoutWatcher();
			disconnectTimer = new TimeoutWatcher();

			receiveTimer.TimeLapse += new OnTimerLapse(Receive_TimeLapse);
			disconnectTimer.TimeLapse += new OnTimerLapse(Disconnect_TimeLapse);
		}

		private void ResetTimeouts()
		{
			_receiveTimeOut = 3;
			_disconnectTimeOut = 5;
		}

		private void TimeoutsWatcher_Connected()
		{
			receiveTimer.Start(ReceiveTimeOut, false);
		}

		private void TimeoutsWatcher_Receive()
		{
			receiveTimer.Pause();
		}

		private void TimeoutsWatcher_SendCompleted()
		{
			receiveTimer.Reset();
		}

		private void TimeoutsWatcher_Disconnect()
		{
			receiveTimer.Pause();
		}

		private void TimeoutsWatcher_Disconnecting()
		{
			disconnectTimer.Start(DisconnectTimeOut, false);
		}

		private void TimeoutsWatcher_Disconnected()
		{
			receiveTimer.Pause();
			disconnectTimer.Pause();
		}

		private void TimeoutsWatcher_Stop()
		{
			receiveTimer.Stop();
			disconnectTimer.Stop();
		}

		private void Receive_TimeLapse()
		{
			receiveTimer.Pause();
			DisconnectByClient();
		}

		private void Disconnect_TimeLapse()
		{
			disconnectTimer.Pause();
			base.Close();
		}
	}
}
