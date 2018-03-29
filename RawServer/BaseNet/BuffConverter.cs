using System;
using System.IO;
using System.Text;

namespace RawServer
{
	public class BuffConverter
	{
		public enum StringEncoding
		{
			ASCII,
			UTF8
		}

		private MemoryStream msStream = null;
		private long writePosition = 0;
		private long readPosition = 0;

		public BuffConverter()
		{
			msStream = new MemoryStream();
		}

		public void SetBuffer(bool isAppend, byte[] buffer, int length)
		{
			if (isAppend == false)
				this.Clear();

			this.WriteBytes(buffer, 0, length);
		}

		public void Clear()
		{
			writePosition = 0;
			readPosition = 0;

			msStream.SetLength(0);
		}

		public long IncomingBytes
		{
			get { return writePosition; }
		}

		public long IncomingBytesUnread
		{
			get { return writePosition - readPosition; }
		}

		private void SetPosition(bool isRead)
		{
			if (isRead)
				msStream.Position = readPosition > msStream.Length ? msStream.Length : readPosition;
			else
				msStream.Position = writePosition > msStream.Length ? msStream.Length : writePosition;
		}

		public void SetPosition(bool isRead, long position)
		{
			if (isRead)
				readPosition = position > msStream.Length ? msStream.Length : position;
			else
				writePosition = position > msStream.Length ? msStream.Length : position;
		}

		public byte[] ToByteArray()
		{
			if (msStream.Length == 0)
				return new byte[0];

			return msStream.ToArray();
		}

		public bool ReadBool()
		{
			return BitConverter.ToBoolean(this.ReadBytes(1), 0);
		}

		public byte ReadByte()
		{
			byte[] numArray = this.ReadBytes(1);

			return numArray.Length == 0 ? (byte)0 : numArray[0];
		}

		public sbyte ReadInt8()
		{
			return (sbyte)this.ReadUInt8();
		}

		public short ReadInt16()
		{
			return BitConverter.ToInt16(this.ReadBytes(2), 0);
		}

		public int ReadInt32()
		{
			return BitConverter.ToInt32(this.ReadBytes(4), 0);
		}

		public long ReadInt64()
		{
			return BitConverter.ToInt64(this.ReadBytes(8), 0);
		}

		public byte ReadUInt8()
		{
			return this.ReadByte();
		}

		public ushort ReadUInt16()
		{
			return BitConverter.ToUInt16(this.ReadBytes(2), 0);
		}

		public uint ReadUInt32()
		{
			return BitConverter.ToUInt32(this.ReadBytes(4), 0);
		}

		public ulong ReadUInt64()
		{
			return BitConverter.ToUInt64(this.ReadBytes(8), 0);
		}

		public float ReadFloat()
		{
			return BitConverter.ToSingle(this.ReadBytes(4), 0);
		}

		public double ReadDouble()
		{
			return BitConverter.ToDouble(this.ReadBytes(8), 0);
		}

		public string ReadString(int length, StringEncoding encoding)
		{
			switch (encoding)
			{
				case StringEncoding.ASCII:
					return Encoding.ASCII.GetString(this.ReadBytes(length));
				case StringEncoding.UTF8:
					return Encoding.UTF8.GetString(this.ReadBytes(length));
				default:
					throw new ArgumentException("encoding");
			}
		}

		public byte[] ReadBytes(int length)
		{
			if (this.IncomingBytesUnread < length || length == 0)
				return new byte[0];

			if (length == -1)
				length = (int)this.IncomingBytesUnread;

			byte[] numArray = new byte[length];

			this.SetPosition(true);
			msStream.Read(numArray, 0, length);
			readPosition += length;

			return numArray;
		}

		public void WriteBool(bool val)
		{
			this.Write(BitConverter.GetBytes(val), 1);
		}

		public void WriteUInt16(ushort val)
		{
			this.Write(BitConverter.GetBytes(val), 2);
		}

		public void WriteUInt32(uint val)
		{
			this.Write(BitConverter.GetBytes(val), 4);
		}

		public void WriteUInt64(ulong val)
		{
			this.Write(BitConverter.GetBytes(val), 8);
		}

		public void WriteInt8(sbyte val)
		{
			this.Write(BitConverter.GetBytes(val), 1);
		}

		public void WriteInt16(short val)
		{
			this.Write(BitConverter.GetBytes(val), 2);
		}

		public void WriteInt32(int val)
		{
			this.Write(BitConverter.GetBytes(val), 4);
		}

		public void WriteInt64(long val)
		{
			this.Write(BitConverter.GetBytes(val), 8);
		}

		public void WriteFloat(float val)
		{
			this.Write(BitConverter.GetBytes(val), 4);
		}

		public void WriteDouble(double val)
		{
			this.Write(BitConverter.GetBytes(val), 8);
		}

		public void WriteUInt8(byte val)
		{
			this.Write(BitConverter.GetBytes(val), 1);
		}

		public void WriteString(string val, StringEncoding encoding)
		{
			switch (encoding)
			{
				case StringEncoding.ASCII:
					this.Write(Encoding.ASCII.GetBytes(val), val.Length);
					break;
				case StringEncoding.UTF8:
					this.Write(Encoding.UTF8.GetBytes(val), val.Length);
					break;
				default:
					throw new ArgumentException("encoding");
			}
		}

		private void Write(byte[] data, int size)
		{
			if (data == null || data.Length == 0 || size == 0)
				return;

			this.WriteBytes(data, 0, size);
		}

		public void WriteBytes(byte[] val)
		{
			if (val == null || val.Length == 0)
				return;

			this.WriteBytes(val, 0, val.Length);
		}

		public void WriteBytes(byte[] val, int offset, int length)
		{
			if (val == null || val.Length == 0 || offset + length > val.Length || offset >= val.Length || length == 0)
				return;

			this.SetPosition(false);
			msStream.Write(val, offset, length);
			writePosition += length - offset;
		}
	}
}
