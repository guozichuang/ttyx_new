using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Web;
using System.Collections;

using Simon.Common;

/// <summary>
/// UpLoad 上传类
/// </summary>
public class UpLoad
{
    #region 上传配置
    //上传目录(相对网站跟目录的路径)
    private string Upload_Path = "/Upload";
    //允许上传的文件类型
    private string Upload_AllowFileType = "zip,rar,bmp,jpeg,jpg,gif,png";
    //文件上传大小限制 单位：KB；
    private int Upload_FileMaxSize = 51200;
    //图片上传大小限制 单位：KB；
    private int Upload_PicMaxSize = 5120;
    //图片上传最大宽度 单位：PX；
    private int Upload_PicMaxWidth = 0;
    //图片上传最大高度 单位：PX；
    private int Upload_PicMaxHeight = 0;
    //生成缩略图宽度 单位：PX；
    private int Upload_PicThumbnailWidth = 400;
    //生成缩略图高度 单位：PX；
    private int Upload_PicThumbnailHeight = 300;
    //上传图片加水印(0不加水印，1文字水印，2图片水印)
    private int Upload_PicWaterMarkType = 0;
    //上传图片加水印：水印文字
    private string Upload_PicWaterMark_Txt = string.Empty;
    //上传图片加水印：水印图片
    private string Upload_PicWaterMark_PicUrl = string.Empty;

    #endregion

    public UpLoad()
    {
        //初始化

    }

