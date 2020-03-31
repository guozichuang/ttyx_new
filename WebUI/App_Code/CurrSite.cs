using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Data;
using System.Data.Common;
using System.Web.Routing;
using System.Text.RegularExpressions;
using System.Net.Mail;
using System.Configuration;

using Microsoft.International.Converters.PinYinConverter;  //微软拼音转换库
using LitJson;
using Simon.Common;

/// <summary>
/// CurrSite 常用方法
/// </summary>
public class CurrSite
{
    public CurrSite()
    {
    }

    #region AliPay支付接口相关

    public static string Alipay_appid = GetAppSettings("alipay_appid");
    public static string Alipay_public_key = GetAppSettings("alipay_public_key");
    public static string Alipay_app_private_key = GetAppSettings("alipay_app_private_key");
    public static string Alipay_notify_url = GetAppSettings("alipay_notify_url");

    #endregion

    #region 15173支付接口相关

    public static string Pay15173_bargainor_id = GetAppSettings("pay15173_bargainor_id");
    public static string Pay15173_key = GetAppSettings("pay15173_key");
    public static string Pay15173_return_url = GetAppSettings("pay15173_return_url");
    public static string Pay15173_select_url = GetAppSettings("pay15173_select_url");
    public static string Pay15173_toalipay_url = GetAppSettings("pay15173_toalipay_url");
    public static string Pay15173_towxpay_url = GetAppSettings("pay15173_towxpay_url");
    public static string Pay15173_towxpay_pc_url = GetAppSettings("pay15173_towxpay_pc_url");

    #endregion

    #region 竣付通支付接口相关

    public static string jft_yingyongnum = GetAppSettings("jft_yingyongnum");
    public static string jft_compkey = GetAppSettings("jft_compkey");
    public static string jft_return_url = GetAppSettings("pay15173_return_url");
    public static string jft_select_url = GetAppSettings("jft_select_url");
    public static string jft_post_url = GetAppSettings("jft_post_url");

    #endregion


    #region API相关

    /// <summary>
    /// 是否启用管理端API验签检查 true false
    /// </summary>
    public static bool CheckAdminSign = bool.Parse(GetAppSettings("checkadminsign"));

    /// <summary>
    /// 是否启用注册推荐人 true false
    /// </summary>
    public static bool EnableRegRec = bool.Parse(GetAppSettings("enableregrec"));

    /// <summary>
    /// 是否启用俱乐部创建审核 true false
    /// </summary>
    public static bool EnableCreateClub = bool.Parse(GetAppSettings("enablecreateclub"));

    /// <summary>
    /// 是否启用俱乐部加入审核 true false
    /// </summary>
    public static bool EnableJoinClub = bool.Parse(GetAppSettings("enablejoinclub"));

    /// <summary>
    /// 是否启用俱乐部加入其他审核 true false
    /// </summary>
    public static bool EnableJoinOtherClub = bool.Parse(GetAppSettings("enablejoinotherclub"));

    /// <summary>
    /// 是否启用金币下分功能 true false
    /// </summary>
    public static bool EnableCashPrize = bool.Parse(GetAppSettings("enablecashprize"));

    /// <summary>
    /// Cookie超时时间(分钟)
    /// </summary>
    public static int CookieExp = int.Parse(GetAppSettings("cookieexp"));


