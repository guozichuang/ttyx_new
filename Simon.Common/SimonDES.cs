using System;
using System.Security.Cryptography;  
using System.Text;
using System.Configuration;

namespace Simon.Common
{
	/// <summary>
	/// DES加密/解密类
	/// </summary>
	public class SimonDES
	{
        /// <summary>
        /// 获取Key，优先读取配置文件
        /// </summary>
        private static string KeyIV
        {
            get
            {
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["DESKEY"]))
                {
                    return ConfigurationManager.AppSettings["DESKEY"];
                }
                else
                {
                    return "CoolSimon20150313";
                }
            }
        }

		#region DES加密
        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="Text"></param>
        /// <returns></returns>
		public static string Encrypt(string Text)
		{
            return Encrypt(Text, KeyIV);
		}
		/// <summary> 
        /// 加密(自定义KeyIV)
		/// </summary> 
		/// <param name="Text"></param> 
		/// <param name="sKey"></param> 
		/// <returns></returns> 
		public static string Encrypt(string Text,string sKey) 
		{ 
			DESCryptoServiceProvider des = new DESCryptoServiceProvider(); 
			byte[] inputByteArray; 
			inputByteArray=Encoding.Default.GetBytes(Text); 
			des.Key = ASCIIEncoding.ASCII.GetBytes(System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(sKey, "md5").Substring(0, 8)); 
			des.IV = ASCIIEncoding.ASCII.GetBytes(System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(sKey, "md5").Substring(0, 8)); 
			System.IO.MemoryStream ms=new System.IO.MemoryStream(); 
			CryptoStream cs=new CryptoStream(ms,des.CreateEncryptor(),CryptoStreamMode.Write); 
			cs.Write(inputByteArray,0,inputByteArray.Length); 
			cs.FlushFinalBlock(); 
			StringBuilder ret=new StringBuilder(); 
			foreach( byte b in ms.ToArray()) 
			{ 
				ret.AppendFormat("{0:X2}",b); 
			} 
			return ret.ToString(); 
		}
		#endregion
		
		#region DES解密
        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="Text"></param>
        /// <returns></returns>
		public static string Decrypt(string Text) 
		{
            return Decrypt(Text, KeyIV);
		}
		/// <summary> 
		/// 解密(自定义KeyIV)
		/// </summary> 
		/// <param name="Text"></param> 
		/// <param name="sKey"></param> 
		/// <returns></returns> 
		public static string Decrypt(string Text,string sKey) 
		{ 
			DESCryptoServiceProvider des = new DESCryptoServiceProvider(); 
			int len; 
			len=Text.Length/2; 
			byte[] inputByteArray = new byte[len]; 
			int x,i; 
			for(x=0;x<len;x++) 
			{ 
				i = Convert.ToInt32(Text.Substring(x * 2, 2), 16); 
				inputByteArray[x]=(byte)i; 
			} 
			des.Key = ASCIIEncoding.ASCII.GetBytes(System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(sKey, "md5").Substring(0, 8)); 
			des.IV = ASCIIEncoding.ASCII.GetBytes(System.Web.Security.FormsAuthentication.HashPasswordForStoringInConfigFile(sKey, "md5").Substring(0, 8)); 
			System.IO.MemoryStream ms=new System.IO.MemoryStream(); 
			CryptoStream cs=new CryptoStream(ms,des.CreateDecryptor(),CryptoStreamMode.Write); 
			cs.Write(inputByteArray,0,inputByteArray.Length); 
			cs.FlushFinalBlock(); 
			return Encoding.Default.GetString(ms.ToArray()); 
		} 
		#endregion
	}
}
