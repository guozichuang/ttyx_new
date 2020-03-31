using System;
using System.Collections.Generic;
using System.Text;

using System.Web.UI.WebControls;
using System.Text.RegularExpressions;
using System.Web.Security;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Net;

namespace Simon.Common
{
    /// <summary>
    /// 常用工具类
    /// </summary>
    public class SimonUtils
    {
        #region 简化输出 
        /// <summary>
        /// 简化输出 
        /// </summary>
        /// <param name="str">需要输出的内容</param>
        public static void RespW(string str)
        {
            HttpContext.Current.Response.Write(str);
            HttpContext.Current.Response.End();
        } 
        #endregion

        #region 简化输出(禁止页面缓存)
        /// <summary>
        /// 简化输出(禁止页面缓存)
        /// </summary>
        /// <param name="str">需要输出的内容</param>
        public static void RespWNC(string str)
        {
            HttpContext.Current.Response.Cache.SetNoStore(); //禁止缓存
            HttpContext.Current.Response.ContentType = "text/html";
            HttpContext.Current.Response.Buffer = true;
            HttpContext.Current.Response.ExpiresAbsolute = DateTime.Now.AddDays(-1);
            HttpContext.Current.Response.AddHeader("pragma", "no-cache");
            HttpContext.Current.Response.AddHeader("cache-control", "no-store");
            HttpContext.Current.Response.CacheControl = "no-cache";
            HttpContext.Current.Response.Write(str);
            HttpContext.Current.Response.End();
        } 
        #endregion

        #region 弹出对话框并返回上一页 (结束当前页)
        /// <summary>
        /// 弹出对话框并返回上一页 (结束当前页)
        /// </summary>
        /// <param name="msg">提示信息</param>
        public static void Alert(string msg)
        {
            string js = @"<script type='text/javascript'>alert('" + msg + "');window.history.go(-1);</script>";
            RespW(js);
        } 
        #endregion

        #region 弹出对话框并跳转URL (结束当前页)
        /// <summary>
        /// 弹出对话框并跳转URL (结束当前页)
        /// </summary>
        /// <param name="msg">提示信息</param>
        public static void Alert(string msg, string url)
        {
            string js = @"<script type='text/javascript'>alert('" + msg + "');window.location.href='" + url + "';</script>";
            RespW(js);
        } 
        #endregion

        #region 弹出对话框 (JS脚本延迟加载,不结束当前页,需视情况return或强制结束)
        /// <summary>
        /// 弹出对话框 (JS脚本延迟加载,不结束当前页,需视情况return或强制结束)
        /// </summary>
        /// <param name="page">当前页面指针，一般为this</param>
        /// <param name="msg">提示信息</param>
        public static void Alert(Page page, string msg)
        {
            string js = @"<script type='text/javascript' defer>alert('" + msg + "');</script>";
            page.ClientScript.RegisterStartupScript(page.GetType(), Guid.NewGuid().ToString(), js);
        } 
        #endregion

        #region 弹出对话框并跳转URL (JS脚本延迟加载,不结束当前页,需视情况return或强制结束)
        /// <summary>
        /// 弹出对话框并跳转URL (JS脚本延迟加载,不结束当前页,需视情况return或强制结束)
        /// </summary>
        /// <param name="page">当前页面指针，一般为this</param>
        /// <param name="msg">提示信息</param>
        /// <param name="url">跳转URL</param>
        public static void Alert(Page page, string msg, string url)
        {
            string js = @"<script type='text/javascript' defer>alert('" + msg + "');window.location.href='" + url + "';</script>";
            page.ClientScript.RegisterStartupScript(page.GetType(), Guid.NewGuid().ToString(), js);
        } 
        #endregion

        #region 关闭Lhgdialog插件窗口
        /// <summary>
        /// 关闭Lhgdialog插件窗口
        /// </summary>
        /// <param name="page">当前页面指针，一般为this</param>
        /// <param name="dialogid">窗口id</param>
        public static void CloseLhgdialog(Page page, string dialogid)
        {
            string js = @"<script type='text/javascript' defer>
                              var api = frameElement.api, W = api.opener;
                              W.$.dialog({id:'" + dialogid + "'}).close();</script>";
            page.ClientScript.RegisterStartupScript(page.GetType(), Guid.NewGuid().ToString(), js);
        } 
        #endregion

        #region 打开指定大小的新窗口
        /// <summary>
        /// 打开指定大小的新窗口
        /// </summary>
        /// <param name="page">当前页面指针，一般为this</param>
        /// <param name="url">新窗口URL</param>
        /// <param name="width">宽</param>
        /// <param name="heigth">高</param>
        /// <param name="top">顶部位置</param>
        /// <param name="left">左边位置</param>
        public static void OpenWinBySize(Page page, string url, int width, int heigth, int top, int left)
        {
            string js = @"<script type='text/javascript' defer>window.open('" + url + "','','height=" + heigth + ",width=" + width + ",top=" + top + ",left=" + left + ",location=no,menubar=no,resizable=yes,scrollbars=yes,status=yes,titlebar=no,toolbar=no,directories=no');</script>";
            page.ClientScript.RegisterStartupScript(page.GetType(), Guid.NewGuid().ToString(), js);
        } 
        #endregion

        #region 打开指定大小的模态窗口
        /// <summary>
        /// 打开指定大小的模态窗口
        /// </summary>
        /// <param name="page">当前页面指针，一般为this</param>
        /// <param name="url">模态窗口URL</param>
        /// <param name="width">宽</param>
        /// <param name="height">高</param>
        /// <param name="top">顶部位置</param>
        /// <param name="left">左边位置</param>
        public static void OpenModalDialog(Page page, string url, int width, int height, int top, int left)
        {
            string par = "dialogWidth:" + width.ToString() + "px"
                       + ";dialogHeight:" + height.ToString() + "px"
                       + ";dialogLeft:" + left.ToString() + "px"
                       + ";dialogTop:" + top.ToString() + "px"
                       + ";center:yes;help=no;resizable:no;status:no;scroll=yes";
            string js = @"<script type='text/javascript' defer>showModalDialog('" + url + "','','" + par + "');</script>";
            page.ClientScript.RegisterStartupScript(page.GetType(), Guid.NewGuid().ToString(), js);
        } 
        #endregion

        #region 获取Request值,字符串类型,检测是否为中英文,不能带特殊符号
        /// <summary>
        /// 获取Request值,字符串类型,检测是否为中英文,不能带特殊符号
        /// </summary>
        /// <param name="str">要获取的Request参数名</param>
        /// <returns></returns>
        public static string Q(string str)
        {
            str = str.ToLower();
            if (HttpContext.Current.Request.Params[str] != null && HttpContext.Current.Request.Params[str].ToString().Trim() != string.Empty && IsEC(HttpContext.Current.Request.Params[str].ToString().Trim()))
                return HttpContext.Current.Request.Params[str].ToString().ToLower().Trim();
            else return string.Empty;
        } 
        #endregion

