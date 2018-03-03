using Pool;

namespace RawServer
{
	public sealed partial class BaseProtocol_v2 : OnConnection, IPoolSlotHolder<BaseProtocol>
	{
		private void SendWelcome()
		{
			buffWriter.Clear();

			WriteHeader(MessageTypes.Welcome);
			WriteCryptoWelcome();
			WriteCRC();

			base.Send(buffWriter.ToByteArray());
		}

		private bool HandleWelcome(byte[] buffer, int length)
		{
			buffReader.SetBuffer(false, buffer, length);

			if (CRC8.ComputeChecksum(0, length - 1, buffReader.ReadBytes(length - 1)) != buffReader.ReadByte())
				return false;

			buffReader.SetPosition(true, 0);

			MessageTypes msgType = (MessageTypes)buffReader.ReadUInt8();

			if (buffReader.ReadUInt64() != PacketNumber)
				return false;

			switch (msgType)
			{
				case MessageTypes.Welcome:
					if (CryptoHandleWelcome() == false)
						return false;

					if (IsCryptingAccept)						// Плохой кусок проверки
						WriteCryptoKeys();
					else
						IsConnected = true;
					break;
				case MessageTypes.CryptInfo:
					if (CryptoHandleInfo() == false)
						return false;
					break;
				default:
					return false;
			}

			return true;
		}
	}
}
