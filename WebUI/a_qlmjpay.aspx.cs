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

public partial class a_qlmjpay : System.Web.UI.Page
{
    //const string pay_url = "http://pay.uukudear9.cn/gateway/dopay";
    //const string s_key = "FFF3C76820904D10916ECBB568344468";
    //const string str_merchant_id = "10928";
    //const string str_return_url = "http://129.226.59.89/a_qlmjpay_return_url.aspx";
    //const string str_notify_url = "http://129.226.59.89/a_qlmjpay_notify_url.aspx";

    const string pay_url = "http://pay.uukudear9.cn/gateway/dopay";
    const string s_key = "57D492C965574527A769E1C61E99F91D";
    const string str_merchant_id = "11415";
    const string str_return_url = "http://114.55.209.104/a_qlmjpay_return_url.aspx";
    const string str_notify_url = "http://114.55.209.104/a_qlmjpay_notify_url.aspx";

    protected void Page_Load(object sender, EventArgs e)
    {
        CheckSign();
        string userid = SimonUtils.Qnum("userid");  //用户ID 
        string rechargermb = SimonUtils.Qnum("rechargermb"); //充值金额(人民币)  
        string payip = Request.Params["payip"];  //用户IP
        string paytype= Request.Params["paytype"];  //支付方式。微信固码8001013,支付宝当面付8001024
        string purchasetype = Request.Params["purchasetype"];  //货币类型：1，金币；2房卡

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
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "162"));  //暴风雪支付（http://pay.uukudear9.cn/gateway/dopay） 支付类型设置为162  
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "baofeng"+paytype));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", _ordernum));  //订单号
        rmbcost_lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", false));  //In状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));  //Pay状态
        rmbcost_lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["WalletMoney"].ToString()));
        rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态 0 未更新  1 已更新
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PurchaseType", purchasetype));  //充值金币1 充值房卡2
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayIP", payip));

        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                                   ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                                   PayIP)
                                                           values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                                   @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                                   @PayIP)", rmbcost_lpar.ToArray());

        string str_out_trade_no = _ordernum;
        string str_amount = string.Format("{0:N2}", int.Parse(rechargermb));
        string str_ordertime = DateTime.Now.ToString("yyyyMMddHHmmss");
        string str_nonce_str = SimonUtils.GetRandomString(20, true, false, true, false, "");
        string str_sign = SimonUtils.EnCodeMD5("amount=" + str_amount + "&" + "device_type=wap" + "&" + "merchant_id=" + str_merchant_id + "&" + "nonce_str=" + str_nonce_str + "&" + "notify_url=" + str_notify_url + "&" + "out_trade_no=" + str_out_trade_no + "&" + "pay_ip=" + payip + "&" + "pay_type=" + paytype + "&" + "request_time=" + str_ordertime + "&" + "return_url=" + str_return_url + "&" + "version=V2.0" + "&" + s_key).ToUpper();
        //string str_sign = SimonUtils.EnCodeMD5("merchant_id=" + str_merchant_id + "&" + "version=V2.0" + "&" + "pay_type=" + paytype + "&" + "device_type=wap" + "&" + "request_time=" + str_ordertime + "&" + "nonce_str=" + str_nonce_str + "&" + "pay_ip=" + payip + "&" + "out_trade_no=" + str_out_trade_no + "&" + "amount=" + str_amount +"&"+ "notify_url=" + str_notify_url +"&" +"&" + "return_url=" + str_return_url  + "&" + s_key).ToUpper();

        //form1.Action = pay_url;
        //amount.Value = str_amount;
        //device_type.Value = "wap";
        //merchant_id.Value = str_merchant_id;
        //nonce_str.Value = str_nonce_str;
        //notify_url.Value = str_notify_url;
        //out_trade_no.Value = str_out_trade_no;
        //pay_ip.Value = payip;
        //pay_type.Value = paytype;
        //request_time.Value = str_ordertime;
        //return_url.Value = str_return_url;
        //sign.Value = str_sign;
        //version.Value = "V2.0";

        //ScriptManager.RegisterStartupScript(this.Page, GetType(), "post1", "Post();", true);


        //对接暴风雪支付


        string parms = "amount=" + str_amount
                    + "&device_type=wap" 
                    + "&merchant_id=" + str_merchant_id
                    + "&nonce_str=" + str_nonce_str
                    + "&notify_url=" + str_notify_url
                    + "&out_trade_no=" + str_out_trade_no
                    + "&pay_ip=" + payip
                    + "&pay_type=" + paytype
                    + "&request_time=" + str_ordertime
                    + "&return_url=" + str_return_url
                    + "&version=V2.0";

        string sParmsMd5 = GetMD5String(parms + s_key).ToUpper();
        parms += "&sign=" + sParmsMd5;

        //parms = SimonUrl.UpdateParam(parms, "s_key", "");  //加密计算后,提交POST参数时置空参数key,更安全


        StringBuilder str = new StringBuilder();
        str.Append("{");
        str.Append("amount:\"" + str_amount + "\",");
        str.Append("device_type:\"" + "wap" + "\",");
        str.Append("merchant_id:\"" + str_merchant_id + "\",");
        str.Append("nonce_str:\"" + str_nonce_str + "\",");
        str.Append("notify_url:\"" + str_notify_url + "\",");
        str.Append("out_trade_no:\"" + str_out_trade_no + "\",");
        str.Append("pay_ip:\"" + payip + "\",");
        str.Append("pay_type:\"" + paytype + "\",");
        str.Append("request_time:\"" + str_ordertime + "\",");
        str.Append("return_url:\"" + str_return_url + "\",");
        str.Append("version:\"" + "V2.0" + "\",");
        str.Append("sign:\"" + sParmsMd5 + "\"");
        str.Append("}");
        string josnParms = str.ToString();

        //JObject jo = (JObject)JsonConvert.DeserializeObject(jsonText);

        //JObject jo = JObject.Parse(retString);
        //JsonData jo = JsonMapper.ToObject(josnParms);

        string receive_str = PostRequest(pay_url, josnParms);  //暴风雪要求使用post模式json格式
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
            out_jd["results"]["payurl"] = receive_jd["pay_url"].ToString();
            SimonUtils.RespWNC(out_jd.ToJson());
        }
        else
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 暴风雪 支付 错误日志-----------------------------------------------------------------------------------");
            sb.Append("\r\n receive_str: " + receive_str);
            sb.Append("\r\n parms: " + parms);
            sb.Append("\r\n josnParms: " + josnParms); 
            sb.Append("\r\n pay_url: " + pay_url);
            sb.Append("\r\n receive_jd: " + receive_jd.ToString());
            //sb.Append("\r\n sign: " + receive_jd["data"]["sign"].ToString());
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_a_qlmjpay_error_" + DateTime.Now.ToString("yyyyMMdd"));

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
       // httpWebRequest.ContentType = "application/x-www-form-urlencoded"; 
        httpWebRequest.ContentType = "application/json; charset = UTF - 8";

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