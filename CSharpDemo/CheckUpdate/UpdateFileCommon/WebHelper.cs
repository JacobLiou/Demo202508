using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace UpdateFileCommon
{
    public class WebHelper
    {
        [DllImport("wininet")]
        private extern static bool InternetGetConnectedState(out int connectionDescription, int reservedValue);

        /// <summary>
        /// 检查网络是否畅通
        /// </summary>
        /// <returns></returns>
        public static bool CheckWebIsOK()
        {
            int i = 0;
            var result = InternetGetConnectedState(out i, 0);
            if (result)
            {
                try
                {
                    try
                    {
                        string all = GetUrlRequestContent("http://www.baidu.com");   //读取网站返回的数据
                        if (String.IsNullOrEmpty(all))
                        {
                            all = GetUrlRequestContent("http://cn.bing.com/");
                            if (String.IsNullOrEmpty(all))
                                result = false;
                        }
                    }
                    catch (Exception)
                    {
                        string all = GetUrlRequestContent("http://cn.bing.com/");
                        if (String.IsNullOrEmpty(all))
                            result = false;
                    }

                }
                catch (Exception)
                {
                    result = false;
                }
            }
            return result;
        }

        /// <summary>
        /// 获取网页内容
        /// </summary>
        /// <returns></returns>
        private static string GetUrlRequestContent(string url)
        {
            Uri uri = new Uri(url);
            WebRequest webreq = WebRequest.Create(uri);
            Stream s = webreq.GetResponse().GetResponseStream();
            StreamReader sr = new StreamReader(s, Encoding.Default);
            string all = sr.ReadToEnd();
            return all;
        }

    }
}