    /// <summary>
    /// Api调用是否超时 (true 超时， false 未超时)
    /// </summary>
    /// <param name="t">Unix时间戳</param>
    /// <returns></returns>
    public static bool ApiCallTimeOut(string t)
    {
        int Api_CallTimeOut = int.Parse(GetAppSettings("api_calltimeout"));  //Api超时时间设置 (单位：秒)
        DateTime unixtime = SimonTimeParser.UnixTimeStampToDateTime(double.Parse(t));
        return (DateTime.Now > unixtime.AddSeconds(Api_CallTimeOut));
    }
    /// <summary>
    /// 前端Api验签 (true 验证通过， false 验证失败)
    /// </summary>
    /// <param name="signstr">签名</param>
    /// <param name="t">Unix时间戳</param>
    /// <returns></returns>
    public static bool VerifySign(string signstr, string t)
    {
        string Api_KeyID = GetAppSettings("api_keyid");
        string Api_Secret = GetAppSettings("api_secret");
        return (signstr.Equals(SimonUtils.EnCodeMD5(Api_KeyID + t + Api_Secret), StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// 推荐人管理端Api验签 (true 验证通过， false 验证失败)
    /// </summary>
    /// <param name="signstr">签名</param>
    /// <param name="t">时间戳</param>
    /// <returns></returns>
    public static bool RecUserVerifySign(string signstr, string t)
    {
        string RecUser_KeyID = GetAppSettings("recuser_keyid");
        string RecUser_Secret = GetAppSettings("recuser_secret");
        return (signstr.Equals(SimonUtils.EnCodeMD5(RecUser_KeyID + t + RecUser_Secret), StringComparison.OrdinalIgnoreCase));
    }
    /// <summary>
    /// 管理端Api验签 (true 验证通过， false 验证失败)
    /// </summary>
    /// <param name="signstr">签名</param>
    /// <param name="t">时间戳</param>
    /// <returns></returns>
    public static bool AdminVerifySign(string signstr, string t)
    {
        string Admin_KeyID = GetAppSettings("admin_keyid");
        string Admin_Secret = GetAppSettings("admin_secret");
        return (signstr.Equals(SimonUtils.EnCodeMD5(Admin_KeyID + t + Admin_Secret), StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    private static object _lock = new object();
    /// <summary>
    /// 生成订单号
    /// </summary>
    /// <returns></returns>
    public static string GenNewOrderNum()
    {
        string _temp_orderno = string.Empty;
        _temp_orderno += DateTime.Now.ToString("yyMMdd");
        _temp_orderno += DateTime.Now.ToString("fffffff").Substring(1, 6);
        _temp_orderno += new Random().Next(10, 99).ToString();
        return _temp_orderno;
    }

    /// <summary>
    /// 生成UserName
    /// </summary>
    /// <returns></returns>
    public static string GenNewUserName(string nicknamestr)
    {
        nicknamestr = SimonUtils.StrCut2(nicknamestr, 5, "");
        string _temp_nicknameext = string.Empty;
        _temp_nicknameext += DateTime.Now.ToString("fffffff").Substring(1, 3);
        return (nicknamestr + _temp_nicknameext).Replace("-", "_").Trim('_'); //适应UserName和NickName规则
    }

    /// <summary>
    /// 获取RouteData.Values值(数字)
    /// </summary>
    /// <param name="page">page对象,一般为this</param>
    /// <param name="str">参数</param>
    /// <returns></returns>
    public static string GetRdvNum(Page page, string str)
    {
        if (page.RouteData.Values[str] != null && page.RouteData.Values[str].ToString().Trim() != string.Empty && SimonUtils.IsNum(page.RouteData.Values[str].ToString().Trim()))
            return page.RouteData.Values[str].ToString().Trim();
        return string.Empty;
    }

    /// <summary>
    /// 获取RouteData.Values值(字符串,中英文,不能带特殊符号)
    /// </summary>
    /// <param name="page">page对象,一般为this</param>
    /// <param name="str">参数</param>
    /// <returns></returns>
    public static string GetRdv(Page page, string str)
    {
        if (page.RouteData.Values[str] != null && page.RouteData.Values[str].ToString().Trim() != string.Empty && SimonUtils.IsEC(page.RouteData.Values[str].ToString().Trim()))
            return page.RouteData.Values[str].ToString().Trim();
        return string.Empty;
    }

    /// <summary>
    /// 获取Json格式错误信息
    /// </summary>
    /// <param name="errcode">错误代码</param>
    /// <param name="errmsg">错误信息</param>
    /// <param name="results">输出信息</param>
    /// <returns></returns>
    public static string GetErrJson(string errcode, string errmsg)
    {
        Dictionary<string,object> dic = new Dictionary<string,object>();
        dic.Add("code",errcode);
        dic.Add("msg", errmsg);
        dic.Add("results", null);
        return JsonMapper.ToJson(dic);
    }

    /// <summary>
    /// 根据Dictionary值获得键名
    /// </summary>
    /// <param name="val">值</param>
    /// <param name="dic">Dictionary</param>
    /// <returns>key</returns>
    public static string GetDicKeyByValue(string val, Dictionary<string, string> dic)
    {
        string result = string.Empty;
        foreach (string key in dic.Keys)
        {
            if (dic[key] == val) result = key;
        }
        return result;
    }
    public static Dictionary<string, string> TempDic
    {
        get
        {
            Dictionary<string, string> _dic = new Dictionary<string, string>();
            _dic.Add("项一", "0");
            _dic.Add("项二", "1");
            return _dic;
        }
    }

    /// <summary> 
    /// 汉字转化为拼音
    /// </summary> 
    /// <param name="str">汉字</param> 
    /// <returns>全拼</returns> 
    public static string GetPinYin(string str)
    {
        string r = string.Empty;
        if (str.Length > 0)
        {
            foreach (char obj in str)
            {
                try
                {
                    ChineseChar chineseChar = new ChineseChar(obj);
                    string t = chineseChar.Pinyins[0].ToString();
                    r += t.Substring(0, t.Length - 1);
                }
                catch
                {
                    r += obj.ToString();
                }
            }
        }
        return r;
    }

    /// <summary>
    /// IP地址转换为地理位置
    /// </summary>
    /// <param name="ipstr"></param>
    /// <returns></returns>
    public static string GetIPQQWryLocator(string ipstr)
    {
        string result = string.Empty;
        try
        {
            QQWry.NET.QQWryLocator qqWry = new QQWry.NET.QQWryLocator(SimonUtils.GetMapPath("/App_Data/qqwry.dat"));
            QQWry.NET.IPLocation ipl = qqWry.Query(ipstr);
            result = string.Format("{0} {1} {2}", ipl.IP, ipl.Country, ipl.Local);
        }
        catch
        {
            result = "error";
        }

        return result;
    }

    /// <summary>
    /// 获取AppSettings设置的值
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string GetAppSettings(string key)
    {
        if (ConfigurationManager.AppSettings[key] != null)
        {
            return ConfigurationManager.AppSettings[key];
        }
        return "";
    }

}
