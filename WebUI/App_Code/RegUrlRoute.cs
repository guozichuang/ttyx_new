using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;

using Simon.Common;

/// <summary>
/// UrlRoute 的摘要说明
/// </summary>
public class RegUrlRoute
{
	public RegUrlRoute()
	{
	}

    public static void RegisterRoutes(RouteCollection routes)
    {
        routes.Ignore("{resource}.axd/{*pathInfo}");  //路由忽略

        //前端api路由规则
        routes.MapPageRoute("api", "api/{act}", "~/api/api.aspx", false, new RouteValueDictionary { { "act", string.Empty } });

        //推荐人管理端api路由规则
        routes.MapPageRoute("api_recuser", "api/recuser/{act}", "~/api/recuser.aspx", false, new RouteValueDictionary { { "act", string.Empty } });

        //管理端api路由规则
        routes.MapPageRoute("api_admin", "api/admin/{act}", "~/api/admin.aspx", false, new RouteValueDictionary { { "act", string.Empty } });


        #region 演示参数说明

        routes.MapPageRoute("blogs", //给这个UrlRouting规则起一个名字
            "archive/{year}/{month}/{date}/default.aspx", //希望的友好Url地址格式
            "~/blogs.aspx", //映射到的aspx页面路径
            false, //是否需要检查用户权限
            new RouteValueDictionary{ 
                {"year", DateTime.Now.Year},
                {"month", DateTime.Now.Month},
                {"date", DateTime.Now.Date}
            },//参数的默认值
            new RouteValueDictionary { 
                {"year",@"(19|20)\d{2}"},
                {"month",@"\d{1,2}"},
                {"date",@"\d{1,2}"}
            } //参数的规则，我们在这里限制url中的年月日是我们想要的数据格式
        ); 

        #endregion

    }
}