using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Simon.Common
{
    /// <summary>
    /// Session控制类
    /// </summary>
    public class SimonSession
    {
        /// <summary>
        /// 设置Sesion项
        /// </summary>
        /// <param name="SessionName">Sesion名称</param>
        /// <param name="SessionValue">Sesion值</param>
        public static void Save(string SessionName, object SessionValue)
        {
            HttpContext.Current.Session[SessionName] = SessionValue;
            //HttpContext.Current.Session.Timeout = 30;
        }

        /// <summary>
        /// 设置Sesion项
        /// </summary>
        /// <param name="SessionName">Sesion名称</param>
        /// <param name="SessionValue">Sesion值</param>
        /// <param name="SesionExpires">Sesion超时时间(分钟)</param>
        public static void Save(string SessionName, object SessionValue, int SesionExpires)
        {
            HttpContext.Current.Session[SessionName] = SessionValue;
            HttpContext.Current.Session.Timeout = SesionExpires;
        }

        /// <summary>
        /// 读取Session项(不存在则返回null)
        /// </summary>
        /// <param name="SessionName">Session名称</param>
        /// <returns>Session值</returns>
        public static object Get(string SessionName)
        {
            if (HttpContext.Current.Session[SessionName] != null)
                return HttpContext.Current.Session[SessionName];
            return null;
        }

        /// <summary>
        /// 清除Session项(设置Session项为 null)
        /// </summary>
        /// <param name="SessionName">SessionName</param>
        public static void Del(string SessionName)
        {
            HttpContext.Current.Session[SessionName] = null;
        }

        /// <summary>
        /// 取消Session会话状态
        /// </summary>
        public static void Abandon()
        {
            HttpContext.Current.Session.Abandon();
        }

    }
}