        #region 获取Request值,数字类型,检测是否为数字,不能带特殊符号
        /// <summary>
        /// 获取Request值,数字类型,检测是否为数字,不能带特殊符号
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string Qnum(string str)
        {
            str = str.ToLower();
            if (HttpContext.Current.Request.Params[str] != null && HttpContext.Current.Request.Params[str].ToString().Trim() != string.Empty && IsInt(HttpContext.Current.Request.Params[str].ToString().Trim()))
                return HttpContext.Current.Request.Params[str].ToString().ToLower().Trim();
            else return string.Empty;
        } 
        #endregion

        #region 获得客户端提交的参数,基础过滤
        /// <summary>
        /// 获得客户端提交的参数,基础过滤
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string myRequest(string str)
        {
            str = str.ToLower();
            if (HttpContext.Current.Request.Params[str] != null && HttpContext.Current.Request.Params[str].ToString() != string.Empty)
            {
                return HttpContext.Current.Server.UrlDecode(ReplaceBadWordsAndHtml(HttpContext.Current.Request.Params[str].ToString().ToLower().Trim()));
                //Regex obj = new Regex("\\w+");
                //Match objmach = obj.Match(HttpContext.Current.Request.Params[s].ToString());
                //if (objmach.Success) return objmach.Value;
            }
            return string.Empty;
        }
        #endregion

        #region MD5字符串加密
        /// <summary>
        /// MD5字符串加密(普通加密)
        /// </summary>
        /// <param name="str">要加密的字符串</param>
        /// <returns></returns>
        public static string EnCodeMD5(string str)
        {
            return FormsAuthentication.HashPasswordForStoringInConfigFile(str, "MD5");
        }
        /// <summary>
        /// MD5字符串加密(混合加密)
        /// </summary>
        /// <param name="str">要加密的字符串</param>
        /// <returns></returns>
        public static string EnCodeMixMD5(string str)
        {
            str = SimonDES.Encrypt(str);
            return FormsAuthentication.HashPasswordForStoringInConfigFile(str, "MD5");
        } 
        #endregion

        #region 过滤空格和换行符
        /// <summary>
        /// 过滤空格和换行符
        /// </summary>
        /// <param name="str">需要过滤的字符串</param>
        /// <returns>过滤后的字符串</returns>
        public static string ReplaceSpaceAndLineBreak(string str)
        {
            str = Regex.Replace(str, "[\f\n\r\t\v]", "");
            str = Regex.Replace(str, " {2,}", " ");
            str = Regex.Replace(str, ">[ ]{1}", ">");
            return str;
        } 
        #endregion

        #region 过滤敏感字符和html代码
        /// <summary>
        /// 过滤敏感字符和html代码
        /// </summary>
        /// <param name="str">需要过滤的字符串</param>
        /// <returns>过滤后的字符串</returns>
        public static string ReplaceBadWordsAndHtml(string str)
        {
            str = ReplaceBadWords(str);
            str = HtmlToTxt(str);
            return str;
        } 
        #endregion

        #region 敏感字符过滤
        /// <summary>
        /// 敏感字符过滤
        /// </summary>
        /// <param name="str">需要过滤的字符串</param>
        /// <returns>过滤后的字符串</returns>
        public static string ReplaceBadWords(string str)
        {
            str = str.Replace("共产党", "***");
            str = str.Replace("共产", "***");
            str = str.Replace("共产党", "***");
            str = str.Replace("中共", "***");
            str = str.Replace("法轮", "***");
            str = str.Replace("李宏", "***");
            str = str.Replace("灭党", "***");
            str = str.Replace("退党", "***");
            str = str.Replace("毛泽东", "***");
            str = str.Replace("六四", "***");
            str = str.Replace("文革", "***");
            str = str.Replace("他妈的", "***");
            str = str.Replace("TMD", "***");
            return str;
        } 
        #endregion

        #region 密码强度检测(密码为六位及以上并且字母、数字、特殊字符三项中有两项，中等强度密码)
        /// <summary>
        /// 密码强度检测(密码为六位及以上并且字母、数字、特殊字符三项中有两项，中等强度密码)
        /// </summary>
        /// <param name="pwdstr"></param>
        /// <returns></returns>
        public static bool PwdCheck(string pwdstr)
        {
            return Regex.IsMatch(pwdstr, @"^(?=.{6,})(((?=.*[A-Z])(?=.*[a-z]))|((?=.*[A-Z])(?=.*[0-9]))|((?=.*[a-z])(?=.*[0-9]))).*$");
        } 
        #endregion

        #region 检测是否有Sql危险字符
        /// <summary>
        /// 检测是否有Sql危险字符
        /// </summary>
        /// <param name="str">要判断字符串</param>
        /// <returns>判断结果</returns>
        public static bool CheckSqlString(string str)
        {
            return Regex.IsMatch(str, @"[-|;|,|\/|\(|\)|\[|\]|\}|\{|%|@|\*|!|\']");
        }

        /// <summary>
        /// 检查危险字符
        /// </summary>
        /// <param name="Input"></param>
        /// <returns></returns>
        public static string SqlStringFilter(string sInput)
        {
            if (sInput == null || sInput == "")
                return null;
            string sInput1 = sInput.ToLower();
            string output = sInput;
            string pattern = @"*|and|exec|insert|select|delete|update|count|master|truncate|declare|char(|mid(|chr(|'";
            if (Regex.Match(sInput1, Regex.Escape(pattern), RegexOptions.Compiled | RegexOptions.IgnoreCase).Success)
            {
                throw new Exception("字符串中含有非法字符!");
            }
            else
            {
                output = output.Replace("'", "''");
            }
            return output;
        }
        #endregion

        #region 检测是否为邮箱格式
        /// <summary>
        /// 检测是否为邮箱格式
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsEmail(string str)
        {
            return Regex.IsMatch(str, @"^([\w-.]+)@(([[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.)|(([\w-]+.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(]?)$");
        }
        #endregion

        #region 检测是否为手机格式

