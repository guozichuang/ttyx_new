using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;

namespace Simon.Common
{
    /// <summary>
    /// Simon 简单分页
    /// </summary>
    public class SimonPager
    {
        /// <summary>
        /// 计算页数
        /// </summary>
        /// <param name="RecordCount">总记录数</param>
        /// <param name="PageSize">分页大小</param>
        /// <returns>页数</returns>
        public static int GetTotalPage(int RecordCount, int PageSize)
        {
            return RecordCount % PageSize == 0 ? RecordCount / PageSize : RecordCount / PageSize + 1;
        }

        /// <summary>
        /// 写出分页
        /// </summary>
        /// <param name="TotalPage">总页数</param>
        /// <param name="PageIndex">当前页码</param>
        /// <param name="StepNumBtnCount">当前页两侧数字页码数量</param>
        /// <param name="PageCssClass">分页CssClass</param>
        /// <returns>分页代码</returns>
        public static string GetPagerHtml(int TotalPage, int PageIndex, int StepNumBtnCount, string PageCssClass)
        {
            string _BaseUrl = "", _ParmsStr = ""; NameValueCollection _NVC = null;

            SimonUrl.ParseUrl(HttpContext.Current.Request.RawUrl, out _BaseUrl, out _NVC);
            foreach (string _key in _NVC.Keys)
            {
                if (_key.ToString() != "page")
                    _ParmsStr += "&" + _key.ToString() + "=" + _NVC[_key].ToString();
            }
            string _LinkFormat = "?page={0}" + _ParmsStr; //分页url格式

            int _PageStart = 1;
            TotalPage = TotalPage == 0 ? 1 : TotalPage;
            PageIndex = PageIndex == 0 ? 1 : PageIndex;

            StringBuilder _PagerHtml = new StringBuilder();
            _PagerHtml.Append("<div class=\"" + PageCssClass + "\"><ul>");
            _PagerHtml.Append("<li class=\"info\">第" + PageIndex.ToString() + "页,共" + TotalPage.ToString() + "页</li>");

            if (PageIndex - StepNumBtnCount < 2)
                _PageStart = 1;
            else _PageStart = PageIndex - StepNumBtnCount;

            int _PageEnd = TotalPage;
            if (PageIndex + StepNumBtnCount >= TotalPage)
                _PageEnd = TotalPage;
            else _PageEnd = PageIndex + StepNumBtnCount;

            if (_PageStart == 1)
            {
                if (PageIndex > 1)
                {
                    _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, 1) + "\" title=\"首页\">首页</a></li>");
                    _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, (PageIndex - 1)) + "\" title=\"上一页\">上一页</a></li>");
                }
            }
            else
            {
                _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, 1) + "\" title=\"首页\">首页</a></li>");
                _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, (PageIndex - 1)) + "\" title=\"上一页\">上一页</a></li>");
            }
            for (int i = _PageStart; i <= _PageEnd; i++)
            {
                if (i == PageIndex)
                    _PagerHtml.Append("<li><a href=\"javascript:;\" title=\"第" + i.ToString() + "页\">" + i.ToString() + "</a></li>");
                else _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, i) + "\" title=\"第" + i.ToString() + "页\">" + i.ToString() + "</a></li>");
                if (i == TotalPage) break;
            }
            if (_PageEnd == TotalPage)
            {
                if (TotalPage > PageIndex)
                {
                    _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, (PageIndex + 1)) + "\" title=\"下一页\">下一页</a></li>");
                    _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, TotalPage) + "\" title=\"尾页\">尾页</a></li>");
                }
            }
            else
            {
                _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, (PageIndex + 1)) + "\" title=\"下一页\">下一页</a></li>");
                _PagerHtml.Append("<li><a href=\"" + string.Format(_LinkFormat, TotalPage) + "\" title=\"尾页\">尾页</a></li>");
            }
            _PagerHtml.Append("</ul></div>");
            return _PagerHtml.ToString();
        }
    }
}
