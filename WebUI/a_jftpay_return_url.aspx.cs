using Simon.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class a_jftpay_return_url : System.Web.UI.Page
{
    string userCode = System.Configuration.ConfigurationSettings.AppSettings["jft_yingyongnum"];
    //商户密钥(由竣付通注册后分配)
    string compKey = System.Configuration.ConfigurationSettings.AppSettings["jft_compkey"];
    public class ResponseBean
    {
        HttpRequest Request { get; set; }
        /// <summary>
        /// 应用号
        /// </summary>
        public string p1_yingyongnum { get { return Request.Params["p1_yingyongnum"]; } }
        /// <summary>
        /// 订单号
        /// </summary>
        public string p2_ordernumber { get { return Request.Params["p2_ordernumber"]; } }
        /// <summary>
        /// 订单金额
        /// </summary>
        public string p3_money { get { return Request.Params["p3_money"]; } }
        /// <summary>
        /// 支付结果
        /// </summary>
        public string p4_zfstate { get { return Request.Params["p4_zfstate"]; } }

        /// <summary>
        /// 竣付通订单号
        /// </summary>
        public string p5_orderid { get { return Request.Params["p5_orderid"]; } }
        /// <summary>
        /// 产品
        /// </summary>
        public string p6_productcode { get { return Request.Params["p6_productcode"]; } }
        /// <summary>
        /// 支付通道编码(银行,卡类编码)
        /// </summary>
        public string p7_bank_card_code { get { return Request.Params["p7_bank_card_code"]; } }
        /// <summary>
        /// 编码方式
        /// </summary>
        public string p8_charset { get { return Request.Params["p8_charset"]; } }
        /// <summary>
        /// 签名验证方式
        /// </summary>
        public string p9_signtype { get { return Request.Params["p9_signtype"]; } }
        /// <summary>
        /// 签名
        /// </summary>
        public string p10_sign { get { return Request.Params["p10_sign"]; } }
        /// <summary>
        /// 备注
        /// </summary>
        public string p11_pdesc { get { return Request.Params["p11_pdesc"]; } }
        /// <summary>
        /// 备注
        /// </summary>
        public string p13_zfmoney { get { return Request.Params["p13_zfmoney"]; } }

        public ResponseBean(HttpRequest request)
        {
            this.Request = request;
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        ResponseBean responseBean = new ResponseBean(Request);
        var sign = GetSign(responseBean).ToUpper();

        //记录日志
        StringBuilder sb = new StringBuilder();
        sb.Append("\r\n 竣付通 同步返回通知 日志-----------------------------------------------------------------------------------");
        sb.Append("\r\n p1_yingyongnum=" + responseBean.p1_yingyongnum);
        sb.Append("\r\n p2_ordernumber=" + responseBean.p2_ordernumber);
        sb.Append("\r\n p3_money=" + responseBean.p3_money);
        sb.Append("\r\n p4_zfstate=" + responseBean.p4_zfstate);
        sb.Append("\r\n p5_orderid=" + responseBean.p5_orderid);
        sb.Append("\r\n p6_productcode=" + responseBean.p6_productcode);
        sb.Append("\r\n p7_bank_card_code=" + responseBean.p7_bank_card_code);
        sb.Append("\r\n p8_charset=" + responseBean.p8_charset);
        sb.Append("\r\n p9_signtype=" + responseBean.p9_signtype);
        sb.Append("\r\n p10_sign=" + responseBean.p10_sign);
        sb.Append("\r\n p11_pdesc=" + responseBean.p11_pdesc);
        sb.Append("\r\n p13_zfmoney=" + responseBean.p13_zfmoney);
        sb.Append("\r\n MD5Sign=" + sign);
        sb.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(sb.ToString(), "/Log/", "log_jftpay_error_" + DateTime.Now.ToString("yyyyMMdd"));

        if (responseBean.p4_zfstate == "1" && sign.Equals(responseBean.p10_sign.ToLower()))
        {

            // 交易成功
            DataTable RMBCostDT = SimonDB.DataTable(@"select * from Web_RMBCost where OrderID=@OrderID", new DbParameter[] {
                        SimonDB.CreDbPar("@OrderID", responseBean.p2_ordernumber)
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
            if (Convert.ToInt32(decimal.Parse(RMBCostDR["PayMoney"].ToString())) != Convert.ToInt32(decimal.Parse(responseBean.p3_money)))
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
            //DataTable RechargeRateDT = SimonDB.DataTable(@"select * from RechargeRate where RechargeRMB=@RechargeRMB", new DbParameter[] {
            //            SimonDB.CreDbPar("@RechargeRMB", RMBCostDR["PayMoney"].ToString())
            //        });
            //if (RechargeRateDT.Rows.Count <= 0)
            //{
            //    Response.Write("此充值金额的金币兑换率不存在");
            //    return;
            //}
            //DataRow RechargeRateDR = RechargeRateDT.Rows[0];

            //更新订单
            List<DbParameter> rmbcost_lpar = new List<DbParameter>();
            rmbcost_lpar.Add(SimonDB.CreDbPar("@OrderID", responseBean.p2_ordernumber));  //订单号
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InMoney", Convert.ToInt32(decimal.Parse(responseBean.p3_money))));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@InSuccess", true));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@PaySuccess", true));
            rmbcost_lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "1"));  //更新状态
            SimonDB.ExecuteNonQuery(@"update Web_RMBCost set InMoney=@InMoney,InSuccess=@InSuccess,
                                              PaySuccess=@PaySuccess,UpdateFlag=@UpdateFlag
                                              where OrderID=@OrderID", rmbcost_lpar.ToArray());

            //充值动作
            SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
                        //SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RechargeGold"].ToString()),
                        SimonDB.CreDbPar("@ChangeMoney", RMBCostDR["PayMoney"].ToString()),
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
                    });

            //房卡日志
            SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
                                                                  values (@UserID,@UserName,@StartMoney,@ChangeMoney,11,getdate(),@Remark)", new DbParameter[] {
                        SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
                        SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
                        SimonDB.CreDbPar("@StartMoney", UserDR["RoomCard"].ToString()),
                        SimonDB.CreDbPar("@ChangeMoney", RMBCostDR["PayMoney"].ToString()),
                        SimonDB.CreDbPar("@Remark", "竣付通充值，订单号：" + responseBean.p3_money)
                    });
            //竣付通房卡日志充值类型（ChangeType）为11

            //充值赠送金币
            //SimonDB.ExecuteNonQuery(@"update TUserInfo set WalletMoney=WalletMoney+@ChangeMoney where UserID=@UserID", new DbParameter[] {
            //            SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
            //            SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString())
            //        });

            //充值赠送金币日志
            //SimonDB.ExecuteNonQuery(@"insert into Web_MoneyChangeLog (UserID,UserName,StartMoney,ChangeMoney,ChangeType,DateTime,Remark)
            //                                                      values (@UserID,@UserName,@StartMoney,@ChangeMoney,2,getdate(),@Remark)", new DbParameter[] {
            //            SimonDB.CreDbPar("@UserID", UserDR["UserID"].ToString()),
            //            SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()),
            //            SimonDB.CreDbPar("@StartMoney", UserDR["WalletMoney"].ToString()),
            //            SimonDB.CreDbPar("@ChangeMoney", RechargeRateDR["RegiveGold"].ToString()),
            //            SimonDB.CreDbPar("@Remark", "充值赠送金币,关联旺实富订单号：" + responseBean.p5_orderid)
            //        });

            //已成功支付
            Response.Write("支付成功！！！");
        }
    }

    private string GetSign(ResponseBean bean)
    {
        string rawString = bean.p1_yingyongnum + "&" + bean.p2_ordernumber + "&" + bean.p3_money + "&" + bean.p4_zfstate + "&" + bean.p5_orderid + "&" + bean.p6_productcode + "&" + bean.p7_bank_card_code + "&" + bean.p8_charset + "&" + bean.p9_signtype + "&" + bean.p11_pdesc + "&" + bean.p13_zfmoney + "&" + compKey;
        return FormsAuthentication.HashPasswordForStoringInConfigFile(rawString, "MD5");
    }
}