    /// <summary>
    /// 裁剪图片并保存
    /// </summary>
    public bool cropSaveAs(string fileName, string newFileName, int maxWidth, int maxHeight, int cropWidth, int cropHeight, int X, int Y)
    {
        string fileExt = SimonUtils.GetFileExt(fileName); //文件扩展名，不含“.”
        if (!IsImage(fileExt))
        {
            return false;
        }
        string newFileDir = SimonUtils.GetMapPath(newFileName.Substring(0, newFileName.LastIndexOf(@"/") + 1));
        //检查是否有该路径，没有则创建
        if (!Directory.Exists(newFileDir))
        {
            Directory.CreateDirectory(newFileDir);
        }
        try
        {
            string fileFullPath = SimonUtils.GetMapPath(fileName);
            string toFileFullPath = SimonUtils.GetMapPath(newFileName);
            return SimonThumbnail.MakeThumbnailImage(fileFullPath, toFileFullPath, 180, 180, cropWidth, cropHeight, X, Y);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 文件上传方法
    /// </summary>
    /// <param name="postedFile">文件流</param>
    /// <param name="isThumbnail">是否生成缩略图</param>
    /// <param name="isWater">是否打水印</param>
    /// <returns>上传后文件信息</returns>
    public string fileSaveAs(HttpPostedFile postedFile, bool isThumbnail, bool isWater)
    {
        try
        {
            string fileExt = SimonUtils.GetFileExt(postedFile.FileName); //文件扩展名，不含“.”
            int fileSize = postedFile.ContentLength; //获得文件大小，以字节为单位
            string fileName = postedFile.FileName.Substring(postedFile.FileName.LastIndexOf(@"\") + 1); //取得原文件名
            string newFileName = SimonUtils.GetRamCode() + "." + fileExt; //随机生成新的文件名
            string newThumbnailFileName = "thumb_" + newFileName; //随机生成缩略图文件名
            string upLoadPath = GetUpLoadPath(); //上传目录相对路径
            string fullUpLoadPath = SimonUtils.GetMapPath(upLoadPath); //上传目录的物理路径
            string newFilePath = upLoadPath + newFileName; //上传后的路径
            string newThumbnailPath = upLoadPath + newThumbnailFileName; //上传后的缩略图路径

            //检查文件扩展名是否合法
            if (!CheckFileExt(fileExt))
            {
                return "不允许上传" + fileExt + "类型的文件";
            }
            //检查文件大小是否合法
            if (!CheckFileSize(fileExt, fileSize))
            {
                return "文件超过限制的大小";
            }
            //检查上传的物理路径是否存在，不存在则创建
            SimonUtils.CreateDir(fullUpLoadPath);

            //保存文件
            postedFile.SaveAs(fullUpLoadPath + newFileName);
            //如果是图片，检查图片是否超出最大尺寸，是则裁剪
            if (IsImage(fileExt) && (Upload_PicMaxWidth > 0 || Upload_PicMaxHeight > 0))
            {
                SimonThumbnail.MakeThumbnailImage(fullUpLoadPath + newFileName, fullUpLoadPath + newFileName,
                    Upload_PicMaxWidth, Upload_PicMaxHeight);
            }
            //如果是图片，检查是否需要生成缩略图，是则生成
            if (IsImage(fileExt) && isThumbnail && Upload_PicThumbnailWidth > 0 && Upload_PicThumbnailHeight > 0)
            {
                SimonThumbnail.MakeThumbnailImage(fullUpLoadPath + newFileName, fullUpLoadPath + newThumbnailFileName,
                    Upload_PicThumbnailWidth, Upload_PicThumbnailHeight, "Cut");
            }
            //如果是图片，检查是否需要打水印
            if (IsWaterMark(fileExt) && isWater)
            {
                switch (Upload_PicWaterMarkType)
                {
                    case 1:
                        SimonWaterMark.AddImageSignText(newFilePath, newFilePath, Upload_PicWaterMark_Txt, 9, 63, "黑体", 16);
                        break;
                    case 2:
                        SimonWaterMark.AddImageSignPic(newFilePath, newFilePath, Upload_PicWaterMark_PicUrl, 9, 63, 6);
                        break;
                }
            }
            //处理完毕，返回JOSN格式的文件信息
            //return "{\"status\": 1, \"msg\": \"上传文件成功！\", \"name\": \""
            //    + fileName + "\", \"path\": \"" + newFilePath + "\", \"thumb\": \""
            //    + newThumbnailPath + "\", \"size\": " + fileSize + ", \"ext\": \"" + fileExt + "\"}";
            return "success|" + newFilePath + ""; 
        }
        catch
        {
            return "上传过程中发生意外错误";
        }
    }

    #region 私有方法

    /// <summary>
    /// 返回上传目录相对路径
    /// </summary>
    /// <param name="fileName">上传文件名</param>
    private string GetUpLoadPath()
    {
        return Upload_Path + "/" + DateTime.Now.ToString("yyyyMMdd") + "/";
    }

    /// <summary>
    /// 是否需要打水印
    /// </summary>
    /// <param name="_fileExt">文件扩展名，不含“.”</param>
    private bool IsWaterMark(string _fileExt)
    {
        //判断是否开启水印
        if (Upload_PicWaterMarkType > 0)
        {
            //判断是否可以打水印的图片类型
            ArrayList al = new ArrayList();
            al.Add("bmp");
            al.Add("jpeg");
            al.Add("jpg");
            al.Add("png");
            if (al.Contains(_fileExt.ToLower()))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 是否为图片文件
    /// </summary>
    /// <param name="_fileExt">文件扩展名，不含“.”</param>
    private bool IsImage(string _fileExt)
    {
        ArrayList al = new ArrayList();
        al.Add("bmp");
        al.Add("jpeg");
        al.Add("jpg");
        al.Add("gif");
        al.Add("png");
        if (al.Contains(_fileExt.ToLower()))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查是否为合法的上传文件
    /// </summary>
    private bool CheckFileExt(string _fileExt)
    {
        //检查危险文件
        string[] excExt = { "asp", "aspx", "php", "jsp", "htm", "html" };
        for (int i = 0; i < excExt.Length; i++)
        {
            if (excExt[i].ToLower() == _fileExt.ToLower())
            {
                return false;
            }
        }
        //检查合法文件
        string[] allowExt = Upload_AllowFileType.Split(',');
        for (int i = 0; i < allowExt.Length; i++)
        {
            if (allowExt[i].ToLower() == _fileExt.ToLower())
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查文件大小是否合法
    /// </summary>
    /// <param name="_fileExt">文件扩展名，不含“.”</param>
    /// <param name="_fileSize">文件大小(B)</param>
    private bool CheckFileSize(string _fileExt, int _fileSize)
    {
        //判断是否为图片文件
        if (IsImage(_fileExt))
        {
            if (Upload_PicMaxSize > 0 && _fileSize > Upload_PicMaxSize * 1024)
            {
                return false;
            }
        }
        else
        {
            if (Upload_FileMaxSize > 0 && _fileSize > Upload_FileMaxSize * 1024)
            {
                return false;
            }
        }
        return true;
    }

    #endregion
}
