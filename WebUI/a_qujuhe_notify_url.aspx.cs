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

public partial class a_qujuhe_notify_url : System.Web.UI.Page
{
    const string APP_KEY = "focbu8s471tr1cmxpt0o4qzk7mc94wfo";  //api密匙

    protected void Page_Load(object sender, EventArgs e)
    {
        string memberid = Request.Params["memberid"];
        string orderid = Request.Params["orderid"];
        string amount = Request.Params["amount"];
        string transaction_id = Request.Params["transaction_id"];
        string datetime = Request.Params["datetime"];
        string returncode = Request.Params["returncode"];
        string attach = Request.Params["attach"];
        string sign = Request.Params["sign"];

        string SignTemp = "amount=" + amount
                        + "&datetime=" + datetime
                        + "&memberid=" + memberid
                        + "&orderid=" + orderid
                        + "&returncode=" + returncode
                        + "&transaction_id=" + transaction_id
                        + "&key=" + APP_KEY;
        string Md5Sign = GetMD5String(SignTemp).ToUpper();

        if (sign == Md5Sign && returncode == "00")
        {
            //交易成功
            DataTable RMBCostDT = SimonDB.DataTable(@"select * from Web_RMBCost where OrderID=@OrderID", new DbParameter[] {
                        SimonDB.CreDbPar("@OrderID", orderid)
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
            if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(amount)))
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
            rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", orderid));  //订单号
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", Convert.ToInt32(decimal.Parse(amount))));
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
                        SimonDB.CreDbPar("@Remark", "去聚合充值，订单号：" + amount)
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
                        SimonDB.CreDbPar("@Remark", "充值赠送金币,关联去聚合订单号：" + amount)
                    });

            Response.Write("OK"); //输出成功标识
        }
        else
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 去聚合 异步通知 错误日志-----------------------------------------------------------------------------------");
            sb.Append("\r\n memberid=" + memberid);
            sb.Append("\r\n orderid=" + orderid);
            sb.Append("\r\n amount=" + amount);
            sb.Append("\r\n transaction_id=" + transaction_id);
            sb.Append("\r\n datetime=" + datetime);
            sb.Append("\r\n returncode=" + returncode);
            sb.Append("\r\n attach=" + attach);
            sb.Append("\r\n sign=" + sign);
            sb.Append("\r\n Md5Sign=" + Md5Sign);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_qujuhepay_error_" + DateTime.Now.ToString("yyyyMMdd"));

            Response.Write("fail"); //输出失败标识
        }
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