        #endregion
        /// <summary>
        /// 检测是否为手机格式
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool CheckPhoneIsAble(string input)
        {
            if (input.Length < 11)
            {
                return false;
            }
            //电信手机号码正则
            string dianxin = @"^1[3578][01379]\d{8}$";
            Regex regexDX = new Regex(dianxin);
            //联通手机号码正则
            string liantong = @"^1[34578][01256]\d{8}";
            Regex regexLT = new Regex(liantong);
            //移动手机号码正则
            string yidong = @"^(1[012345678]\d{8}|1[345678][012356789]\d{8})$";
            Regex regexYD = new Regex(yidong);
            if (regexDX.IsMatch(input) || regexLT.IsMatch(input) || regexYD.IsMatch(input))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #region 检测是否为英文和数字组合
        /// <summary>
        /// 检测是否为英文和数字组合
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNumAndEn(string str)
        {
            return Regex.IsMatch(str, @"^(?=.*?[a-zA-Z])(?=.*?[0-9])[a-zA-Z0-9]+$");
        } 
        #endregion

        #region 检测是否为英文或数字组合
        /// <summary>
        /// 检测是否为英文或数字组合
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNumOrEn(string str)
        {
            return Regex.IsMatch(str, @"^[A-Za-z0-9]+$");
        } 
        #endregion

        #region 检测是否为中文、英文、数字、下划线 并且不能以下划线开头和结尾
        /// <summary>
        /// 检测是否为中文、英文、数字、下划线 并且不能以下划线开头和结尾
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsEC(string str)
        {
            return Regex.IsMatch(str, @"^(?!_)(?!.*?_$)[a-zA-Z0-9_\u4e00-\u9fa5]+$");
        } 
        #endregion

        #region 检测是否为数字和字母组合的字符
        /// <summary>
        /// 检测是否为数字和字母组合的字符
        /// </summary>
        /// <param name="str">要检测的字符</param>
        /// <returns></returns>
        public static bool IsWordNum(string str)
        {
            return Regex.IsMatch(str, @"^[A-Za-z0-9]+$");
        } 
        #endregion

        #region 检测是否为数字 整数
        /// <summary>
        /// 检测是否为数字 整数
        /// </summary>
        /// <param name="str">要检测的字符</param>
        /// <returns></returns>
        public static bool IsNum(string str)
        {
            return Regex.IsMatch(str, @"^[0-9]+$");
        } 
        #endregion

        #region 检测是否为整数(正整数、负整数)
        /// <summary>
        /// 检测是否为整数(正整数、负整数)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsInt(string str)
        {
            return Regex.IsMatch(str, @"^-?\d+$");
        }
        #endregion

        #region 检查是否为IP地址格式
        /// <summary>
        /// 检查是否为IP地址格式
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsIP(string ip)
        {
            return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }
        #endregion

        #region 获取字符串中的数字(字符串形式)
        /// <summary>
        /// 获取字符串中的数字(字符串形式)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string GetNumber(string str)
        {
            string strTemp = str;
            strTemp = Regex.Replace(strTemp, @"[^\d]*", "");
            return strTemp;
        } 
        #endregion

        #region 检测是否为浮点数或整数
        /// <summary>
        /// 检测是否为浮点数或整数
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsDecimal(string str)
        {
            return Regex.IsMatch(str, @"^(0?|[1-9]\d*)(\.\d{0,2})?$");
        } 
        #endregion

        #region 检测是否为浮点数或整数 不限小数点位数
        /// <summary>
        /// 检测是否为浮点数或整数 不限小数点位数
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsDecimal2(string str)
        {
            return Regex.IsMatch(str, @"^\d+(\.\d+)?$");
        } 
        #endregion	

        #region 检测货币的价格金额，整数或者小数点后2位
        /// <summary>
        /// 检测货币的价格金额，整数或者小数点后2位
        /// </summary>
        /// <param name="str">要检测的字符串</param>
        /// <returns></returns>
        public static bool IsMoney(string str)
        {
            return Regex.IsMatch(str, @"^(0?|[1-9]\d*)(\.\d{0,2})?$");
        }
        #endregion

        ///   <summary>      
        ///   将指定字符串按指定长度进行剪切，      
        ///   </summary>      
        ///   <param name= "oldStr "> 需要截断的字符串 </param>      
        ///   <param name= "maxLength "> 字符串的最大长度 </param>      
        ///   <param name= "endWith "> 超过长度的后缀 </param>      
        ///   <returns> 如果超过长度，返回截断后的新字符串加上后缀，否则，返回原字符串 </returns>      
        public static string StrCut2(string oldStr, int maxLength, string endWith)
        {
            if (string.IsNullOrEmpty(oldStr))
                //   throw   new   NullReferenceException( "原字符串不能为空 ");      
                return oldStr + endWith;
            if (maxLength < 1)
                throw new Exception("返回的字符串长度必须大于[0] ");
            if (oldStr.Length > maxLength)
            {
                string strTmp = oldStr.Substring(0, maxLength);
                if (string.IsNullOrEmpty(endWith))
                    return strTmp;
                else
                    return strTmp + endWith;
            }
            return oldStr;
        }

        #region 长度截断,英文1个字符、中文2个字符计算,例如截取5个字符可以设置为10
        /// <summary>
        /// 长度截断,英文1个字符、中文2个字符计算,例如截取5个字符可以设置为10
        /// </summary>
        /// <param name="str">被截取的字符串</param>
        /// <param name="length">长度,中文为2个字符,英文为一个z字符,例如截取5个字符可以设置为10</param>
        /// <returns></returns>
        public static string StrCut(string str, int length)
        {
            Regex regex = new Regex("[\u4e00-\u9fa5]+", RegexOptions.Compiled);
            char[] stringChar = str.ToCharArray();
            StringBuilder sb = new StringBuilder();
            int nLength = 0;
            for (int i = 0; i < stringChar.Length; i++)
            {
                if (regex.IsMatch((stringChar[i]).ToString()))
                {
                    nLength += 2;
                }
                else
                {
                    nLength = nLength + 1;
                }

                if (nLength <= length)
                {
                    sb.Append(stringChar[i]);
                }
                else
                {
                    break;
                }
            }

            return sb.ToString();
        } 
        #endregion

        #region 获取混合字符串长度，中文按两个字符计算，数字和字母按一个计算
        /// <summary>
        /// 获取混合字符串长度，中文按两个字符计算，数字和字母按一个计算
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns>字符串长度</returns>
        public static int GetStrLen(string str)
        {
            Regex regex = new Regex("[\u4e00-\u9fa5]+", RegexOptions.Compiled);
            char[] strChar = str.ToCharArray();
            int strLength = 0;
            for (int i = 0; i < strChar.Length; i++)
            {
                if (regex.IsMatch((strChar[i]).ToString()))
                {
                    strLength += 2;
                }
                else
                {
                    strLength = strLength + 1;
                }
            }

            return strLength;
        }
        #endregion

        #region 计算剩余时间,输入整数分钟，转换为 ..天..小时..分钟
        /// <summary>
        /// 计算剩余时间,输入整数分钟，转换为 ..天..小时..分钟
        /// </summary>
        /// <param name="min">要转换的总分钟数</param>
        /// <returns></returns>
        public static string FormatMin(int min)
        {
            int tian = min / 1440;
            int xiaoshi = min % 1440 / 60;
            int fen = min % 60;
            string sj = tian.ToString() + "天" + xiaoshi.ToString() + "时" + fen + "分";
            return sj;
        } 
        #endregion

        #region 格式化价格数据,带货币符号
        /// <summary>
        /// 格式化价格数据,带货币符号
        /// </summary>
        /// <param name="str">要转换的价格</param>
        /// <returns></returns>
        public static string FormatPrice(string str)
        {
            return decimal.Parse(str).ToString("c2");
        } 
        #endregion

        #region 格式化价格数据,不带货币符号
        /// <summary>
        /// 格式化价格数据,不带货币符号
        /// </summary>
        /// <param name="str">要转换的价格</param>
        /// <returns></returns>
        public static string FormatPrice2(string str)
        {
            return decimal.Parse(str).ToString("n2");
        } 
        #endregion

        #region 文件大小 转换KB,MB,GB
        /// <summary>    
        /// 文件大小 转换KB,MB,GB
        /// </summary>    
        /// <param name="size">文件的大小 单位：bytes</param>    
        /// <returns></returns>    
        public static string GetFileSize(int size)
        {
            string FileSize = string.Empty;
            if (size != 0)
            {
                if (size >= 1073741824)
                    FileSize = System.Math.Round(Convert.ToDouble((double)size / (double)1073741824), 2).ToString() + "GB";
                else if (size >= 1048576)
                    FileSize = System.Math.Round(Convert.ToDouble((double)size / (double)1048576), 2).ToString() + "MB";
                else if (size >= 1024)
                    FileSize = System.Math.Round(Convert.ToDouble((double)size / (double)1024), 2).ToString() + "KB";
                else FileSize = size.ToString() + "B";
            }
            else FileSize = size.ToString() + "B";
            return FileSize;
        } 
        #endregion

        #region 输入汉字字符串索引拼音的首字母
        /// <summary>
        /// 输入汉字字符串索引拼音的首字母
        /// </summary>
        /// <param name="ChineseStr">要进行索引的汉字</param>
        /// <returns></returns>
        public static string ChineseCap(string ChineseStr)
        {
            byte[] ZW = new byte[2];
            long ChineseStr_int;
            string CharStr, ChinaStr = "";
            CharStr = ChineseStr.Substring(0, 1).ToString();
            ZW = System.Text.Encoding.Default.GetBytes(CharStr);
            // 得到汉字符的字节数组
            if (ZW.Length == 2)
            {
                int i1 = (short)(ZW[0]);
                int i2 = (short)(ZW[1]);
                ChineseStr_int = i1 * 256 + i2;
                //table of the constant list
                // 'A';     //45217..45252
                // 'B';     //45253..45760
                // 'C';     //45761..46317
                // 'D';     //46318..46825
                // 'E';     //46826..47009
                // 'F';     //47010..47296
                // 'G';     //47297..47613

                // 'H';     //47614..48118
                // 'J';     //48119..49061
                // 'K';     //49062..49323
                // 'L';     //49324..49895
                // 'M';     //49896..50370
                // 'N';     //50371..50613
                // 'O';     //50614..50621
                // 'P';     //50622..50905
                // 'Q';     //50906..51386

                // 'R';     //51387..51445
                // 'S';     //51446..52217
                // 'T';     //52218..52697
                //没有U,V
                // 'W';     //52698..52979
                // 'X';     //52980..53640
                // 'Y';     //53689..54480
                // 'Z';     //54481..55289

                if ((ChineseStr_int >= 45217) && (ChineseStr_int <= 45252))
                {
                    ChinaStr = "A";
                }
                else if ((ChineseStr_int >= 45253) && (ChineseStr_int <= 45760))
                {
                    ChinaStr = "B";
                }
                else if ((ChineseStr_int >= 45761) && (ChineseStr_int <= 46317))
                {
                    ChinaStr = "C";

                }
                else if ((ChineseStr_int >= 46318) && (ChineseStr_int <= 46825))
                {
                    ChinaStr = "D";
                }
                else if ((ChineseStr_int >= 46826) && (ChineseStr_int <= 47009))
                {
                    ChinaStr = "E";
                }
                else if ((ChineseStr_int >= 47010) && (ChineseStr_int <= 47296))
                {
                    ChinaStr = "F";
                }
                else if ((ChineseStr_int >= 47297) && (ChineseStr_int <= 47613))
                {
                    ChinaStr = "G";
                }
                else if ((ChineseStr_int >= 47614) && (ChineseStr_int <= 48118))
                {

                    ChinaStr = "H";
                }

                else if ((ChineseStr_int >= 48119) && (ChineseStr_int <= 49061))
                {
                    ChinaStr = "J";
                }
                else if ((ChineseStr_int >= 49062) && (ChineseStr_int <= 49323))
                {
                    ChinaStr = "K";
                }
                else if ((ChineseStr_int >= 49324) && (ChineseStr_int <= 49895))
                {
                    ChinaStr = "L";
                }
                else if ((ChineseStr_int >= 49896) && (ChineseStr_int <= 50370))
                {
                    ChinaStr = "M";
                }

                else if ((ChineseStr_int >= 50371) && (ChineseStr_int <= 50613))
                {
                    ChinaStr = "N";

                }
                else if ((ChineseStr_int >= 50614) && (ChineseStr_int <= 50621))
                {
                    ChinaStr = "O";
                }
                else if ((ChineseStr_int >= 50622) && (ChineseStr_int <= 50905))
                {
                    ChinaStr = "P";

                }
                else if ((ChineseStr_int >= 50906) && (ChineseStr_int <= 51386))
                {
                    ChinaStr = "Q";

                }

                else if ((ChineseStr_int >= 51387) && (ChineseStr_int <= 51445))
                {
                    ChinaStr = "R";
                }
                else if ((ChineseStr_int >= 51446) && (ChineseStr_int <= 52217))
                {
                    ChinaStr = "S";
                }
                else if ((ChineseStr_int >= 52218) && (ChineseStr_int <= 52697))
                {
                    ChinaStr = "T";
                }
                else if ((ChineseStr_int >= 52698) && (ChineseStr_int <= 52979))
                {
                    ChinaStr = "W";
                }
                else if ((ChineseStr_int >= 52980) && (ChineseStr_int <= 53640))
                {
                    ChinaStr = "X";
                }
                else if ((ChineseStr_int >= 53689) && (ChineseStr_int <= 54480))
                {
                    ChinaStr = "Y";
                }
                else if ((ChineseStr_int >= 54481) && (ChineseStr_int <= 55289))
                {
                    ChinaStr = "Z";
                }
                return ChinaStr + "->";
            }
            else
            {
                return string.Empty;
            }
        } 
        #endregion

        #region RadioButtonList选定某项值或文本
        /// <summary>
        /// RadioButtonList选定某项值
        /// </summary>
        /// <param name="rbl">rbl</param>
        /// <param name="valuestr">值</param>
        public static void RBL_Selected(RadioButtonList rbl, string valuestr)
        {
            if (rbl.Items.FindByValue(valuestr) != null)
            {
                rbl.ClearSelection();
                rbl.Items.FindByValue(valuestr).Selected = true;
            }
        }
        /// <summary>
        /// RadioButtonList选定某项文本
        /// </summary>
        /// <param name="rbl">rbl</param>
        /// <param name="textstr">文本</param>
        public static void RBL_SelectedByText(RadioButtonList rbl, string textstr)
        {
            if (rbl.Items.FindByText(textstr) != null)
            {
                rbl.ClearSelection();
                rbl.Items.FindByText(textstr).Selected = true;
            }
        } 
        #endregion

        #region DropDownList选定某项值或文本
        /// <summary>
        /// DropDownList选定某项值
        /// </summary>
        /// <param name="ddl">ddl</param>
        /// <param name="valuestr">值</param>
        public static void DDL_Selected(DropDownList ddl, string valuestr)
        {
            if (ddl.Items.FindByValue(valuestr) != null)
            {
                ddl.ClearSelection();
                ddl.Items.FindByValue(valuestr).Selected = true;
            }
        }
        /// <summary>
        /// DropDownList选定某项文本
        /// </summary>
        /// <param name="ddl">ddl</param>
        /// <param name="textstr">文本</param>
        public static void DDL_SelectedByText(DropDownList ddl, string textstr)
        {
            if (ddl.Items.FindByText(textstr) != null)
            {
                ddl.ClearSelection();
                ddl.Items.FindByText(textstr).Selected = true;
            }
        } 
        #endregion

        #region 绑定24小时到DropDownList(选择时间时使用)
        /// <summary>
        /// 绑定24小时到DropDownList(选择时间时使用)
        /// </summary>
        /// <param name="ddl"></param>
        public static void Bind24Hour(DropDownList ddl)
        {
            //倒序
            for (int i = 0; i < 24; i++)
            {
                string hourstr = i.ToString();
                if (hourstr.Length < 2) hourstr = "0" + hourstr;
                hourstr += "时";

                ddl.Items.Add(new ListItem(hourstr, i.ToString()));
            }
        } 
        #endregion

        #region 获取CheckBoxList选中的值
        /// <summary>
        /// 获取CheckBoxList选中的值
        /// </summary>
        /// <param name="cbl">要获取的CheckBoxList</param>
        /// <param name="separator">获取值之间的分割字符，如,或|</param>
        /// <returns></returns>
        public static string GetCheckBoxList(CheckBoxList cbl, string separator)
        {
            string _retval = string.Empty;

            foreach (ListItem item in cbl.Items)
            {
                if (item.Selected)
                    _retval += item.Value + separator;
            }
            if (_retval != string.Empty)
            {
                _retval = _retval.Remove(_retval.LastIndexOf(separator), 1);//删除最后一个分割字符        
                return _retval;
            }
            else
            {
                return string.Empty;
            }
        } 
        #endregion

        #region 重置CheckBoxList
        /// <summary>
        /// 重置CheckBoxList
        /// </summary>
        /// <param name="cbl">要重置的CheckBoxList</param>
        public static void ResetCheckBoxList(CheckBoxList cbl)
        {
            foreach (ListItem item in cbl.Items)
            {
                item.Selected = false;
            }
        } 
        #endregion

        #region 给CheckBoxList赋初始值
        /// <summary>
        /// 给CheckBoxList赋初始值 
        /// </summary>
        /// <param name="cbl">要赋值的CheckBoxList</param>
        /// <param name="strArr"></param>
        public static void SetCheckBoxList(CheckBoxList cbl, string[] strArr)
        {
            //赋值之前先把原有的值给清除 
            ResetCheckBoxList(cbl);

            foreach (string val in strArr)
            {
                if (val != "")
                {
                    foreach (ListItem item in cbl.Items)
                    {
                        if (item.Value == val)
                            item.Selected = true;
                    }
                }
            }
        } 
        #endregion

        #region 获得用户IP
        /// <summary>
        /// 获得用户IP(兼容阿里云)
        /// </summary>
        /// <returns>IP</returns>
        public static string GetUserIp()
        {
            string ip;
            string[] temp;
            bool isErr = false;
            if (System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_ForWARDED_For"] == null)
                ip = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"].ToString();
            else
                ip = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_ForWARDED_For"].ToString();
            if (ip.Length > 15)
                isErr = true;
            else
            {
                temp = ip.Split('.');
                if (temp.Length == 4)
                {
                    for (int i = 0; i < temp.Length; i++)
                    {
                        if (temp[i].Length > 3) isErr = true;
                    }
                }
                else
                    isErr = true;
            }

            if (isErr)
                return "1.1.1.1";
            else
                return ip;
        } 
        #endregion

        #region 获得两个日期的间隔
        /// <summary>
        /// 获得两个日期的间隔
        /// </summary>
        /// <param name="DateTime1">日期一。</param>
        /// <param name="DateTime2">日期二。</param>
        /// <returns>日期间隔TimeSpan。</returns>
        public static TimeSpan DateDiff(DateTime DateTime1, DateTime DateTime2)
        {
            TimeSpan ts1 = new TimeSpan(DateTime1.Ticks);
            TimeSpan ts2 = new TimeSpan(DateTime2.Ticks);
            TimeSpan ts = ts1.Subtract(ts2).Duration();
            return ts;
        } 
        #endregion

        #region 格式化日期时间
        /// <summary>
        /// 格式化日期时间
        /// </summary>
        /// <param name="dateTime1">日期时间</param>
        /// <param name="dateMode">显示模式</param>
        /// <returns>0-9种模式的日期</returns>
        public static string FormatDate(DateTime dateTime1, string dateMode)
        {
            switch (dateMode)
            {
                case "0":
                    return dateTime1.ToString("yyyy-MM-dd");
                case "1":
                    return dateTime1.ToString("yyyy-MM-dd HH:mm:ss");
                case "2":
                    return dateTime1.ToString("yyyy/MM/dd");
                case "3":
                    return dateTime1.ToString("yyyy年MM月dd日");
                case "4":
                    return dateTime1.ToString("MM-dd");
                case "5":
                    return dateTime1.ToString("MM/dd");
                case "6":
                    return dateTime1.ToString("MM月dd日");
                case "7":
                    return dateTime1.ToString("yyyy-MM");
                case "8":
                    return dateTime1.ToString("yyyy/MM");
                case "9":
                    return dateTime1.ToString("yyyy年MM月");
                default:
                    return dateTime1.ToString();
            }
        } 
        #endregion

        #region 替换文件名中的特殊字符(如果过滤后文件名为空,则根据时间生成随机文件名)
        /// <summary>
        /// 替换文件名中的特殊字符(如果过滤后文件名为空,则根据时间生成随机文件名)
        /// </summary>
        /// <param name="filename">原始文件名</param>
        /// <returns>过滤后的文件名</returns>
        public static string ReplaceFileName(string filename)
        {
            filename = filename.Replace("_", "-");
            filename = filename.Replace(" ", string.Empty);
            filename = filename.Replace("+", string.Empty);
            filename = filename.Replace("/", string.Empty);
            filename = filename.Replace(":", string.Empty);
            filename = filename.Replace("?", string.Empty);
            filename = filename.Replace("%", string.Empty);
            filename = filename.Replace("#", string.Empty);
            filename = filename.Replace("&", string.Empty);
            filename = filename.Replace("=", string.Empty);
            filename = filename.Replace("!", string.Empty);
            filename = filename.Replace("~", string.Empty);
            filename = filename.Replace("@", string.Empty);
            filename = filename.Replace("$", string.Empty);
            filename = filename.Replace("%", string.Empty);
            filename = filename.Replace("^", string.Empty);
            filename = filename.Replace("*", string.Empty);

            filename = HtmlToTxt(filename);
            if (filename.Equals(string.Empty)) //如果过滤后文件名为空,则根据时间生成随机文件名
                filename = DateTime.Now.ToString("yyyyMMdd") + DateTime.Now.ToString("fffffff").Substring(1, 3);
            return filename;
        } 
        #endregion

        #region 过滤Html标签
        /// <summary>
        /// 过滤Html标签
        /// </summary>
        /// <param name="strHtml">html字符串</param>
        /// <returns>过滤后的字符串</returns>
        public static string HtmlToTxt(string strHtml)
        {
            strHtml = strHtml.ToLower();

            string[] aryReg ={
            @"<script[^>]*?>.*?</script>",
            @"<(\/\s*)?!?((\w+:)?\w+)(\w+(\s*=?\s*(([""'])(\\[""'tbnr]|[^\7])*?\7|\w+)|.{0})|\s)*?(\/\s*)?>",
            @"([\r\n])[\s]+",
            @"&(quot|#34);",
            @"&(amp|#38);",
            @"&(lt|#60);",
            @"&(gt|#62);", 
            @"&(nbsp|#160);", 
            @"&(iexcl|#161);",
            @"&(cent|#162);",
            @"&(pound|#163);",
            @"&(copy|#169);",
            @"&#(\d+);",
            @"-->",
            @"<!--.*\n"
            };
            
            string newReg = aryReg[0];
            for (int i = 0; i < aryReg.Length; i++)
            {
                Regex regex = new Regex(aryReg[i], RegexOptions.IgnoreCase);
                strHtml = regex.Replace(strHtml, string.Empty);
            }

            strHtml = strHtml.Replace("<", "");
            strHtml = strHtml.Replace(">", "");
            strHtml = strHtml.Replace("\r\n", "");

            strHtml = strHtml.Replace("<script", "&lt;script");
            strHtml = strHtml.Replace("script>", "script&gt;");
            strHtml = strHtml.Replace("<%", "&lt;%");
            strHtml = strHtml.Replace("%>", "%&gt;");
            strHtml = strHtml.Replace("<$", "&lt;$");
            strHtml = strHtml.Replace("$>", "$&gt;");

            strHtml = strHtml.Replace("select", "");
            strHtml = strHtml.Replace("insert", "");
            strHtml = strHtml.Replace("update", "");
            strHtml = strHtml.Replace("delete", "");
            strHtml = strHtml.Replace("create", "");
            strHtml = strHtml.Replace("drop", "");
            strHtml = strHtml.Replace("delcare", "");
            strHtml = strHtml.Replace("'", "");
            strHtml = strHtml.Replace("_", "");
            strHtml = strHtml.Replace("%", "[%] ");
            strHtml = strHtml.Replace(";", "");
            strHtml = strHtml.Replace("exec", "");
            strHtml = strHtml.Replace("cmd", "");
            strHtml = strHtml.Replace("&nbsp;", "");
            strHtml = strHtml.Replace(" ", "");

            return strHtml;
        } 
        #endregion

        #region 得到随机日期
        /// <summary>
        /// 得到随机日期
        /// </summary>
        /// <param name="time1">起始日期</param>
        /// <param name="time2">结束日期</param>
        /// <returns>随机日期</returns>
        public static DateTime GetRandomTime(DateTime time1, DateTime time2)
        {
            Random random = new Random();
            DateTime minTime = new DateTime();
            DateTime maxTime = new DateTime();

            System.TimeSpan ts = new System.TimeSpan(time1.Ticks - time2.Ticks);

            // 获取两个时间相隔的秒数
            double dTotalSecontds = ts.TotalSeconds;
            int iTotalSecontds = 0;

            if (dTotalSecontds > System.Int32.MaxValue)
            {
                iTotalSecontds = System.Int32.MaxValue;
            }
            else if (dTotalSecontds < System.Int32.MinValue)
            {
                iTotalSecontds = System.Int32.MinValue;
            }
            else
            {
                iTotalSecontds = (int)dTotalSecontds;
            }


            if (iTotalSecontds > 0)
            {
                minTime = time2;
                maxTime = time1;
            }
            else if (iTotalSecontds < 0)
            {
                minTime = time1;
                maxTime = time2;
            }
            else
            {
                return time1;
            }

            int maxValue = iTotalSecontds;

            if (iTotalSecontds <= System.Int32.MinValue)
                maxValue = System.Int32.MinValue + 1;

            int i = random.Next(System.Math.Abs(maxValue));

            return minTime.AddSeconds(i);
        } 
        #endregion

        #region 快速验证一个字符串是否符合指定的正则表达式。
        /// <summary>
        /// 快速验证一个字符串是否符合指定的正则表达式。
        /// </summary>
        /// <param name="_express">正则表达式的内容。</param>
        /// <param name="_value">需验证的字符串。</param>
        /// <returns>是否合法的bool值。</returns>
        public static bool QuickValidate(string _express, string _value)
        {
            if (_value == null) return false;
            System.Text.RegularExpressions.Regex myRegex = new System.Text.RegularExpressions.Regex(_express);
            if (_value.Length == 0)
            {
                return false;
            }
            return myRegex.IsMatch(_value);
        } 
        #endregion

        #region 把字符串转成整型
        /// <summary>
        /// 把字符串转成整型
        /// </summary>
        /// <param name="_value">字符串</param>
        /// <param name="_defValue">默认值</param>
        /// <returns>转换后的整型</returns>
        public static int StrToInt(string _value, int _defValue)
        {
            if (IsNum(_value))
                return int.Parse(_value);
            else
                return _defValue;
        } 
        #endregion

        #region 检查一个字符串是否可以转化为日期
        /// <summary>
        /// 检查一个字符串是否可以转化为日期
        /// </summary>
        /// <param name="_value">日期字符串</param>
        /// <returns></returns>
        public static bool IsStringDate(string _value)
        {
            DateTime dt;
            try
            {
                dt = DateTime.Parse(_value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        } 
        #endregion

        #region 检查一个字符串是否可以转化为日期，然后验证是否符合格式，默认格式 "yyyy-MM-dd"
        /// <summary>
        /// 检查一个字符串是否可以转化为日期，然后验证是否符合格式，默认格式 "yyyy-MM-dd"
        /// </summary>
        /// <param name="_value">日期字符串</param>
        /// <returns></returns>
        public static bool IsStringDateAndFormat(string _value)
        {
            return IsStringDateAndFormat(_value, "yyyy-MM-dd");
        } 
        #endregion

        #region 检查一个字符串是否可以转化为日期，然后验证是否符合格式
        /// <summary>
        /// 检查一个字符串是否可以转化为日期，然后验证是否符合格式
        /// </summary>
        /// <param name="_value">日期字符串</param>
        /// <param name="_dateformat">需验证日期格式 如 yyyy-MM-dd HH:mm:ss</param>
        /// <returns></returns>
        public static bool IsStringDateAndFormat(string _value, string _dateformat)
        {
            DateTime dt;
            return DateTime.TryParseExact(_value, _dateformat, null, System.Globalization.DateTimeStyles.None, out dt);
        } 
        #endregion

        #region 是否为日期型字符串，例如 2008-05-08
        /// <summary>
        /// 是否为日期型字符串，例如 2008-05-08
        /// </summary>
        /// <param name="_value">日期字符串</param>
        /// <returns></returns>
        public static bool IsStringDateByRegex(string _value)
        {
            return Regex.IsMatch(_value, @"^((((1[6-9]|[2-9]/d)/d{2})-(0?[13578]|1[02])-(0?[1-9]" + @"|[12]/d|3[01]))|(((1[6-9]|[2-9]/d)/d{2})-(0?[13456789]|" + @"1[012])-(0?[1-9]|[12]/d|30))|(((1[6-9]|[2-9]/d)/d{2})-0?" + @"2-(0?[1-9]|1/d|2[0-9]))|(((1[6-9]|[2-9]/d)(0[48]|[2468]" + @"[048]|[13579][26])|((16|[2468][048]|[3579][26])00))-0?2-29-))$");
        } 
        #endregion

        #region 是否为时间型字符串，例如 15:00:00
        /// <summary>
        /// 是否为时间型字符串，例如 15:00:00
        /// </summary>
        /// <param name="_value">时间字符串</param>
        /// <returns></returns>
        public static bool IsStringTimeByRegex(string _value)
        {
            return Regex.IsMatch(_value, @"^((20|21|22|23|[0-1]?/d):[0-5]?/d:[0-5]?/d)$");
        } 
        #endregion

        #region 是否为日期+时间型字符串，例如 2008-05-08 15:00:00
        /// <summary>
        /// 是否为日期+时间型字符串，例如 2008-05-08 15:00:00
        /// </summary>
        /// <param name="_value">时间字符串</param>
        /// <returns></returns>
        public static bool IsStringDateTimeByRegex(string _value)
        {
            return Regex.IsMatch(_value, @"^(((((1[6-9]|[2-9]/d)/d{2})-(0?[13578]|1[02])-(0?" + @"[1-9]|[12]/d|3[01]))|(((1[6-9]|[2-9]/d)/d{2})-(0?" + @"[13456789]|1[012])-(0?[1-9]|[12]/d|30))|(((1[6-9]" + @"|[2-9]/d)/d{2})-0?2-(0?[1-9]|1/d|2[0-8]))|(((1[6-" + @"9]|[2-9]/d)(0[48]|[2468][048]|[13579][26])|((16|[" + @"2468][048]|[3579][26])00))-0?2-29-)) (20|21|22|23" + @"|[0-1]?/d):[0-5]?/d:[0-5]?/d)$ ");
        } 
        #endregion

        #region 把字符串转成日期
        /// <summary>
        /// 把字符串转成日期
        /// </summary>
        /// <param name="_value">字符串</param>
        /// <param name="_defValue">默认值</param>
        /// <returns>转换后的日期</returns>
        public static DateTime StrToDate(string _value, DateTime _defValue)
        {
            if (IsStringDate(_value))
                return Convert.ToDateTime(_value);
            else
                return _defValue;
        } 
        #endregion

        #region 转全角的函数(SBC case) 
        /// <summary> 
        /// 转全角的函数(SBC case) 
        /// </summary> 
        /// <param name="input">任意字符串</param> 
        /// <returns>全角字符串</returns> 
        ///<remarks> 
        ///全角空格为12288，半角空格为32 
        ///其他字符半角(33-126)与全角(65281-65374)的对应关系是：均相差65248 
        ///</remarks> 
        public static string ToSBC(string input)
        {
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 32)
                {
                    c[i] = (char)12288;
                    continue;
                }
                if (c[i] < 127)
                    c[i] = (char)(c[i] + 65248);
            }
            return new string(c);
        } 
        #endregion

        #region 转半角的函数(DBC case) 
        /// <summary> 
        /// 转半角的函数(DBC case) 
        /// </summary> 
        /// <param name="input">任意字符串</param> 
        /// <returns>半角字符串</returns> 
        ///<remarks> 
        ///全角空格为12288，半角空格为32 
        ///其他字符半角(33-126)与全角(65281-65374)的对应关系是：均相差65248 
        ///</remarks> 
        public static string ToDBC(string input)
        {
            char[] c = input.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 12288)
                {
                    c[i] = (char)32;
                    continue;
                }
                if (c[i] > 65280 && c[i] < 65375)
                    c[i] = (char)(c[i] - 65248);
            }
            return new string(c);
        } 
        #endregion

