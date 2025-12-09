using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qiniu.CDN;
using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace UpdateFileServerToQiNiu.Utils
{
    public static class QiNiuUtils
    {
        /// <summary>
        /// 获取空间绑定域名
        /// </summary>
        /// <param name="AK"></param>
        /// <param name="SK"></param>
        /// <param name="bucketName"></param>
        /// <returns></returns>
        public static string getBucketDomain(string AK, string SK, string bucketName)
        {
            try
            {
                string getUrl = "http://uc.qiniuapi.com/v2/domains?tbl=" + bucketName;
                Mac mac = new Mac(AK, SK);
                string manageToken = Auth.CreateManageToken(mac, getUrl);

                HttpManager httpManager = new HttpManager();
                HttpResult httpResult = httpManager.Get(getUrl, manageToken);
                // ["update.ksaq.com.cn"]
                String[] domainArr = JsonConvert.DeserializeObject<string[]>(httpResult.Text);
                if (domainArr.Length > 0)
                {
                    return "http://" + domainArr[0] + "/";
                }
            }
            catch (Exception ex) { }
            return "域名未绑定，请先绑定域名";
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="AK"></param>
        /// <param name="SK"></param>
        /// <param name="bucketName"></param>
        /// <returns>上传的文件名</returns>
        public static string uploadFile(string AK, string SK, string bucketName, string filePath, string fileName)
        {
            Mac mac = new Mac(AK, SK);
            PutPolicy putPolicy = new PutPolicy();
            putPolicy.Scope = bucketName + ":" + fileName;
            //putPolicy.ReturnBody = "{\"key\":\"$(key)\",\"hash\":\"$(etag)\",\"fsiz\":$(fsize),\"bucket\":\"$(bucket)\",\"name\":\"$(x:name)\"}";
            string token = Auth.CreateUploadToken(mac, putPolicy.ToJsonString());

            Config config = new Config();
            // 设置上传区域
            config.Zone = Zone.ZONE_CN_East;
            // 设置 http 或者 https 上传
            config.UseHttps = false;
            config.UseCdnDomains = true;
            config.ChunkSize = ChunkUnit.U512K;
            // 表单上传
            FormUploader target = new FormUploader(config);
            HttpResult result = target.UploadFile(filePath, fileName, token, null);
            JObject resultJ = JsonConvert.DeserializeObject<JObject>(result.Text);
            if (resultJ != null && resultJ.ContainsKey("key"))
            {
                return resultJ["key"].ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取七牛编码URL
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string getEncodedEntryURI(string bucketName, string key)
        {
            string entry = bucketName + ":" + key;
            return Qiniu.Util.Base64.UrlSafeBase64Encode(entry);
        }

        /// <summary>
        /// 获取七牛文件MD5
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string getUploadFileMd5(string AK, string SK, string bucketName, string fileName)
        {

            string entryUrl = getEncodedEntryURI(bucketName, fileName);
            string getUrl = "http://rs.qiniuapi.com/stat/" + entryUrl;
            Mac mac = new Mac(AK, SK);
            string manageToken = Auth.CreateManageToken(mac, getUrl);
            HttpManager httpManager = new HttpManager();
            HttpResult httpResult = httpManager.Get(getUrl, manageToken);
            // ["update.ksaq.com.cn"]
            JObject resultJ = JsonConvert.DeserializeObject<JObject>(httpResult.Text);
            if (resultJ != null && resultJ.ContainsKey("md5"))
            {
                return resultJ["md5"].ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取远端文件Md5
        /// </summary>
        /// <param name="qiNiuPath"></param>
        /// <returns></returns>
        public static string getRemoteFileMd5(string qiNiuPath)
        {
            string getUrl = qiNiuPath + "?qhash/md5";
            string response = HttpHelper.HttpGet(getUrl);
            JObject resultJ = JsonConvert.DeserializeObject<JObject>(response);
            if (resultJ != null && resultJ.ContainsKey("hash"))
            {
                return resultJ["hash"].ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 刷新CDN
        /// </summary>
        /// <param name="AK"></param>
        /// <param name="SK"></param>
        /// <param name="domain"></param>
        /// <param name="urls"></param>
        /// <returns></returns>
        public static bool refreshCDN(string AK, string SK, string domain, string[] urls)
        {
            Mac mac = new Mac(AK, SK);
            CdnManager manager = new CdnManager(mac);

            RefreshResult ret = manager.RefreshUrls(urls);
            return ret.Code == (int)HttpCode.OK;
        }
    }
}
