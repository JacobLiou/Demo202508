using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using UpdateFileServerToQiNiu.Utils;

namespace UpdateFileTest
{
    [TestClass]
    public class UnitTest1
    {
        // 查看位置，https://portal.qiniu.com/developer/user/key
        private const string AK = "";
        private const string SK = "";
        private const string BUCKETNAME = "";


        [TestMethod]
        public void TestMethod1()
        {
            String domainName = QiNiuUtils.getBucketDomain(AK, SK, BUCKETNAME);
            Assert.AreEqual(domainName, "http://update.ksaq.com.cn/");
        }

        [TestMethod]
        public void Test获取文件md5()
        {
            String md5 = QiNiuUtils.getUploadFileMd5(AK, SK, BUCKETNAME, "UpdateServer.xml");
            Assert.AreEqual(md5, "e9e674f44a78f45488a80fa477c0dc04");
        }

        [TestMethod]
        public void TestGet获取文件是否存在() {
            var result = UrlFileExistsByGet("http://update.ksaq.com.cn/cool-admin-java-4.01.jar");
            Assert.IsTrue(result);
        }

        /// <summary>
        /// 文件是否存在，GET方法
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private bool UrlFileExistsByGet(string uri)
        {
            HttpWebRequest req = null;
            HttpWebResponse res = null;
            try
            {
                req = (HttpWebRequest)WebRequest.Create(uri);
                req.Method = "GET";
                req.Timeout = 3000;
                res = (HttpWebResponse)req.GetResponse();
                string contentLength = res.GetResponseHeader("Content-Length");
                return !string.IsNullOrEmpty(contentLength);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (res != null)
                {
                    res.Close();
                    res = null;
                }
                if (req != null)
                {
                    req.Abort();
                    req = null;
                }
            }
        }
    }
}