        #region 文件下载
        /// <summary>
        /// 文件下载
        /// </summary>
        /// <param name="filepath"></param>
        public static void DownloadFile(string filepath)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(GetMapPath(filepath));

            HttpContext.Current.Response.ContentType = "application/ms-download";
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.AddHeader("Content-Type", "application/octet-stream");
            HttpContext.Current.Response.Charset = "utf-8";
            HttpContext.Current.Response.AddHeader("Content-Disposition", "attachment;filename=" + System.Web.HttpUtility.UrlEncode(file.Name, System.Text.Encoding.UTF8));
            HttpContext.Current.Response.AddHeader("Content-Length", file.Length.ToString());
            HttpContext.Current.Response.WriteFile(file.FullName);
            HttpContext.Current.Response.Flush();
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.End();
        } 
        #endregion

        #region 压缩Html
        /// <summary>
        /// 压缩Html
        /// </summary>
        /// <param name="Html">Html</param>
        /// <returns>压缩后的html代码</returns>
        public static string ZipHtml(string Html)
        {
            Html = Regex.Replace(Html, @">\s+?<", "><");//去除Html中的空白字符.
            Html = Regex.Replace(Html, @"\r\n\s*", "");
            Html = Regex.Replace(Html, @"<body([\s|\S]*?)>([\s|\S]*?)</body>", @"<body$1>$2</body>", RegexOptions.IgnoreCase);
            return Html;
        } 
        #endregion

