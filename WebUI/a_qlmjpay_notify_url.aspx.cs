﻿using System;
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

public partial class a_qlmjpay_notify_url : System.Web.UI.Page
{
    //const string s_key = "FFF3C76820904D10916ECBB568344468";
    const string s_key = "57D492C965574527A769E1C61E99F91D";
    protected void Page_Load(object sender, EventArgs e)
    {
        Stream receive_str = Request.InputStream;

        StreamReader sr = new StreamReader(receive_str);

        string requestMes = sr.ReadToEnd();

        JsonData receive_jd = null;
        try
        {
            receive_jd = JsonMapper.ToObject(requestMes);
        }
        catch { SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "Json异常")); }

        string merchant_id = receive_jd["merchant_id"].ToString();
        string request_time = receive_jd["request_time"].ToString();
        string pay_time = receive_jd["pay_time"].ToString();
        string status = receive_jd["status"].ToString();
        string order_amount = receive_jd["order_amount"].ToString();
        string pay_amount = receive_jd["pay_amount"].ToString();
        string out_trade_no = receive_jd["out_trade_no"].ToString();
        //if (out_trade_no == null) out_trade_no = "";
        string trade_no = receive_jd["trade_no"].ToString();
        string fees = receive_jd["fees"].ToString();
        string pay_type = receive_jd["pay_type"].ToString();
        string nonce_str = receive_jd["nonce_str"].ToString();
        string sign = receive_jd["sign"].ToString();

        string MD5Sign = SimonUtils.EnCodeMD5("fees="+ fees + "&" + "merchant_id=" + merchant_id + "&" + "nonce_str=" + nonce_str + "&"+ "order_amount="+ order_amount + "&" + "out_trade_no=" + out_trade_no + "&" + "pay_amount="+ pay_amount+"&" + "pay_time="+ pay_time + "&" + "pay_type=" + pay_type + "&" + "request_time=" + request_time + "&"  + "status="+ status+"&" + "trade_no="+ trade_no + s_key).ToUpper();

        //记录日志
        StringBuilder sb = new StringBuilder();
        sb.Append("\r\n 暴风雪 异步通知 回调日志-----------------------------------------------------------------------------------");
        sb.Append("\r\n requestMes=" + requestMes);
        sb.Append("\r\n merchant_id=" + merchant_id);
        sb.Append("\r\n request_time=" + request_time);
        sb.Append("\r\n pay_time=" + pay_time);
        sb.Append("\r\n status=" + status);
        sb.Append("\r\n order_amount=" + order_amount);
        sb.Append("\r\n pay_amount=" + pay_amount);
        sb.Append("\r\n out_trade_no=" + out_trade_no);
        sb.Append("\r\n trade_no=" + trade_no);
        sb.Append("\r\n fees=" + fees);
        sb.Append("\r\n pay_type=" + pay_type);
        sb.Append("\r\n nonce_str=" + nonce_str);
        sb.Append("\r\n sign=" + sign);
        sb.Append("\r\n MD5Sign=" + MD5Sign);
        sb.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(sb.ToString(), "/Log/", "log_paywap_error_" + DateTime.Now.ToString("yyyyMMdd"));

        if (MD5Sign.Equals(sign, StringComparison.OrdinalIgnoreCase) && status == "success")
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
            if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(order_amount)))
            {
                Response.Write("充值金额不符");
                return;
            }
            //获取充值货币类型
            string PurchaseType = RMBCostDT.Rows[0]["PurchaseType"].ToString();
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
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", Convert.ToInt32(decimal.Parse(order_amount))));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", true));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", true));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "1"));  //更新状态
            SimonDB.ExecuteNonQuery(@"update Web_RMBCost set InMoney=@InMoney,InSuccess=@InSuccess,
                                              PaySuccess=@PaySuccess,UpdateFlag=@UpdateFlag
                                              where OrderID=@OrderID", rmbcost_lpar.ToArray());
            if (PurchaseType=="1")
            {
                //充值金币动作
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
                        SimonDB.CreDbPar("@Remark", "暴风雪充值，订单号：" + trade_no)
                    });

                //充值赠送金币
                //SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                //        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                //        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                //    });

                //充值赠送金币日志
                //SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                //                                                  values (@UserID,@UserName,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
                //        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                //        SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                //        SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
                //        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                //        SimonDB.CreDbPar("@Remark", "赠送金币,关联暴风雪订单号：" + trade_no)
                //    });
            }
            if (PurchaseType=="2")
            {
                //充值房卡动作
                SimonDB.ExecuteNonQuery(@"update TUserInfo set RoomCard=RoomCard+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                    });
                string RecordNum = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                //房卡日志
                SimonDB.ExecuteNonQuery(@"insert into FangkaRecord (UserID,RecordNum,CurrencyType,StartCount,Count,DeductType,DeductTime,Remark)
                                                                  values (@UserID,@RecordNum,1,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                        SimonDB.CreDbPar("@RecordNum", RecordNum),
                        SimonDB.CreDbPar("@StartMoney", UserDR["RoomCard"].ToString()),
                        SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
                        SimonDB.CreDbPar("@Remark", "暴风雪充值房卡，订单号：" + trade_no)
                    });
                
            }
           

            SimonUtils.RespW("success");
        }
    }
}