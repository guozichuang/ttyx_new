using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Web.Security;

using Simon.Common;
using LitJson;
using System.Net;
using System.Text;
using System.IO;

public partial class a_jftpay : System.Web.UI.Page
{
    public class RequestBean
    {
        /// <summary>
        /// 商户应用号
        /// </summary>
        public string p1_yingyongnum { get; set; }
        /// <summary>
        /// 商户订单号
        /// </summary>
        public string p2_ordernumber { get; set; }
        /// <summary>
        /// 商户订单金额
        /// </summary>
        public string p3_money { get; set; }
        /// <summary>
        /// 商户订单时间
        /// </summary>
        public string p6_ordertime { get; set; }

        /// <summary>
        /// 产品支付类型编码
        /// </summary>
        public string p7_productcode { get; set; }
        /// <summary>
        /// 订单签名
        /// </summary>
        public string p8_sign { get; set; }
        /// <summary>
        /// 签名方式
        /// </summary>
        public string p9_signtype { get; set; }
        /// <summary>
        /// 银行卡或卡类编码
        /// </summary>
        public string p10_bank_card_code { get; set; }
        /// <summary>
        /// 商户支付银行卡类型id
        /// </summary>
        public int p11_cardtype { get; set; }
        /// <summary>
        /// 商户支付银行卡类型长度
        /// </summary>
        public string p12_channel { get; set; }
        /// <summary>
        /// 订单失效时间
        /// </summary>
        public string p13_orderfailertime { get; set; }
        /// <summary>
        /// 商户游戏账号
        /// </summary>        
        public string p14_customname { get; set; }
        /// <summary>
        /// 商户联系内容
        /// </summary>
        public string p15_customcontact { get; set; }
        /// <summary>
        /// 付款ip地址
        /// </summary>
        public string p16_customip { get; set; }
        /// <summary>
        /// 商户名称
        /// </summary>
        /// <returns></returns>
        public string p17_product { get; set; }
        /// <summary>
        /// 商品种类
        /// </summary>
        /// <returns></returns>
        public string p18_productcat { get; set; }
        /// <summary>
        /// 商品数量
        /// </summary>
        /// <returns></returns>
        public string p19_productnum { get; set; }
        /// <summary>
        /// 商品描述
        /// </summary>
        /// <returns></returns>
        public string p20_pdesc { get; set; }
        /// <summary>
        /// 对接版本
        /// </summary>
        /// <returns></returns>
        public string p21_version { get; set; }
        /// <summary>
        /// SDK版本
        /// </summary>
        /// <returns></returns>
        public string p22_sdkversion { get; set; }
        /// <summary>
        /// 编码格式
        /// </summary>
        /// <returns></returns>
        public string p23_charset { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        /// <returns></returns>
        public string p24_remark { get; set; }
        /// <summary>
        /// 商户终端设备值:1 pc 2 ios  3 安卓
        /// </summary>
        /// <returns></returns>
        public string p25_terminal { get; set; }
        /// <summary>
        /// 预留参数
        /// </summary>
        /// <returns></returns>
        public string p26_ext1 { get; set; }
        /// <summary>
        /// 预留参数
        /// </summary>
        /// <returns></returns>
        public string p27_ext2 { get; set; }
        /// <summary>
        /// 预留参数
        /// </summary>
        /// <returns></returns>
        public string p28_ext3 { get; set; }
        /// <summary>
        /// 预留参数
        /// </summary>
        /// <returns></returns>
        public string p29_ext4 { get; set; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        CheckSign();
        Random rd = new Random();
        this.p1_yingyongnum.Value = System.Configuration.ConfigurationManager.AppSettings["jft_yingyongnum"];                //商户号;     
        this.p2_ordernumber.Value = DateTime.Now.ToString("yyMMddHHmmss") + rd.Next(10000000, 99999999).ToString();         //
        this.p3_money.Value = Request.Params["p3_money"];                     //?
        //this.p3_money.Value = "1";
        this.p6_ordertime.Value = DateTime.Now.ToString("yyyyMMddHHmmss");  //
        this.p7_productcode.Value = Request.Params["p7_productcode"];         //?
        //this.p7_productcode.Value = "ZFB";
        this.p8_sign.Value = "";                                            //
        this.p9_signtype.Value = "1";                                       //MD5
        this.p10_bank_card_code.Value = Request.Form["p10_bank_card_code"]; //?
        this.p11_cardtype.Value = "";
        this.p12_channel.Value = "";
        this.p13_orderfailertime.Value = "";
        this.p14_customname.Value = Request.Params["p14_customname"];         //?
        this.p15_customcontact.Value = "";
        //this.p16_customip.Value = "192_168_0_253";
        this.p16_customip.Value = Request.Params["p16_customip"];
        this.p17_product.Value = "钻石";
        this.p18_productcat.Value = "";
        this.p19_productnum.Value = "";
        this.p20_pdesc.Value = "";
        this.p21_version.Value = "";
        this.p22_sdkversion.Value = "";
        this.p23_charset.Value = "UTF-8";
        this.p24_remark.Value = "";
        this.p25_terminal.Value = Request.Params["p25_terminal"];             //?
        this.p26_ext1.Value = "1.1";
        this.p27_ext2.Value = "";
        this.p28_ext3.Value = "";
        this.p29_ext4.Value = "";
        this.Card_Number.Value = Request.Form["Card_Number"];
        this.Card_Password.Value = Request.Form["Card_Password"];

        RequestBean requestBean = new RequestBean()
        {
            p1_yingyongnum = this.p1_yingyongnum.Value,
            p2_ordernumber = this.p2_ordernumber.Value,
            p3_money = this.p3_money.Value,
            p6_ordertime = this.p6_ordertime.Value,
            p7_productcode = this.p7_productcode.Value,
            p8_sign = ""
        };

        this.p8_sign.Value = GetSign(requestBean);

        //平台创建订单
        string player_id = p14_customname.Value;
        if (string.IsNullOrWhiteSpace(player_id))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家账号错误"));
        }
        if (string.IsNullOrWhiteSpace(p25_terminal.Value))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "设备信息错误"));
        }
        if (string.IsNullOrWhiteSpace(p16_customip.Value))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "玩家IP错误"));
        }
        if (string.IsNullOrWhiteSpace(p7_productcode.Value))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "支付方式错误"));
        }
        if (string.IsNullOrWhiteSpace(p3_money.Value) || !SimonUtils.IsDecimal(p3_money.Value))
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "充值金额错误"));
        }

        //判断玩家账号是否存在
        DbParameter[] userparms = new DbParameter[] { SimonDB.CreDbPar("@userid", player_id) };
        DataTable UserDT = SimonDB.DataTable(@"select * from TUsers as a inner join TUserInfo as b on a.userid=b.userid where a.userid=@userid", userparms);
        if (UserDT.Rows.Count <= 0)
        {
            SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "用户不存在"));
        }
        DataRow UserDR = UserDT.Rows[0];

        //提交form表单到requestUrl
        //form1.Action = System.Configuration.ConfigurationManager.AppSettings["requestUrl"];
        ScriptManager.RegisterStartupScript(this.Page, GetType(), "post1", "Post();", true);
        
        //创建订单
        List<DbParameter> lpar = new List<DbParameter>();
        lpar.Add(SimonDB.CreDbPar("@Users_ids", UserDR["UserID"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@TrueName", UserDR["NickName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UserName", UserDR["UserName"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@PayMoney", p3_money.Value));
        lpar.Add(SimonDB.CreDbPar("@PayType", "78"));  //竣付通 的支付类型设置为78
        lpar.Add(SimonDB.CreDbPar("@TypeInfo", "Jft_" + p7_productcode.Value));
        lpar.Add(SimonDB.CreDbPar("@OrderID", p2_ordernumber.Value));  //订单号
        lpar.Add(SimonDB.CreDbPar("@AddTime", DateTime.Now.ToString()));
        lpar.Add(SimonDB.CreDbPar("@ExchangeRate", "1"));  //充值兑换率(此字段暂时无效)
        lpar.Add(SimonDB.CreDbPar("@InMoney", "0"));  //提交订单时写入0,确定充值成功后需更新该字段
        lpar.Add(SimonDB.CreDbPar("@InSuccess", false));
        lpar.Add(SimonDB.CreDbPar("@PaySuccess", false));
        lpar.Add(SimonDB.CreDbPar("@MoneyFront", UserDR["RoomCard"].ToString()));
        lpar.Add(SimonDB.CreDbPar("@UpdateFlag", "0"));  //更新状态
        lpar.Add(SimonDB.CreDbPar("@PurchaseType", "3")); //充值金币1 充值元宝2 充值房卡3
        lpar.Add(SimonDB.CreDbPar("@PayIP", p16_customip.Value));
        lpar.Add(SimonDB.CreDbPar("@ao_device", p25_terminal.Value));
        SimonDB.ExecuteNonQuery(@"insert into Web_RMBCost  (Users_ids,TrueName,UserName,PayMoney,PayType,TypeInfo,OrderID,AddTime,
                                                            ExchangeRate,InMoney,InSuccess,PaySuccess,MoneyFront,UpdateFlag,PurchaseType,
                                                            PayIP,ao_device,ao_device_id)
                                                    values (@Users_ids,@TrueName,@UserName,@PayMoney,@PayType,@TypeInfo,@OrderID,@AddTime,
                                                            @ExchangeRate,@InMoney,@InSuccess,@PaySuccess,@MoneyFront,@UpdateFlag,@PurchaseType,
                                                            @PayIP,@ao_device,'')", lpar.ToArray());
    }

    private string GetSign(RequestBean bean)
    {
        string rawString = bean.p1_yingyongnum + "&" + bean.p2_ordernumber + "&" + bean.p3_money + "&" + bean.p6_ordertime +
                           "&" + bean.p7_productcode + "&" + System.Configuration.ConfigurationManager.AppSettings["jft_compkey"];

        return FormsAuthentication.HashPasswordForStoringInConfigFile(rawString, "MD5");
    }

    public string GetIP()
    {
        string result = String.Empty;

        result = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
        if (string.IsNullOrEmpty(result))
        {
            result = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        }
        if (string.IsNullOrEmpty(result))
        {
            result = HttpContext.Current.Request.UserHostAddress;
        }
        if (string.IsNullOrEmpty(result))
        {
            return "127.0.0.1";
        }
        return result;
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