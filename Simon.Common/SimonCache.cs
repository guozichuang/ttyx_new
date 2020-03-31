using System.Collections.Generic; 
using System.Web; 
using System;

namespace Simon.Common
{
    /// <summary> 
    /// 缓存控制类 
    /// </summary>  
    public class SimonCache
    {
        /// <summary>
        /// 所有用户缓存
        /// </summary>
        public static List<string> AllUseCacheKey = new List<string>();

        /// <summary> 
        /// 添加缓存 
        /// </summary> 
        /// <param name="key"></param> 
        /// <param name="value"></param> 
        /// <param name="absoluteExpiration"></param> 
        public static void AddCache(string key, object value, DateTime absoluteExpiration)
        {
            if (!AllUseCacheKey.Contains(key))
            {
                AllUseCacheKey.Add(key);
            }
            HttpContext.Current.Cache.Add(key, value, null, absoluteExpiration, TimeSpan.Zero, System.Web.Caching.CacheItemPriority.Normal, null);
        }

        /// <summary> 
        /// 取出缓存
        /// </summary> 
        /// <param name="key"></param> 
        public static object GetCache(string key)
        {
            if (AllUseCacheKey.Contains(key))
                return HttpContext.Current.Cache[key];
            else
                return null;
        }

        /// <summary> 
        /// 移除缓存 
        /// </summary> 
        /// <param name="key"></param> 
        public static void RemoveCache(string key)
        {
            if (AllUseCacheKey.Contains(key))
            {
                AllUseCacheKey.Remove(key);
            }
            HttpContext.Current.Cache.Remove(key);
        }
        /// <summary> 
        /// 清空使用的缓存 
        /// </summary> 
        public static void ClearCache()
        {
            foreach (string value in AllUseCacheKey)
            {
                HttpContext.Current.Cache.Remove(value);
            }
            AllUseCacheKey.Clear();
        }
    }
}
