using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Simon.Common
{
    public class SimonLog
    {
        /// <summary>
        ///  写文本日志
        /// </summary>
        /// <param name="txtinfo">文本文件内容</param>
        /// <param name="txtfilepath">文本文件路径</param>
        /// <param name="txtfilename">文本文件名称</param>
        public static void WriteLog(string txtinfo, string txtfilepath, string txtfilename)
        {
            FileStream fs = null;
            StreamWriter sw = null;
            try
            {
                txtfilename = string.IsNullOrEmpty(txtfilename) ? DateTime.Now.ToString("yyyyMMdd") + ".txt" : txtfilename + ".txt";
                string dirpath = AppDomain.CurrentDomain.BaseDirectory + txtfilepath;
                SimonUtils.CreateDir(dirpath);
                fs = new FileStream(dirpath + txtfilename, System.IO.FileMode.Append, System.IO.FileAccess.Write);
                sw = new StreamWriter(fs, Encoding.UTF8);
                sw.WriteLine(DateTime.Now.ToString() + "     " + txtinfo + "\r\n");
            }
            finally
            {
                if (sw != null)
                {
                    sw.Flush();
                    sw.Dispose();
                    sw = null;
                }
                if (fs != null)
                {
                    fs.Dispose();
                    fs = null;
                }
            }
        }
    }
}
