using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using System.Data;
using System.Data.Common;

using LitJson;
using Simon.Common;
using Senparc.Weixin.MP;
using Senparc.Weixin.MP.TenPayLibV3;

public partial class Pay_WeixinPay_notify_url : System.Web.UI.Page
{
    private static TenPayV3Info _tenPayV3Info;
    public static TenPayV3Info TenPayV3Info
    {
        get
        {
            if (_tenPayV3Info == null)
            {
                _tenPayV3Info = TenPayV3InfoCollection.Data[System.Configuration.ConfigurationManager.AppSettings["TenPayV3_MchId"]];
            }
            return _tenPayV3Info;
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        ResponseHandler resHandler = new ResponseHandler(null);
        string return_code = resHandler.GetParameter("return_code");
        string return_msg = resHandler.GetParameter("return_msg");
        resHandler.SetKey(TenPayV3Info.Key);

        //写日志
        StringBuilder sb = new StringBuilder();
        sb.Append("\r\n 测试日志 微信支付支付 异步通知消息 notify_url -----------------------------------------------------------------------------------");
        sb.Append("\r\n out_trade_no=" + resHandler.GetParameter("out_trade_no"));
        sb.Append("\r\n return_code=" + resHandler.GetParameter("return_code"));
        sb.Append("\r\n return_msg=" + resHandler.GetParameter("return_msg"));
        sb.Append("\r\n result_code=" + resHandler.GetParameter("result_code"));
        sb.Append("\r\n total_fee=" + resHandler.GetParameter("total_fee"));
        sb.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(sb.ToString(), "/Log/", "log_Weixinpay_" + DateTime.Now.ToString("yyyyMMdd"));

        string xmlfmt = @"<xml>
                            <return_code><![CDATA[{0}]]></return_code>
                            <return_msg><![CDATA[{1}]]></return_msg>
                          </xml>";
        if (!resHandler.IsTenpaySign() || return_code != "SUCCESS")
        {
            ResponseStr(string.Format(xmlfmt, "FAIL", "FAIL"), "text/xml");
        }

        string result_code = resHandler.GetParameter("result_code");
        if (result_code == "SUCCESS")
        {
            //交易成功
            string out_trade_no = resHandler.GetParameter("out_trade_no");
            string total_fee = resHandler.GetParameter("total_fee");
            total_fee = (decimal.Parse(total_fee) / 100).ToString(); //单位分转换为单位元

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
            if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(total_fee)))
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
                        SimonDB.CreDbPar("@Remark", "微信充值，订单号：" + out_trade_no)

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
                        SimonDB.CreDbPar("@Remark", "充值赠送金币,关联微信订单号：" + out_trade_no)

                    });

            ResponseStr(string.Format(xmlfmt, "SUCCESS", "OK"), "text/xml");
        }
        else
        {
            ResponseStr(string.Format(xmlfmt, "FAIL", "FAIL"), "text/xml");
        }
    }

    private void ResponseStr(string str, string contenttype = "application/json")
    {
        Response.Cache.SetNoStore();
        Response.ContentType = contenttype;
        Response.Buffer = true;
        Response.ExpiresAbsolute = DateTime.Now.AddDays(-1);
        Response.AddHeader("pragma", "no-cache");
        Response.AddHeader("cache-control", "no-store");
        Response.CacheControl = "no-cache";
        Response.Write(str);
        Response.End();
    }
}