        #region 读取文件
        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="tempDir">文件路径</param>
        /// <returns>读取的文件内容</returns>
        public static string ReadFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                StreamReader sr = new StreamReader(filePath, System.Text.Encoding.GetEncoding("utf-8"));
                string str = sr.ReadToEnd();
                sr.Close();
                return str;
            }
            else
            {
                return "File Error";
            }
        } 
        #endregion

        #region 创建目录(已存在则不创建)
        /// <summary>
        /// 创建目录(已存在则不创建)
        /// </summary>
        /// <param name="directorypath">路径</param>
        public static void CreateDir(string directorypath)
        {
            try
            {
                if (!Directory.Exists(directorypath))
                {
                    Directory.CreateDirectory(directorypath);
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
        } 
        #endregion

        #region 返回文件扩展名，不含“.”
        /// <summary>
        /// 返回文件扩展名，不含“.”
        /// </summary>
        /// <param name="_filepath">文件全名称</param>
        /// <returns>string</returns>
        public static string GetFileExt(string _filepath)
        {
            if (string.IsNullOrEmpty(_filepath))
            {
                return string.Empty;
            }
            if (_filepath.LastIndexOf(".") > 0)
            {
                return _filepath.Substring(_filepath.LastIndexOf(".") + 1); //文件扩展名，不含“.”
            }
            return string.Empty;
        } 
        #endregion

        #region 生成日期随机码
        /// <summary>
        /// 生成日期随机码
        /// </summary>
        /// <returns></returns>
        public static string GetRamCode()
        {
            #region
            return DateTime.Now.ToString("yyyyMMddHHmmssffff");
            #endregion
        }
        #endregion

        #region 生成随机字符串 

        ///<summary>
        ///生成随机字符串 
        ///</summary>
        ///<param name="length">目标字符串的长度</param>
        ///<param name="useNum">是否包含数字，1=包含，默认为包含</param>
        ///<param name="useLow">是否包含小写字母，1=包含，默认为包含</param>
        ///<param name="useUpp">是否包含大写字母，1=包含，默认为包含</param>
        ///<param name="useSpe">是否包含特殊字符，1=包含，默认为不包含</param>
        ///<param name="custom">要包含的自定义字符，直接输入要包含的字符列表</param>
        ///<returns>指定长度的随机字符串</returns>
        public static string GetRandomString(int length, bool useNum, bool useLow, bool useUpp, bool useSpe, string custom)
        {
            byte[] b = new byte[4];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(b);
            Random r = new Random(BitConverter.ToInt32(b, 0));
            string s = null, str = custom;
            if (useNum == true) { str += "0123456789"; }
            if (useLow == true) { str += "abcdefghijklmnopqrstuvwxyz"; }
            if (useUpp == true) { str += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; }
            if (useSpe == true) { str += "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"; }
            for (int i = 0; i < length; i++)
            {
                s += str.Substring(r.Next(0, str.Length - 1), 1);
            }
            return s;
        }
        #endregion

        #region 获得当前绝对路径
        /// <summary>
        /// 获得当前绝对路径
        /// </summary>
        /// <param name="strPath">指定的路径</param>
        /// <returns>绝对路径</returns>
        public static string GetMapPath(string strPath)
        {
            if (strPath.ToLower().StartsWith("http://"))
            {
                return strPath;
            }
            if (HttpContext.Current != null)
            {
                return HttpContext.Current.Server.MapPath(strPath);
            }
            else //非web程序引用
            {
                strPath = strPath.Replace("/", "\\");
                if (strPath.StartsWith("\\"))
                {
                    strPath = strPath.Substring(strPath.IndexOf('\\', 1)).TrimStart('\\');
                }
                return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, strPath);
            }
        } 
        #endregion

        #region 获取指定路径文件夹内文件列表,可指定文件类型
        /// <summary>
        /// 获取指定路径文件夹内文件列表,可指定文件类型
        /// </summary>
        /// <param name="dirPath">路径</param>
        /// <param name="searchop">文件类型或匹配规则</param>
        /// <returns></returns>
        public static List<string> GetDirFileList(string dirPath, string searchop)
        {
            List<string> images = new List<string>();
            string[] paths = Directory.GetFiles(GetMapPath(dirPath), searchop);
            foreach (string path in paths)
            {
                images.Add(Path.Combine(dirPath, Path.GetFileName(path)));
            }
            return images;
        } 
        #endregion

        #region 判断文件是否存在
        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="filepath">路径</param>
        /// <returns>返回bool值</returns>
        public static bool FileExists(string filepath)
        {
            return File.Exists(filepath);
        } 
        #endregion

        #region 删除文件
        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePath">路径</param>
        public static void DeleteFile(string filePath)
        {
            //存在则删除
            if (FileExists(filePath)) File.Delete(filePath);
        }
        #endregion

        #region 创建Html文件
        /// <summary>
        /// 创建Html文件
        /// </summary>
        /// <param name="filePath">路径</param>
        /// <param name="text">内容</param>
        public static void CreateHtmlFile(string filePath, string text)
        {
            try
            {
                StreamWriter sw = new StreamWriter(filePath, false, Encoding.GetEncoding("UTF-8"));
                sw.WriteLine(text);
                sw.Flush();
                sw.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        } 
        #endregion

        #region 删除文件夹,和所有文件
        /// <summary>   
        /// 删除文件夹,和所有文件
        /// </summary>   
        /// <param name="dir">带文件夹名的路径</param>   
        /// <param name="deldir">是否删除文件夹本身和所有子文件夹</param>  
        public static void DeleteDir(string dir, bool deldir)
        {
            if (Directory.Exists(dir)) //如果存在这个文件夹删除之    
            {
                foreach (string d in Directory.GetFileSystemEntries(dir))
                {
                    if (File.Exists(d))
                        File.Delete(d); //直接删除其中的文件                           
                    else
                        DeleteDir(d, deldir); //递归删除子文件夹    
                }
                if (deldir) Directory.Delete(dir, true);
            }
        } 
        #endregion

        #region 获取远程页面内容
        /// <summary>
        /// 获取远程页面内容
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">编码</param>
        /// <returns>返回html</returns>
        public static string GetHttp(string url, Encoding encoding)
        {
            WebResponse response = null;
            string str;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = HttpContext.Current.Request.UserAgent;
                request.Timeout = 5000;
                request.AllowAutoRedirect = true;
                request.MaximumAutomaticRedirections = 2;
                response = request.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), encoding))
                {
                    str = reader.ReadToEnd();
                }
            }
            catch
            {
                str = "error";
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
            return str;
        } 
        #endregion

    }
}
