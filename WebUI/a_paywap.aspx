<%@ Page Language="C#" AutoEventWireup="true" CodeFile="a_paywap.aspx.cs" Inherits="a_paywap" %>
<!DOCTYPE html>
<html>
<head runat="server">
    <title></title>
    <script type="text/javascript">
        function Post() {
            document.getElementById("form1").submit();
        }
    </script>
</head>
<body>
    <form id="form1" runat="server" method="post" action="http://pay.paywap.cn/form/pay">
        <asp:HiddenField ID="p1_usercode" runat="server" />
        <!--商户号-->
        <asp:HiddenField ID="p2_order" runat="server" />
        <!--订单号-->
        <asp:HiddenField ID="p3_money" runat="server" />
        <!--金额-->
        <asp:HiddenField ID="p4_returnurl" runat="server" />
        <!--回掉界面-->
        <asp:HiddenField ID="p5_notifyurl" runat="server" />
        <!--通知界面-->
        <asp:HiddenField ID="p6_ordertime" runat="server" />
        <!--订单时间-->
        <asp:HiddenField ID="p7_sign" runat="server" />
        <!--商户传递参数加密值-->
        <asp:HiddenField ID="p9_paymethod" runat="server" />
        <!--支付通道编码-->
        <asp:HiddenField ID="p14_customname" runat="server" />
        <!--客户名称-->
        <asp:HiddenField ID="p17_customip" runat="server" />
        <!--客户端IP-->
        <asp:HiddenField ID="p25_terminal" runat="server" />
        <!--终端设备-->
        <asp:HiddenField ID="p26_iswappay" runat="server" />
        <!--支付场景-->
    </form>
</body>
</html>