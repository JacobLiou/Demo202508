using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace UpdateFileServerToQiNiu
{
    public class HttpHelper
    {
        public static string createPostJson(string url, string postDataStr)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = "application/json;charset=UTF-8";
            httpWebRequest.ContentLength = (long)postDataStr.Length;
            StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream(), Encoding.ASCII);
            streamWriter.Write(postDataStr);
            streamWriter.Flush();
            streamWriter.Close();
            HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
            string name = response.ContentEncoding;
            if (name == null || name.Length < 1)
                name = "UTF-8";
            StreamReader streamReader = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding(name));
            string end = streamReader.ReadToEnd();
            streamReader.Close();
            return end;
        }

        public static string HttpPost(
          string url,
          Dictionary<string, string> headers = null,
          string postData = null,
          string contentType = "application/x-www-form-urlencoded",
          int timeOut = 30)
        {
            postData = postData ?? "";
            HttpClientHandler handler = new HttpClientHandler();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            if (headers != null && headers.Count(t => t.Key.ToLower() == "cookie") > 0)
                handler.UseCookies = false;
            if (headers != null &&
                headers.Count(t => t.Key.ToLower() == "accept-encoding" && t.Value.ToLower().Contains("gzip")) > 0)
                handler.AutomaticDecompression = DecompressionMethods.GZip;
            using (HttpClient httpClient = new HttpClient((HttpMessageHandler)handler))
            {
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                        request.Headers.Add(header.Key, header.Value);
                }

                using (HttpContent httpContent = (HttpContent)new StringContent(postData, Encoding.UTF8))
                {
                    if (contentType != null)
                        httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    return httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;
                }
            }
        }

        public static string HttpPost(
          string url,
          Dictionary<string, string> headers = null,
          Dictionary<string, string> pdatas = null,
          string contentType = "application/x-www-form-urlencoded",
          int timeOut = 30)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                if (headers.Count<KeyValuePair<string, string>>(
                  (Func<KeyValuePair<string, string>, bool>)(t => t.Key.ToLower() == "cookie")) > 0)
                    handler.UseCookies = false;
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (HttpClient httpClient = new HttpClient((HttpMessageHandler)handler))
                {
                    if (headers != null)
                    {
                        foreach (KeyValuePair<string, string> header in headers)
                            request.Headers.Add(header.Key, header.Value);
                    }

                    using (HttpContent httpContent =
                      (HttpContent)new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)pdatas))
                    {
                        if (contentType != null)
                            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                        request.Content = httpContent;
                        return httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;
                    }
                }
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public static string PostUrl(string url, string postData, Dictionary<string, string> headers = null)
        {
            string result = "";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.Timeout = 800;//请求超时时间
                byte[] data = Encoding.UTF8.GetBytes(postData);
                req.ContentLength = data.Length;
                if (null != headers)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                        req.Headers.Add(header.Key, header.Value);
                }
                using (Stream reqStream = req.GetRequestStream())
                {
                    reqStream.Write(data, 0, data.Length);
                    reqStream.Close();
                }
                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                Stream stream = resp.GetResponseStream();
                //获取响应内容
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    result = reader.ReadToEnd();
                }
            }
            catch (Exception e) { }
            return result;
        }

        public static string HttpGet(
          string url,
          string contentType = "application/json",
          Dictionary<string, string> headers = null)
        {
            HttpClientHandler handler = new HttpClientHandler();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            if (headers != null)
            {
                if (headers.Count<KeyValuePair<string, string>>(
                  (Func<KeyValuePair<string, string>, bool>)(t => t.Key.ToLower() == "cookie")) > 0)
                    handler.UseCookies = false;
                if (headers.Count<KeyValuePair<string, string>>((Func<KeyValuePair<string, string>, bool>)(t =>
                  t.Key.ToLower() == "accept-encoding" && t.Value.ToLower().Contains("gzip"))) > 0)
                    handler.AutomaticDecompression = DecompressionMethods.GZip;
            }

            using (HttpClient httpClient = new HttpClient((HttpMessageHandler)handler))
            {
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                        request.Headers.Add(header.Key, header.Value);
                }

                return httpClient.SendAsync(request).Result.Content.ReadAsStringAsync().Result;
            }
        }
    }
}