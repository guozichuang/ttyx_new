using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Zi.Common
{
    public class ZiFiles
    {
        /// <summary>
        /// 这个函数把文件的每一行读入list
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<string[]> ReadInfoFromFile(string filePath)
        {

            if (File.Exists(filePath))
            {
                List<string[]> list = new List<string[]>();
                // 打开文件时 一定要注意编码 也许你的那个文件并不是GBK编码的
                using (StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("GBK")))
                {
                    while (!sr.EndOfStream) //读到结尾退出
                    {
                        string temp = sr.ReadLine();
                        //将每一行拆分，分隔符就是char 数组中的字符
                        string[] strArray = temp.Split(new char[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        //将拆分好的string[] 存入list
                        list.Add(strArray);
                    }
                }
                return list;
            }
            return null;
        }

        /// <summary>
        /// 这个函数把list中的每一行写入文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="list"></param>
        public static void WriteInfoTofile(string filePath, List<string[]> list)
        {
            // 打开文件时 一定要注意编码 也许你的那个文件并不是GBK编码的
            using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.GetEncoding("GBK")))
            {
                //一个string[] 是一行 ，一行中以tab键分隔
                foreach (string[] strArray in list)
                {
                    string line = string.Empty;
                    foreach (string temp in strArray)
                    {
                        if (!string.IsNullOrEmpty(temp))
                        {
                            line += temp;
                            line += "\t";
                        }
                    }
                    sw.WriteLine(line);
                }
            }
        }

    }
}
