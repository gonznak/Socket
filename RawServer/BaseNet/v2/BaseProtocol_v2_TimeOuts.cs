using System;

namespace RawServer.BaseNet
{
	public sealed partial class BaseProtocol_v2
	{
		private sbyte _pingInterval = 5;
		private sbyte _pingTimeOut = 8;
		private sbyte _receiveTimeOut = 3;
		private sbyte _disconnectTimeOut = 5;

		private TimeoutWatcher pingTimer;
		private TimeoutWatcher receiveTimer;
		private TimeoutWatcher disconnectTimer;


		public sbyte PingInterval
		{
			get { return _pingInterval; }
			private set
			{
				if (value > PingTimeOut - 3)
					throw new ArgumentException("New value PingInterval > PingTimeOut - 3");
				else
					_pingInterval = value < 5 ? (sbyte)5 : value;
			}
		}

		public sbyte PingTimeOut
		{
			get { return _pingTimeOut; }
			private set
			{
				if (value < PingInterval + 3)
					throw new ArgumentException("New value PingTimeOut < PingInterval + 3");
				else
					_pingTimeOut = value < 8 ? (sbyte)8 : value;
			}
		}

		public sbyte ReceiveTimeOut
		{
			get => _receiveTimeOut;
			private set => _pingTimeOut = value < 3 ? (sbyte)3 : value;
		}

		public sbyte DisconnectTimeOut
		{
			get => _disconnectTimeOut;
			private set => _pingTimeOut = value < 5 ? (sbyte)5 : value;
		}


		private void InitTimeouts()
		{
			ResetTimeouts();

			pingTimer = new TimeoutWatcher();
			receiveTimer = new TimeoutWatcher();
			disconnectTimer = new TimeoutWatcher();

			pingTimer.TimeLapse += new OnTimerLapse(Ping_TimeLapse);
			receiveTimer.TimeLapse += new OnTimerLapse(Receive_TimeLapse);
			disconnectTimer.TimeLapse += new OnTimerLapse(Disconnect_TimeLapse);
		}

		private void ResetTimeouts()
		{
			_pingInterval = 5;
			_pingTimeOut = 8;
			_receiveTimeOut = 3;
			_disconnectTimeOut = 5;
		}

		private void TimeoutsWatcher_Connected()
		{
			pingTimer.Start(PingInterval, true);
			receiveTimer.Start(ReceiveTimeOut, true);
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
			pingTimer.Pause();
			receiveTimer.Pause();
		}

		private void TimeoutsWatcher_Disconnecting()
		{
			disconnectTimer.Start(DisconnectTimeOut, false);
		}

		private void TimeoutsWatcher_Disconnected()
		{
			pingTimer.Pause();
			receiveTimer.Pause();
			disconnectTimer.Pause();
		}

		private void TimeoutsWatcher_Stop()
		{
			pingTimer.Stop();
			receiveTimer.Stop();
			disconnectTimer.Stop();
		}


		private void Ping_TimeLapse()
		{
			//base.Send(AssemblyRawPack(LowCommandsRequest.Ping));
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
