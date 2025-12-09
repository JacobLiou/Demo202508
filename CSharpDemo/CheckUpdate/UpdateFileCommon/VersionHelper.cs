using System;
using System.Collections.Generic;
using System.Text;
using UpdateFileCommon;
using System.Net;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Windows.Forms;

namespace UpdateFileCommon
{
    public class VersionHelper
    {
        /// <summary>
        /// 剪切新版的更新程序
        /// </summary>
        /// <returns></returns>
        public static void CutNewUpdateEXE()
        {
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
            XmlDocument localDoc = new XmlDocument();
            localDoc.Load(xmlPath);
            var UpdateProgramDic = XMLHelper.GetXmlNodeValueByXpath(localDoc, "/Config/UpdateProgramDirectory");
            var UpdateProgramName = XMLHelper.GetXmlNodeValueByXpath(localDoc, "/Config/ClientProgramName");
            var updatefile = String.Format(System.AppDomain.CurrentDomain.BaseDirectory + "\\{0}\\{1}", UpdateProgramDic, UpdateProgramName);
            // 2024.06.26增加下载完整性判断
            int localVersion = GetLocalVersionValue();
            int serverVersion = GetServerVersionValue();
            if (File.Exists(updatefile) && localVersion == serverVersion)
            {
                var savepath = System.AppDomain.CurrentDomain.BaseDirectory + "\\" + UpdateProgramName;
                //如果原文件存在，则删除
                if (File.Exists(savepath))
                    File.Delete(savepath);
                File.Move(updatefile, savepath);
            }
        }


        /// <summary>
        /// 获取本地版本号
        /// </summary>
        /// <returns></returns>
        public static string GetLocalVersion()
        {
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
            return XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/LocalVersion");
        }

        /// <summary>
        /// 获取本地版本号具体版本值
        /// </summary>
        /// <returns></returns>
        public static int GetLocalVersionValue()
        {
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
            var value = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/LocalVersionValue");
            int versionval = 0;
            int.TryParse(value, out versionval);
            return versionval;
        }

        /// <summary>
        /// 获取服务器的最新版本号
        /// </summary>
        /// <returns></returns>
        public static int GetServerVersionValue()
        {
            var serverurl = VersionHelper.GetLoaclServerConfigURL();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(serverurl); //加载XML文档
            //获取服务器版本号
            var vernode = XMLHelper.GetXmlNodeByXpath(xmlDoc, "/Config/Version/Item");
            var vervalue = XMLHelper.GetNodeAttributeValue(vernode, "VersionValue");
            int versionval = 0;
            int.TryParse(vervalue, out versionval);
            return versionval;
        }


        /// <summary>
        /// 是否需要更新
        /// </summary>
        /// <returns></returns>
        public static bool IsRequiredUpdate()
        {
            if (WebHelper.CheckWebIsOK())
            {
                var local = GetLocalVersionValue();
                var server = GetServerVersionValue();
                return local < server;
            }
            else
                return false;
        }

        /// <summary>
        /// 获取需要更新的文件
        /// </summary>
        /// <returns></returns>
        public static List<UpdateFileInfo> GetNeedUpdateFileFullInfo()
        {
            try
            {
                List<UpdateFileInfo> localFile, serverFile;
                GetXMlInfo(out localFile, out serverFile);

                List<UpdateFileInfo> needDownloadFileList = new List<UpdateFileInfo>();
                foreach (var item in serverFile)
                {
                    var localItem = localFile.Find(delegate (UpdateFileInfo file)
                    {
                        return file.Name == item.Name;
                    });
                    if (localItem != null)
                    {
                        if (localItem.Version < item.Version)
                            needDownloadFileList.Add(item);
                    }
                    else
                        needDownloadFileList.Add(item);
                }

                return needDownloadFileList;
            }
            catch (Exception)
            {
                return null;
            }

        }


        /// <summary>
        /// 获取需要更新的文件
        /// </summary>
        /// <returns></returns>
        public static List<string> GetNeedUpdateFile()
        {
            try
            {
                List<UpdateFileInfo> localFile, serverFile;
                GetXMlInfo(out localFile, out serverFile);

                List<string> needDownloadFileList = new List<string>();
                foreach (var item in serverFile)
                {
                    var localItem = localFile.Find(delegate (UpdateFileInfo file)
                    {
                        return file.Name == item.Name;
                    });
                    if (localItem != null)
                    {
                        if (localItem.Version < item.Version)
                            needDownloadFileList.Add(item.Name);
                    }
                    else
                        needDownloadFileList.Add(item.Name);
                }

                return needDownloadFileList;
            }
            catch (Exception)
            {
                return null;
            }

        }

