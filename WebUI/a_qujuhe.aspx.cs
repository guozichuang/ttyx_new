using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.Common;
using System.Text;
using System.IO;
using System.Net;
using System.Security.Cryptography;

using Simon.Common;
using LitJson;

public partial class a_qujuhe : System.Web.UI.Page
{
    const string PAY_MEMBERID = "15110";  //商户号
    const string PAY_BANKCODE = "908";  //银行代码
    const string APP_KEY = "focbu8s471tr1cmxpt0o4qzk7mc94wfo";  //api密匙

    const string PAY_URL = "https://api.qujuhe.com/pay_index";  //网关地址
    const string NOTIFY_URL = "http://api-mobilegame-test-000.kk838.com/a_qujuhe_notify_url.aspx";  //异步通知URL

    protected void Page_Load(object sender, EventArgs e)
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string rechargermb = SimonUtils.Qnum("rechargermb"); //充值金额(人民币)  
        string payip = Request.Params["payip"];  //用户IP

        if (userid.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户ID错误(数字类型)"));
        }
        if (rechargermb.Length < 1)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额(人民币)错误(数字类型)"));
        }
        if (string.IsNullOrWhiteSpace(payip))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户IP错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", userid) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //判断充值兑换率
        DataTable RechargeRateDT = SimonDB.DataTable(@"select * from RechargeRate where RechargeRMB=@RechargeRMB", new DbParameter[] {
            SimonDB.CreDbPar("@RechargeRMB", rechargermb)
        });
        if (RechargeRateDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "此充值金额的金币兑换率不存在"));
        }
        DataRow RechargeRateDR = RechargeRateDT.Rows[0];

        //创建订单
        string _orderdes = "充值金额:" + RechargeRateDR["RechargeRMB"].ToString() + " 兑换金币:" + RechargeRateDR["RechargeGold"].ToString() + " 赠送金币:" + RechargeRateDR["RegiveGold"].ToString();
        string _ordernum = CurrSite.GenNewOrderNum();
        while (((int)SimonDB.ExecuteScalar(@"select count(*) from Web_RMBCost where OrderID=@ordernum", new DbParameter[] {
            SimonDB.CreDbPar("@ordernum", _ordernum)
        })) > 0)
        {
            _ordernum = CurrSite.GenNewOrderNum();
        }

        List<DbParameter> rmbcost_lpar = new List<DbParameter>();
        rmbcost_lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        //rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb == "0.01" ? "6" : rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayMoney", rechargermb));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "151"));  //去聚合支付（www.qujuhe.com） 支付类型设置为151  
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "去聚合支付"));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", _ordernum));  //订单号
        rmbcost_lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", false));  //In状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));  //Pay状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态 0 未更新  1 已更新
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PurchaseType", "1"));  //充值金币1 充值元宝2
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayIP", payip));

        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                                   ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                                   PayIP)
                                                           values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                                   @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                                   @PayIP)", rmbcost_lpar.ToArray());

        //对接去聚合
        //参数名称 参数含义    是否必填 参与签名    参数说明
        //pay_memberid    商户号 是   是 平台分配商户号
        //pay_orderid 订单号 是 是   上送订单号唯一, 最大字符长度32
        //pay_applydate   提交时间 是   是 时间格式：2016-12-26 18:18:18
        //pay_bankcode 银行编码    是 是   参考后续说明
        //pay_notifyurl   服务端通知 是   是 服务端返回地址/支付回调（POST返回数据）
        //pay_callbackurl 页面跳转通知  是 是   页面跳转返回**【现阶段不能自动跳转】**
        //pay_amount 订单金额    是 是   商品金额
        //pay_md5sign MD5签名 是   否 请看MD5签名字段格式
        //pay_productname 商品名称    是 否   中文或数字或字母
        //sub_openid  公众号用户的openid 否   否 公众号支付此项必填，请检查公众号是否绑定获取用户openid
        //pay_deviceIp    设备真实IP地址 否   否 H5支付此项必填
        //pay_scene 支付场景（Wap，IOS，Android）   否 否   H5支付此项必填，默认为Wap，区分大小写
        //pay_attach  附加字段 否   否 此字段在返回时按原样返回(中文需要url编码)
        //pay_productnum 商户品数量   否 否
        //pay_productdesc 商品描述    否 否
        //pay_producturl 商户链接地址  否 否
        string parms = "pay_amount=" + rechargermb
                     + "&pay_applydate=" + DateTime.Today.ToString()
                     + "&pay_bankcode=" + PAY_BANKCODE
                     + "&pay_callbackurl=" + NOTIFY_URL
                     + "&pay_memberid=" + PAY_MEMBERID
                     + "&pay_notifyurl=" + NOTIFY_URL
                     + "&pay_orderid=" + _ordernum
                     + "&key=" + APP_KEY;

        string sParmsMd5 = GetMD5String(parms).ToUpper();
        parms += "&pay_md5sign=" + sParmsMd5;
        parms += "&pay_deviceIp=" + payip;
        parms += "&pay_productname=chongzhi";

        parms = SimonUrl.UpdateParam(parms, "key", "");  //加密计算后,提交POST参数时置空参数key,更安全

        string receive_str = PostRequest(PAY_URL, parms);  //去聚合要求使用post模式
        //SimonUtils.RespWNC(receive_str);

        JsonData receive_jd = null;
        try
        {
            receive_jd = JsonMapper.ToObject(receive_str);
        }
        catch { SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "Json异常")); }

        JsonData out_jd = new JsonData();
        if (receive_jd["status"].ToString().ToLower() == "success")
        {
            out_jd["code"] = "1";
            out_jd["msg"] = "success";
            out_jd["results"] = new JsonData();
            out_jd["results"]["orderid"] = _ordernum;
            out_jd["results"]["payurl"] = receive_jd["data"]["code_url"].ToString();
            SimonUtils.RespWNC(out_jd.ToJson());
        }
        else
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 去聚合 支付 错误日志-----------------------------------------------------------------------------------");
            sb.Append("\r\n receive_str: " + receive_str);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_qujuhepay_error_" + DateTime.Now.ToString("yyyyMMdd"));

            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "支付接口网关报错,支付失败"));
        }
    }

    /// <summary>
    /// 前端API验签检查(辅助方法)
    /// </summary>
    private void CheckSign()
    {
        string t = SimonUtils.Qnum("t");  //unix时间戳 (10位数字)
        string sign = SimonUtils.Q("sign");  //签名

        if (t.Length != 10)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "时间戳错误"));
        }
        if (CurrSite.ApiCallTimeOut(t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "请求超时"));
        }
        if (!CurrSite.VerifySign(sign, t))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "签名错误"));
        }
    }

    /// <summary>
    /// GET请求
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    protected string GetRequest(string url)
    {
        HttpWebRequest httpWebRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
        httpWebRequest.Method = "GET";
        httpWebRequest.ServicePoint.Expect100Continue = false;

        StreamReader responseReader = null;
        string responseData;
        try
        {
            responseReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
            responseData = responseReader.ReadToEnd();
        }
        finally
        {
            httpWebRequest.GetResponse().GetResponseStream().Close();
            responseReader.Close();
        }

        return responseData;
    }

    /// <summary>
    /// POST请求
    /// </summary>
    /// <param name="url"></param>
    /// <param name="postData"></param>
    /// <returns></returns>
    protected string PostRequest(string url, string postData)
    {
        HttpWebRequest httpWebRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
        httpWebRequest.Method = "POST";
        httpWebRequest.ServicePoint.Expect100Continue = false;
        httpWebRequest.ContentType = "application/x-www-form-urlencoded";

        //写入POST参数
        StreamWriter requestWriter = new StreamWriter(httpWebRequest.GetRequestStream());
        try
        {
            requestWriter.Write(postData);
        }
        finally
        {
            requestWriter.Close();
        }

        //读取请求后的结果
        StreamReader responseReader = null;
        string responseData;
        try
        {
            responseReader = new StreamReader(httpWebRequest.GetResponse().GetResponseStream());
            responseData = responseReader.ReadToEnd();
        }
        finally
        {
            httpWebRequest.GetResponse().GetResponseStream().Close();
            responseReader.Close();
        }

        return responseData;
    }

    public static string GetMD5String(string str)
    {
        MD5 md5 = MD5.Create();
        byte[] data = Encoding.UTF8.GetBytes(str);
        byte[] data2 = md5.ComputeHash(data);
        return GetbyteToString(data2);
    }

    private static string GetbyteToString(byte[] data)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("x2"));
        }
        return sb.ToString();
    }
}