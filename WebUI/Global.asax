<%@ Application Language="C#" %>

<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        //在应用程序启动时运行的代码
        RegUrlRoute.RegisterRoutes(RouteTable.Routes);
        //去除禁用控件后的默认样式附加样式 "aspNetDisabled"
        WebControl.DisabledCssClass = string.Empty;

        RegisterWeixinThreads();  //微信公众号-激活微信缓存（必须）
        RegisterSenparcWeixin();  //微信公众号-注册微信公众号的账号信息
        RegisterWeixinPay();  //微信公众号-注册微信支付
    }

    void Application_End(object sender, EventArgs e)
    {
        //在应用程序关闭时运行的代码

    }

    void Application_Error(object sender, EventArgs e)
    {
        //在出现未处理的错误时运行的代码

        //写入错误日志
        Exception ex = Server.GetLastError().GetBaseException();
        StringBuilder str = new StringBuilder();
        str.Append("\r\n");
        str.Append("\r\n 客户端信息：");
        str.Append("\r\n\t IP:" + SimonUtils.GetUserIp());
        str.Append("\r\n\t 浏览器:" + Request.Browser.Browser.ToString());
        str.Append("\r\n\t 浏览器版本:" + Request.Browser.MajorVersion.ToString());
        str.Append("\r\n\t 操作系统:" + Request.Browser.Platform.ToString());
        str.Append("\r\n 错误信息：");
        str.Append("\r\n\t 页面：" + Request.Url.ToString());
        str.Append("\r\n\t 错误信息：" + ex.Message);
        str.Append("\r\n\t 错误源：" + ex.Source);
        str.Append("\r\n\t 异常方法：" + ex.TargetSite);
        str.Append("\r\n\t 堆栈信息：" + ex.StackTrace);
        str.Append("\r\n");
        str.Append("\r\n--------------------------------------------------------------------------------------------------");
        SimonLog.WriteLog(str.ToString(), "/log/", "errlog_" + DateTime.Now.ToString("yyyyMMdd"));

        //输出错误
        HttpException httpex = Server.GetLastError() as HttpException;
        //if (httpex.GetHttpCode() == 404)
        //{
        //    SimonUtils.RespWNC("404");
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "err"));

        //}
        //if (httpex.GetHttpCode() == 500)
        //{
        //    SimonUtils.RespWNC("500");
        //    SimonUtils.RespWNC(CurrSite.GetErrJson("-1", "err"));
        //}
    }

    void Application_BeginRequest(object sender, EventArgs e)
    {
        //在接收到一个应用程序请求时被触发。对于一个请求来说，它是第一个被触发的事件。请求一般是用户输入的一个页面url。

    }

    void Session_Start(object sender, EventArgs e)
    {
        //在新会话启动时运行的代码

    }

    void Session_End(object sender, EventArgs e)
    {
        //在会话结束时运行的代码。 
        // 注意: 只有在 Web.config 文件中的 sessionstate 模式设置为
        // InProc 时，才会引发 Session_End 事件。如果会话模式 
        //设置为 StateServer 或 SQLServer，则不会引发该事件。

    }



    /// <summary>
    /// 激活微信缓存
    /// </summary>
    private void RegisterWeixinThreads()
    {
        Senparc.Weixin.Threads.ThreadUtility.Register();
    }
    /// <summary>
    /// 注册微信公众号的账号信息
    /// </summary>
    private void RegisterSenparcWeixin()
    {
        Senparc.Weixin.MP.Containers.AccessTokenContainer.Register(
            System.Configuration.ConfigurationManager.AppSettings["TenPayV3_AppId"],
            System.Configuration.ConfigurationManager.AppSettings["TenPayV3_AppSecret"],
            "公众号");
    }
    /// <summary>
    /// 注册微信支付
    /// </summary>
    private void RegisterWeixinPay()
    {
        //提供微信支付信息
        var tenPayV3_MchId = System.Configuration.ConfigurationManager.AppSettings["TenPayV3_MchId"];
        var tenPayV3_Key = System.Configuration.ConfigurationManager.AppSettings["TenPayV3_Key"];
        var tenPayV3_AppId = System.Configuration.ConfigurationManager.AppSettings["TenPayV3_AppId"];
        var tenPayV3_AppSecret = System.Configuration.ConfigurationManager.AppSettings["TenPayV3_AppSecret"];
        var tenPayV3_TenpayNotify = System.Configuration.ConfigurationManager.AppSettings["TenPayV3_TenpayNotify"];
        var tenPayV3Info = new Senparc.Weixin.MP.TenPayLibV3.TenPayV3Info(tenPayV3_AppId, tenPayV3_AppSecret, tenPayV3_MchId, tenPayV3_Key, tenPayV3_TenpayNotify);
        Senparc.Weixin.MP.TenPayLibV3.TenPayV3InfoCollection.Register(tenPayV3Info);
    }

</script>
