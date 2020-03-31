using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using System.Data;
using System.Data.Common;

using Microsoft.Security.Application;
using LitJson;
using Simon.Common;
using Aop.Api.Util;

public partial class Pay_Alipay_notify_url : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        //写日志
        StringBuilder sb = new StringBuilder();
        sb.Append("\r\n 测试日志 支付宝支付 异步通知消息 notify_url -----------------------------------------------------------------------------------");
        sb.Append("\r\n out_trade_no=" + Request.Form["out_trade_no"]);
        sb.Append("\r\n trade_status=" + Request.Form["trade_status"]);
        sb.Append("\r\n total_amount=" + Request.Form["total_amount"]);
        sb.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(sb.ToString(), "/Log/", "log_Alipay_" + DateTime.Now.ToString("yyyyMMdd"));



        IDictionary<string, string> sArray = GetRequestPost();
        bool flag = AlipaySignature.RSACheckV1(sArray, CurrSite.Alipay_public_key, "utf-8", "RSA2", false);
        if (sArray.Count != 0)
        {
            if (flag)
            {
                string out_trade_no = Request.Form["out_trade_no"]; //商户订单号
                string trade_status = Request.Form["trade_status"]; //交易状态
                string total_amount = Request.Form["total_amount"]; //订单金额
                if (trade_status == "TRADE_SUCCESS" || trade_status == "TRADE_FINISHED")
                {
                    //交易成功
                    DataTable RMBCostDT = SimonDB.DataTable(@"select * from Web_RMBCost where OrderID=@OrderID", new DbParameter[] {
                        SimonDB.CreDbPar("@OrderID", out_trade_no)
                    });
                    if (RMBCostDT.Rows.Count <= 0)
                    {
                        Response.Write("订单不存在");
                        return;
                    }
                    DataRow RMBCostDR = RMBCostDT.Rows[0];
                    if (RMBCostDR["UpdateFlag"].ToString() == "1")
                    {
                        Response.Write("订单已处理");
                        return;
                    }
                    if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(total_amount)))
                    {
                        Response.Write("充值金额不符");
                        return;
                    }
                    //判断玩家账号是否存在
                    DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", RMBCostDR["Users_ids"]) };
                    DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
                    if (UserDT.Rows.Count <= 0)
                    {
                        Response.Write("用户不存在");
                        return;
                    }
                    DataRow UserDR = UserDT.Rows[0];
                    //判断充值兑换率
                    DataTable RechargeRateDT = SimonDB.DataTable(@"select * from RechargeRate where RechargeRMB=@RechargeRMB", new DbParameter[] {
                        SimonDB.CreDbPar("@RechargeRMB", RMBCostDR["PayMoney"].ToString())
                    });
                    if (RechargeRateDT.Rows.Count <= 0)
                    {
                        Response.Write("此充值金额的金币兑换率不存在");
                        return;
                    }
                    DataRow RechargeRateDR = RechargeRateDT.Rows[0];

                    //更新订单
                    List<DbParameter> rmbcost_lpar = new List<DbParameter>();
                    rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", out_trade_no));  //订单号
                    rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", Convert.ToInt32(decimal.Parse(total_amount))));
                    rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", true));
                    rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", true));
                    rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "1"));  //更新状态
                    SimonDB.ExecuteNonQuery(@"update Web_RMBCost set InMoney=@InMoney,InSuccess=@InSuccess,
                                              PaySuccess=@PaySuccess,UpdateFlag=@UpdateFlag
                                              where OrderID=@OrderID", rmbcost_lpar.ToArray());

                    //充值动作
                    SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RechargeGold"].ToString()),
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                    });

                    //金币日志
                    SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                                                                  values (@UserID,@UserName,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                        SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                        SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RechargeGold"].ToString()),
                        SimonDB.CreDbPar("@Remark", "支付宝充值，订单号：" + out_trade_no)

                    });

                    //充值赠送金币
                    SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                    });

                    //充值赠送金币日志
                    SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                                                                  values (@UserID,@UserName,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                        SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                        SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                        SimonDB.CreDbPar("@Remark", "充值赠送金币,关联支付宝订单号：" + out_trade_no)

                    });
                }
                else
                {
                    Response.Write("交易失败");
                    return;
                }

                Response.Write("success");
            }
            else
            {
                Response.Write("fail");
            }
        }
    }

    /// 获取支付宝POST过来通知消息，并以“参数名=参数值”的形式组成数组 
    /// request回来的信息组成的数组
    public Dictionary<string, string> GetRequestPost()
    {
        int i = 0;
        Dictionary<string, string> sArray = new Dictionary<string, string>();
        NameValueCollection coll;
        //Load Form variables into NameValueCollection variable.
        coll = Request.Form;

        // Get names of all forms into a string array.
        String[] requestItem = coll.AllKeys;

        for (i = 0; i < requestItem.Length; i++)
        {
            sArray.Add(requestItem[i], Request.Form[requestItem[i]]);
        }

        return sArray;
    }

}