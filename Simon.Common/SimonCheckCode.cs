using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Web;
using System.IO;
using System.Drawing.Imaging;

namespace Simon.Common
{
    /// <summary>
    /// 图片验证码生成类
    /// </summary>
    public class SimonCheckCode
    {
        //颜色列表，用于验证码、噪线、噪点 
        Color[] color = { Color.Black, Color.Red, Color.Blue, Color.Green, Color.Orange, Color.Brown, Color.DarkBlue };
        //字体列表，用于验证码 
        string[] font = { "Times New Roman", "Verdana", "Arial", "Gungsuh", "Impact" };
        //验证码的字符集，去掉了一些容易混淆的字符 
        char[] character = { '2', '3', '4', '5', '6', '8', '9', 'a', 'b', 'd', 'e', 'f', 'h', 'k', 'm', 'n', 'r', 'x', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'W', 'X' };


        /// <summary>
        /// 生成图片验证码并输出到页面
        /// </summary>
        /// <param name="ChkCode">验证码字符串 </param>
        /// <param name="ImageCheckCodeW">验证码图片宽度</param>
        /// <param name="ImageCheckCodeH">验证码图片高度</param>
        /// <param name="fontSize">字符大小</param>
        /// <param name="context">一般为 this.Context</param>
        public void CreateImageOnPage(string ChkCode, int ImageCheckCodeW, int ImageCheckCodeH, int fontSize, HttpContext context)
        {
            //随机种子
            Random rnd = new Random();
            //创建画布
            Bitmap bmp = new Bitmap(ImageCheckCodeW, ImageCheckCodeH);
            Graphics g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            //画噪线 
            for (int i = 0; i < 1; i++)
            {
                int x1 = rnd.Next(ImageCheckCodeW);
                int y1 = rnd.Next(ImageCheckCodeH);
                int x2 = rnd.Next(ImageCheckCodeW);
                int y2 = rnd.Next(ImageCheckCodeH);
                Color clr = color[rnd.Next(color.Length)];
                g.DrawLine(new Pen(clr), x1, y1, x2, y2);
            }
            //画验证码字符串 
            for (int i = 0; i < ChkCode.Length; i++)
            {
                string fnt = font[rnd.Next(font.Length)];
                Font ft = new Font(fnt, fontSize);
                Color clr = color[rnd.Next(color.Length)];
                g.DrawString(ChkCode[i].ToString(), ft, new SolidBrush(clr), (float)i * 18 + 2, (float)0);
            }
            //画噪点 
            for (int i = 0; i < 100; i++)
            {
                int x = rnd.Next(bmp.Width);
                int y = rnd.Next(bmp.Height);
                Color clr = color[rnd.Next(color.Length)];
                bmp.SetPixel(x, y, clr);
            }
            //清除该页输出缓存，设置该页无缓存 
            context.Response.Buffer = true;
            context.Response.ExpiresAbsolute = System.DateTime.Now.AddMilliseconds(0);
            context.Response.Expires = 0;
            context.Response.CacheControl = "no-cache";
            context.Response.AppendHeader("Pragma", "No-Cache");
            //将验证码图片写入内存流，并将其以 "image/Png" 格式输出 
            MemoryStream ms = new MemoryStream();
            try
            {
                bmp.Save(ms, ImageFormat.Png);
                context.Response.ClearContent();
                context.Response.ContentType = "image/Png";
                context.Response.BinaryWrite(ms.ToArray());
            }
            finally
            {
                //显式释放资源 
                bmp.Dispose();
                g.Dispose();
            }
        }

        /// <summary>
        /// 生成验证码随机字符
        /// </summary>
        /// <param name="codeLength">随机码长度</param>
        /// <returns></returns>
        public string CreateCheckCode(int codeLength)
        {
            string _chkCode = string.Empty;
            Random rnd = new Random();
            //生成验证码字符串 
            for (int i = 0; i < codeLength; i++)
            {
                _chkCode += character[rnd.Next(character.Length)];
            }
            return _chkCode;
        }

    }
}
