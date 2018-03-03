using Pool;
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace RawServer
{
	public sealed partial class BaseProtocol_v2 : OnConnection, IPoolSlotHolder<BaseProtocol>
	{
		private ICryptoTransform decryptor = null;
		private ICryptoTransform encryptor = null;
		private byte[][] keys = new byte[5][];
		private byte[] randomData = new byte[256];

		#region Properties
		private bool _isCrypting = false;
		/// <summary>
		/// Определяет поддерживается ли шифрование в данном соединении
		/// </summary>
		public bool IsCrypting
		{
			get => _isCrypting;
			private set
			{
				if (IsAccepted) return;

				if (value)
					CreateCryptoKeys();
				else
					ResetCryptoKeys();

				_isCrypting = value;
			}
		}

		/// <summary>
		/// Определяет подтвердил ли клиент использование шифрования
		/// </summary>
		public bool IsCryptingAccept { get; private set; }

		/// <summary>
		/// Определяет используется ли шифрование при общении с клиентом
		/// </summary>
		public bool IsCryptingUsage { get; private set; }
		#endregion

		private void CreateCryptoKeys()
		{
			using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
			using (RijndaelManaged AES = new RijndaelManaged())
			{
				AES.BlockSize = 256;
				AES.KeySize = 256;
				AES.Mode = CipherMode.CBC;
				AES.GenerateIV();
				AES.GenerateKey();

				decryptor = AES.CreateDecryptor();
				encryptor = AES.CreateEncryptor();

				keys[0] = AES.Key;
				keys[1] = AES.IV;
				keys[2] = RSA.ExportCspBlob(false); // public server key
				keys[3] = RSA.ExportCspBlob(true);  // private server key
			}
		}

		private bool CryptoHandleWelcome()
		{
			IsCryptingAccept = buffReader.ReadBool();

			if (IsCryptingAccept)
			{
				int lengthPublicKey = buffReader.ReadInt32();
				keys[5] = CryptoDeserialize(buffReader.ReadBytes(lengthPublicKey));
				if (keys[5] is null || keys[5].Length == 0)
					return false;
			}

			return true;
		}

		private bool CryptoHandleInfo()
		{
			byte[] testData = CryptoDeserialize(null);

			if(Array.Equals(randomData, testData) == false)
				return false;

			return true;
		}

		private void CryptoSerialize(byte[] unEncrypted)
		{
			using (MemoryStream ms = new MemoryStream())
			using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
			{
				cs.Write(unEncrypted, 0, unEncrypted.Length);
				cs.Close();

				buffWriter.WriteInt32((int)ms.Length);
				buffWriter.WriteBytes(ms.ToArray());
			}
		}

		private byte[] CryptoDeserialize(byte[] cryptData)
		{
			byte[] decrypted = null;

			if (IsCryptingAccept && IsCryptingUsage)
			{
				int cryptoLength = buffReader.ReadInt32();

				using (MemoryStream ms = new MemoryStream(buffReader.ReadBytes(cryptoLength)))
				using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
				using (MemoryStream dest = new MemoryStream())
				{
					cs.CopyTo(dest);
					decrypted = dest.ToArray();
				}
			}
			else if (IsCryptingAccept && IsCryptingUsage == false)
			{
				try
				{
					using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
					{
						RSA.ImportCspBlob(keys[3]);
						decrypted = RSA.Decrypt(cryptData, true);
						IsCryptingUsage = true;
					}
				}
				catch (CryptographicException)
				{ }
			}
			else
				decrypted = cryptData;

			return decrypted;
		}

		private void ResetCryptoKeys()
		{
			if (IsCrypting == false) return;

			Array.ForEach(keys, key => key = null);

			decryptor.Dispose();
			decryptor = null;
			encryptor.Dispose();
			encryptor = null;

			IsCryptingAccept = false;
			IsCryptingUsage = false;
		}

		private void WriteCryptoWelcome()
		{
			buffWriter.WriteBool(IsCrypting);

			if (IsCrypting == false) return;

			buffWriter.WriteInt32(keys[2].Length);
			buffWriter.WriteBytes(keys[2]);
		}

		private void WriteCryptoKeys()
		{
			byte[] encryptedData = null;

			buffWriter.Clear();
			WriteHeader(MessageTypes.CryptInfo);

			try
			{
				using (MemoryStream ms = new MemoryStream())
				using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
				{
					ms.Write(BitConverter.GetBytes((ushort)keys[0].Length), 0, 2);
					ms.Write(keys[0], 0, keys[0].Length);
					ms.Write(BitConverter.GetBytes((ushort)keys[1].Length), 0, 2);
					ms.Write(keys[1], 0, keys[1].Length);

					RSA.ImportCspBlob(keys[5]);
					encryptedData = RSA.Encrypt(ms.ToArray(), true);

					buffWriter.WriteBool(true);
					buffWriter.WriteUInt16((ushort)encryptedData.Length);
					buffWriter.WriteBytes(encryptedData);

					new Random().NextBytes(randomData);
					CryptoSerialize(randomData);
				}
			}
			catch (CryptographicException)
			{
				buffWriter.WriteBool(false);
			}
			
			WriteCRC();

			base.Send(buffWriter.ToByteArray());
		}
	}
}
