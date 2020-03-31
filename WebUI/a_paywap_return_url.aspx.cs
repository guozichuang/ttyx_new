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

public partial class a_paywap_return_url : System.Web.UI.Page
{
    const string comp_key = "56B64D99CB17154D17E8320EE363EA78";

    protected void Page_Load(object sender, EventArgs e)
    {
        string p1_usercode = Request.Params["p1_usercode"];
        string p2_order = Request.Params["p2_order"];
        string p3_money = Request.Params["p3_money"];
        string p4_status = Request.Params["p4_status"];
        string p5_payorder = Request.Params["p5_payorder"];
        string p6_paymethod = Request.Params["p6_paymethod"];
        string p7_paychannelnum = Request.Params["p7_paychannelnum"];
        if (p7_paychannelnum == null) p7_paychannelnum = "";
        string p8_charset = Request.Params["p8_charset"];
        string p9_signtype = Request.Params["p9_signtype"];
        string p10_sign = Request.Params["p10_sign"];
        string p11_remark = Request.Params["p11_remark"];

        string MD5Sign = SimonUtils.EnCodeMD5(p1_usercode + "&" + p2_order + "&" + p3_money + "&" + p4_status + "&" + p5_payorder + "&" + p6_paymethod + "&" + p7_paychannelnum + "&" + p8_charset + "&" + p9_signtype + "&" + comp_key);

        //记录日志
        StringBuilder sb = new StringBuilder();
        sb.Append("\r\n 旺实付 异步通知 日志-----------------------------------------------------------------------------------");
        sb.Append("\r\n p1_usercode=" + p1_usercode);
        sb.Append("\r\n p2_order=" + p2_order);
        sb.Append("\r\n p3_money=" + p3_money);
        sb.Append("\r\n p4_status=" + p4_status);
        sb.Append("\r\n p5_payorder=" + p5_payorder);
        sb.Append("\r\n p6_paymethod=" + p6_paymethod);
        sb.Append("\r\n p7_paychannelnum=" + p7_paychannelnum);
        sb.Append("\r\n p8_charset=" + p8_charset);
        sb.Append("\r\n p9_signtype=" + p9_signtype);
        sb.Append("\r\n p10_sign=" + p10_sign);
        sb.Append("\r\n p11_remark=" + p11_remark);
        sb.Append("\r\n MD5Sign=" + MD5Sign);
        sb.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(sb.ToString(), "/Log/", "log_paywap_error_" + DateTime.Now.ToString("yyyyMMdd"));

        if (MD5Sign.Equals(p10_sign, StringComparison.OrdinalIgnoreCase) && p4_status == "1")
        {
            //交易成功
            DataTable RMBCostDT = SimonDB.DataTable(@"select * from Web_RMBCost where OrderID=@OrderID", new DbParameter[] {
                        SimonDB.CreDbPar("@OrderID", p2_order)
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
            if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(p3_money)))
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
            rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", p2_order));  //订单号
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", Convert.ToInt32(decimal.Parse(p3_money))));
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
                        SimonDB.CreDbPar("@Remark", "旺实富充值，订单号：" + p3_money)
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
                        SimonDB.CreDbPar("@Remark", "充值赠送金币,关联旺实富订单号：" + p5_payorder)
                    });

            SimonUtils.RespW("支付成功");
        }
    }
}