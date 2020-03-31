using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using System.Configuration;
using System.Data;
using System.Data.Common;

using Simon.Common;

public partial class Pay_Jft_nodify_url : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string p4_zfstate = Request.Params["p4_zfstate"];  //支付返回结果 1 代表成功，其他为失败。
        string p1_yingyongnum = Request.Params["p1_yingyongnum"];
        string p2_ordernumber = Request.Params["p2_ordernumber"];
        string p3_money = Request.Params["p3_money"];
        string p5_orderid = Request.Params["p5_orderid"];
        string p6_productcode = Request.Params["p6_productcode"];
        string p8_charset = Request.Params["p8_charset"];
        string p9_signtype = Request.Params["p9_signtype"];
        string p10_sign = Request.Params["p10_sign"];
        string p11_pdesc = Request.Params["p11_pdesc"];
        string p12_remark = Request.Params["p12_remark"];
        string p13_zfmoney = Request.Params["p13_zfmoney"];

        if (string.IsNullOrWhiteSpace(p4_zfstate))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p1_yingyongnum))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p2_ordernumber))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p3_money))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p5_orderid))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p6_productcode))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p8_charset))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p9_signtype))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p10_sign))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p11_pdesc))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p12_remark))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(p13_zfmoney))
        {
            SimonUtils.RespWNC("fail");
        }
        
        string verifysign = "p1_yingyongnum=" + CurrSite.jft_yingyongnum
                          + "&p2_ordernumber=" + p2_ordernumber
                          + "&p3_money=" + p3_money
                          + "&p4_zfstate=" + p4_zfstate
                          + "&p5_orderid=" + p5_orderid
                          + "&p6_productcode=" + p6_productcode
                          + "&p7_bank_card_code="
                          + "&p8_charset=" + p8_charset
                          + "&p9_signtype=" + p9_signtype
                          + "&p11_pdesc=" + p11_pdesc
                          + "&p13_zfmoney=" + p13_zfmoney+"&"
                          + CurrSite.jft_compkey;
        verifysign = SimonUtils.EnCodeMD5(verifysign);

        if (p4_zfstate == "1" && p10_sign == verifysign)
        {
            //WebPayBll biz = new WebPayBll();
            //var IsHaveUpdate = biz.GetPaySuccess(sp_billno);
            //if (IsHaveUpdate)
            //{
            //    已支付成功，不更新订单
            //    SimonUtils.RespWNC("OK");
            //}
            //else
            //{
            //    var total = Convert.ToDecimal(total_fee);
            //    var upResult = biz.UpdatePayOrder(Convert.ToInt32(total), sp_billno);
            //    SimonUtils.RespWNC("OK");
            //}
        }
        else
        {
            //写错误日志
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n 错误日志 回调动作-----------------------------------------------------------------------------------");
            sb.Append("\r\n p4_zfstate=" + p4_zfstate);
            sb.Append("\r\n p1_yingyongnum=" + p1_yingyongnum);
            sb.Append("\r\n p2_ordernumber=" + p2_ordernumber);
            sb.Append("\r\n p3_money=" + p3_money);
            sb.Append("\r\n p5_orderid=" + p5_orderid);
            sb.Append("\r\n p6_productcode=" + p6_productcode);
            sb.Append("\r\n p8_charset=" + p8_charset);
            sb.Append("\r\n p9_signtype=" + p9_signtype);
            sb.Append("\r\n p10_sign=" + p10_sign);
            sb.Append("\r\n p11_pdesc=" + p11_pdesc);
            sb.Append("\r\n p12_remark=" + p12_remark);
            sb.Append("\r\n p13_zfmoney=" + p13_zfmoney);
            sb.Append("\r\n verifysign=" + verifysign);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_jft_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
            return;
        }
    }
}