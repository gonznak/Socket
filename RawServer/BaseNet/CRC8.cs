namespace RawServer
{
	public class CRC8
	{
		static byte[] table = new byte[256];
		// x8 + x7 + x6 + x4 + x2 + 1
		const byte poly = 0xd5;

		public static byte ComputeChecksum(int offset, int count, params byte[] bytes)
		{
			byte crc = 0xff;
			int _offset = 0;
			int _count = 0;

			if (bytes != null && bytes.Length > 0)
			{
				foreach (byte b in bytes)
				{
					_offset += 1;
					if (_offset <= offset)
						continue;

					_count += 1;
					if (_count > count)
						break;

					crc = table[crc ^ b];
				}
			}
			return crc;
		}

		static CRC8()
		{
			for (int i = 0; i < 256; ++i)
			{
				int temp = i;
				for (int j = 0; j < 8; ++j)
				{
					if ((temp & 0x80) != 0)
					{
						temp = (temp << 1) ^ poly;
					}
					else
					{
						temp <<= 1;
					}
				}
				table[i] = (byte)temp;
			}
		}
	}
}