        private static void GetXMlInfo(out List<UpdateFileInfo> localFile, out List<UpdateFileInfo> serverFile)
        {
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
            XmlDocument localDoc = new XmlDocument();
            localDoc.Load(xmlPath);
            //先获取本地的文件列表
            var localNodes = XMLHelper.GetXmlNodeListByXpath(localDoc, "/Config/File/Item");
            int tryInt = 0;
            localFile = new List<UpdateFileInfo>();
            foreach (XmlNode item in localNodes)
            {
                var name = XMLHelper.GetNodeAttributeValue(item, "Name");
                int.TryParse(XMLHelper.GetNodeAttributeValue(item, "Version"), out tryInt);
                var version = tryInt;
                var date = XMLHelper.GetNodeAttributeValue(item, "Date");
                localFile.Add(new UpdateFileInfo() { Name = name, Version = version, Date = date });
            }
            var serverurl = VersionHelper.GetLoaclServerConfigURL(localDoc);

            //再获取服务器的文件列表
            serverFile = new List<UpdateFileInfo>();
            XmlDocument serverDoc = new XmlDocument();
            serverDoc.Load(serverurl);
            var serverNodes = XMLHelper.GetXmlNodeListByXpath(serverDoc, "/Config/File/Item");

            foreach (XmlNode item in serverNodes)
            {
                var name = XMLHelper.GetNodeAttributeValue(item, "Name");
                int.TryParse(XMLHelper.GetNodeAttributeValue(item, "Version"), out tryInt);
                var version = tryInt;
                var date = XMLHelper.GetNodeAttributeValue(item, "Date");
                serverFile.Add(new UpdateFileInfo() { Name = name, Version = version, Date = date });
            }
        }


        /// <summary>
        /// 从服务器下载数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="savePath"></param>
        public void FromServerDownLoadFile(string filePath, string savePath)
        {
            var uri = new Uri(filePath);
            WebClient wClient = new WebClient();
            wClient.DownloadFileAsync(uri, savePath);
        }

        /// <summary>
        /// 获取服务器文件的根路径
        /// </summary>
        /// <returns></returns>
        public static string GetLoaclServerConfigURL(XmlDocument xmlDoc)
        {
            var serverurl = XMLHelper.GetXmlNodeValueByXpath(xmlDoc, "/Config/ServerUrl");
            return serverurl;
        }

        /// <summary>
        /// 获取服务器文件的根路径
        /// </summary>
        /// <returns></returns>
        public static string GetLoaclServerConfigURL(string xmlPath)
        {
            var serverurl = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/ServerUrl");
            return serverurl;
        }

        /// <summary>
        /// 获取服务器文件的根路径
        /// </summary>
        /// <returns></returns>
        public static string GetLoaclServerConfigURL()
        {
            string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
            var serverurl = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/ServerUrl");
            return serverurl;
        }


        /// <summary>
        /// 获取服务器版本信息
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static string GetServiceVersion(XmlDocument xmlDoc)
        {
            //获取版本信息
            var vernode = XMLHelper.GetXmlNodeByXpath(xmlDoc, "/Config/Version/Item");
            var ver = XMLHelper.GetNodeAttributeValue(vernode, "Version");
            return ver;
        }

        /// <summary>
        /// 获取服务器版本是否强制更新
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static bool GetServiceIsMustUpdate(XmlDocument xmlDoc)
        {
            var isMustUpdate = XMLHelper.GetXmlNodeValueByXpath(xmlDoc, "/Config/Version/Item/IsMustUpdate");
            return isMustUpdate.ToUpper() == "TRUE";
        }

        /// <summary>
        /// 获取服务器版本更新描述信息
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static string GetServiceReleaseNote(XmlDocument xmlDoc)
        {
            //获取版本信息
            var releaseNote = XMLHelper.GetXmlNodeValueByXpath(xmlDoc, "/Config/Version/Item/ReleaseNote");
            return releaseNote;
        }


        /// <summary>
        /// 发现新版本调用下载
        /// </summary>
        public static bool GetNewVersionToDownloadSetup(XmlDocument doc = null)
        {
            if (doc == null)
            {
                string xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFile.xml";
                doc = new XmlDocument();
                doc.Load(xmlPath);
            }
            var currentSetupVersion = XMLHelper.GetXmlNodeValueByXpath(doc, "/Config/Setup");
            var serverUrl = VersionHelper.GetLoaclServerConfigURL();
            XmlDocument serverDoc = new XmlDocument();
            serverDoc.Load(serverUrl); //加载XML文档
            //获取服务器版本号
            var setupNode = XMLHelper.GetXmlNodeByXpath(serverDoc, "/Config/Setup");
            var setVersion = XMLHelper.GetNodeAttributeValue(setupNode, "Version");
            int cV = 0; int sV = 0;
            bool isNeedDownLoad = false;
            int.TryParse(currentSetupVersion, out cV);
            int.TryParse(setVersion, out sV);
            if (cV < sV)
            {
                var setupURL = setupNode.InnerText;
                //if (File.Exists(setupURL))
                //{
                MessageBox.Show("发现新版本，请进行下载安装", "提示信息");
                Process.Start(setupURL);
                isNeedDownLoad = true;
                //}
            }
            return isNeedDownLoad;
        }
    }
}
