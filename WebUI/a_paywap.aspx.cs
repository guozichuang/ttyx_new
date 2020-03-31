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

public partial class a_paywap : System.Web.UI.Page
{
    const string pay_url = "http://pay.paywap.cn/form/pay";
    const string comp_key = "56B64D99CB17154D17E8320EE363EA78";
    const string str_p1_usercode = "5010205981";
    const string str_p4_returnurl = "http://api-mobilegame-test-000.kk838.com/a_paywap_return_url.aspx";
    const string str_p5_notifyurl = "http://api-mobilegame-test-000.kk838.com/a_paywap_notify_url.aspx";

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
        rmbcost_lpar.Add(SimonDB.CreDbPar("@PayType", "152"));  //旺实富支付（http://www.paywap.cn/） 支付类型设置为152  
        rmbcost_lpar.Add(SimonDB.CreDbPar("@TypeInfo", "旺实富支付"));
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

        string str_p2_order = _ordernum;
        string str_p3_money = string.Format("{0:N2}", int.Parse(rechargermb));
        string str_p6_ordertime = DateTime.Now.ToString("yyyyMMddHHmmss");
        string str_p7_sign = SimonUtils.EnCodeMD5(str_p1_usercode + "&" + str_p2_order + "&" + str_p3_money + "&" + str_p4_returnurl + "&" + str_p5_notifyurl + "&" + str_p6_ordertime + comp_key).ToUpper();

        form1.Action = pay_url;
        p1_usercode.Value = str_p1_usercode;
        p2_order.Value = str_p2_order;
        p3_money.Value = str_p3_money;
        p4_returnurl.Value = str_p4_returnurl;
        p5_notifyurl.Value = str_p5_notifyurl;
        p6_ordertime.Value = str_p6_ordertime;
        p7_sign.Value = str_p7_sign;
        p9_paymethod.Value = "4";
        p14_customname.Value = userid;
        p17_customip.Value = payip;
        p25_terminal.Value = "3";
        p26_iswappay.Value = "3";

        ScriptManager.RegisterStartupScript(this.Page, GetType(), "post1", "Post();", true);
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
}