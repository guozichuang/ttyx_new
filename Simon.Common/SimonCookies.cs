using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace Simon.Common
{
    /// <summary>
    /// Cookie控制类
    /// </summary>
    public class SimonCookies
    {

        /// <summary>
        /// 保存一个Cookie,不设置过期时间和作用域
        /// </summary>
        /// <param name="CookieName">Cookie名称</param>
        /// <param name="CookieValue">Cookie值</param>
        public static void Save(string CookieName, string CookieValue)
        {
            Save(CookieName, CookieValue, 0, null);
        }

        /// <summary>
        /// 保存一个Cookie,不设置作用域
        /// </summary>
        /// <param name="CookieName">Cookie名称</param>
        /// <param name="CookieValue">Cookie值</param>
        /// <param name="CookieExpMin">Cookie过期时间:以分钟为单位(0表示不设过期时间，赋值为设置为过期失效)</param>
        public static void Save(string CookieName, string CookieValue, int CookieExpMin)
        {
            Save(CookieName, CookieValue, CookieExpMin, null);
        }

        /// <summary>
        /// 保存一个Cookie
        /// </summary>
        /// <param name="CookieName">Cookie名称</param>
        /// <param name="CookieValue">Cookie值</param>
        /// <param name="CookieExpMin">Cookie过期时间:以分钟为单位(0表示不设过期时间，赋值为设置为过期失效)</param>
        /// <param name="CookieDomain">Cookie作用域,域名</param>
        public static void Save(string CookieName, string CookieValue, int CookieExpMin, string CookieDomain)
        {
            HttpCookie objCookie = new HttpCookie(CookieName.Trim());
            objCookie.Value = HttpContext.Current.Server.UrlEncode(CookieValue.Trim());

            if (CookieDomain != null) objCookie.Domain = CookieDomain;
            if (CookieExpMin != 0) objCookie.Expires = DateTime.Now.AddMinutes(CookieExpMin);

            Clear(CookieName.Trim());//更新cookie,先清除再保存
            HttpContext.Current.Response.Cookies.Add(objCookie);
        }

        /// <summary>
        /// 取得CookieValue
        /// </summary>
        /// <param name="CookieName">Cookie名称</param>
        /// <returns>Cookie的值</returns>
        public static string Get(string CookieName)
        {
            HttpCookie objCookie = HttpContext.Current.Request.Cookies[CookieName.Trim()];
            if (objCookie != null)
                return HttpContext.Current.Server.UrlDecode(objCookie.Value);
            return null;
        }

        /// <summary>
        /// 清除CookieValue
        /// </summary>
        /// <param name="CookieName">Cookie名称</param>
        public static void Clear(string CookieName)
        {
            HttpCookie objCookie = new HttpCookie(CookieName.Trim());
            objCookie.Expires = DateTime.Now.AddYears(-3);
            HttpContext.Current.Response.Cookies.Add(objCookie);
        }

    }
}


