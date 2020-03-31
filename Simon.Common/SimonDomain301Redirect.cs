using System;
using System.Web;
using System.Configuration;

namespace Simon.Common
{
    public class SimonDomain301Redirect : IHttpModule
    {
        public void Dispose()
        {

        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(Domain301Redirect);
        }

        public void Domain301Redirect(Object source, EventArgs e)
        {
            HttpApplication app = (HttpApplication)source;
            HttpRequest request = app.Context.Request;
            string lRequestedPath = request.Url.DnsSafeHost.ToString();
            string strWebURL = ConfigurationManager.AppSettings["Domain301Redirect"];
            if (!string.IsNullOrEmpty(strWebURL))
            {
                if (lRequestedPath.IndexOf(strWebURL) < 0)
                {
                    app.Response.StatusCode = 301;
                    app.Response.Status = "301 Moved Permanently";

                    if (request.RawUrl.IndexOf("default.aspx", StringComparison.OrdinalIgnoreCase) >= 0
                        || request.RawUrl.IndexOf("index.aspx", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        //首页
                        app.Response.AddHeader("Location", lRequestedPath.Replace(lRequestedPath, "http://" + strWebURL));  //这里面的域名根据自己的实际情况修改
                    }
                    else
                    {
                        //其他页
                        app.Response.AddHeader("Location", lRequestedPath.Replace(lRequestedPath, "http://" + strWebURL + request.RawUrl.Trim()));  //这里面的域名根据自己的实际情况修改
                    }

                    app.Response.End();
                }
            }
        }
    }
}
