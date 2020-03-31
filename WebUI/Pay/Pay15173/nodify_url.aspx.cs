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

public partial class Pay_Pay15173_nodify_url : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string pay_result = Request.Params["pay_result"];
        string transaction_id = Request.Params["transaction_id"];
        string bargainor_id = Request.Params["bargainor_id"];
        string sp_billno = Request.Params["sp_billno"];
        string pay_info = Request.Params["pay_info"];
        string total_fee = Request.Params["total_fee"];
        string attach = Request.Params["attach"];
        string zidy_code = Request.Params["zidy_code"];
        string sign = Request.Params["sign"];

        if (string.IsNullOrWhiteSpace(pay_result))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(transaction_id))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(bargainor_id))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(sp_billno))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(pay_info))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(total_fee))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(attach))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(zidy_code))
        {
            SimonUtils.RespWNC("fail");
        }
        if (string.IsNullOrWhiteSpace(sign))
        {
            SimonUtils.RespWNC("fail");
        }

        string verifysign = "pay_result=" + pay_result
                          + "&bargainor_id=" + CurrSite.Pay15173_bargainor_id
                          + "&sp_billno=" + sp_billno
                          + "&total_fee=" + total_fee
                          + "&attach=" + attach
                          + "&key=" + CurrSite.Pay15173_key;
        verifysign = SimonUtils.EnCodeMD5(verifysign);

        if (pay_result == "0" && sign == verifysign)
        {
            //WebPayBll biz = new WebPayBll();
            //var IsHaveUpdate = biz.GetPaySuccess(sp_billno);
            //if (IsHaveUpdate)
            //{
            //    //已支付成功，不更新订单
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
            sb.Append("\r\n pay_result=" + pay_result);
            sb.Append("\r\n transaction_id=" + transaction_id);
            sb.Append("\r\n bargainor_id=" + bargainor_id);
            sb.Append("\r\n sp_billno=" + sp_billno);
            sb.Append("\r\n pay_info=" + pay_info);
            sb.Append("\r\n total_fee=" + total_fee);
            sb.Append("\r\n attach=" + attach);
            sb.Append("\r\n zidy_code=" + zidy_code);
            sb.Append("\r\n sign=" + sign);
            sb.Append("\r\n verifysign=" + verifysign);
            sb.Append("\r\n--------------------------------------------------------------------------------------------------");
            SimonLog.WriteLog(sb.ToString(), "/Log/", "log_15173_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
            return;
        }
    }
}