using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Zi.Common;

public partial class ChangeFileTest : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        ChangeFileData();
    }

    #region 测试修改文件内容
    protected void ChangeFileData()
    {
        string filePath = "C:\\ceshi\\roomid.INI";
        List<string[]> list = ZiFiles.ReadInfoFromFile(filePath); //这个函数把文件的每一行读入list

        int s = 0;

        for (int i = 0; i < list.Count; i++)
        {
            
            if (list[i].Contains("95.6%"))
            {
                list.Remove(list[i]);
                string[] newdate = { "jilv=95.7%" };
                list.Insert(i,newdate);
                Response.Write("修改数据成功");
            }
            else
            {
                s++;
                if (s==list.Count)
                {
                    Response.Write("不存在该数据");
                }
            }
        }

        //foreach (string[] strArray in list)
        //{
        //    if (strArray.Length < 5)
        //    {
        //        continue;
        //    }
        //    for (int i = 0; i < strArray.Length; i++)
        //    {
        //        if (i == 1)
        //        {
        //            strArray[i] = "10901800=2"; //更改内容
        //            Response.Write("修改成功");
        //        }
        //    }
        //}


        ZiFiles.WriteInfoTofile(filePath, list);
    }

    #endregion
}