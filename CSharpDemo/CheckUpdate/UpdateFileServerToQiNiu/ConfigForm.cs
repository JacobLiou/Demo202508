using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using UpdateFileCommon;
using UpdateFileServerToQiNiu.Model;
using UpdateFileServerToQiNiu.Utils;

namespace UpdateFileServerToQiNiu
{
    public partial class ConfigForm : Form, INotifyPropertyChanged
    {
        private BindingList<ServerUpdateFileInfo> _fileList;

        /// <summary>
        /// 文件集合
        /// </summary>
        public BindingList<ServerUpdateFileInfo> FileList
        {
            get { return _fileList; }
            set { _fileList = value; ChangedProperty("FileList"); }
        }

        public ConfigForm()
        {
            InitializeComponent();
            // 处理文件复制按钮文本定时器
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000; // 1秒钟的时间间隔
            timer.Tick += OnTimedEvent;

            this.Load += delegate
            {
                FileList = new BindingList<ServerUpdateFileInfo>();
                var xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateServer.xml";
                //获取所有文件，封装成集合
                var nodeList = XMLHelper.GetXmlNodeListByXpath(xmlPath, "/Config/File/Item");

                var configPath = System.AppDomain.CurrentDomain.BaseDirectory + "config.json";
                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath);
                    if (!string.IsNullOrEmpty(content))
                    {
                        ConfigSaveInfo configInfos = JsonConvert.DeserializeObject<ConfigSaveInfo>(content);
                        FileList = new BindingList<ServerUpdateFileInfo>(configInfos.FileList.OrderBy(t => t.SortIndex).ToList());
                        txtAccessKey.Text = configInfos.AK;
                        txtSecretKey.Text = configInfos.SK;
                        txtBuckteName.Text = configInfos.BucketName;
                    }
                }
                this.dataGridView1.AutoGenerateColumns = false;
                this.dataGridView1.DataBindings.Add("DataSource", this, "FileList");

                //获取服务器URL
                var serverFile = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/ServerFile");
                this.txtServerURLFileName.Text = serverFile;
                //获取版本信息
                var vernode = XMLHelper.GetXmlNodeByXpath(xmlPath, "/Config/Version/Item");
                var ver = XMLHelper.GetNodeAttributeValue(vernode, "Version");
                var vervalue = XMLHelper.GetNodeAttributeValue(vernode, "VersionValue");
                this.lblVersionValue.Text = vervalue;
                this.txtVersion.Text = ver;
                //this.lblk
                //获取更新描述
                var releaseNote = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/Version/Item/ReleaseNote");
                this.txtDescription.Text = releaseNote;
                //获取是否强制更新
                var isMustUpdate = XMLHelper.GetXmlNodeValueByXpath(xmlPath, "/Config/Version/Item/IsMustUpdate");
                this.chkIsMustUpdate.Checked = isMustUpdate.ToUpper() == "TRUE";
                //获取安装包版本
                var setupNode = XMLHelper.GetXmlNodeByXpath(xmlPath, "/Config/Setup");
                this.txtSetupURL.Text = setupNode.InnerText;
                this.txtSetupVersion.Text = XMLHelper.GetNodeAttributeValue(setupNode, "Version");
                // 异步更新本地文件MD5
                ThreadPool.QueueUserWorkItem(updateLocalFileMd5);
            };

            this.btnUploadFile.Click += delegate
            {
                ThreadPool.QueueUserWorkItem(uploadFileToQiNiu);
            };

            this.btnAdd.Click += delegate
            {
                ShowSelectFile();
            };

            this.btnApply.Click += delegate
            {
                applyFile();
            };
            this.dataGridView1.CellValueChanged += (sender, e) =>
            {
                int verValue = 0;
                int.TryParse(lblVersionValue.Text, out verValue);
                lblVersionValue.Text = (verValue + 1).ToString();
            };
            this.btnRefreshFileListCDN.Click += delegate
            {
                string domain = txtQiNiuDomain.Text.Substring(0, txtQiNiuDomain.Text.Length - 2);
                var fileList = FileList.Select(t => t.QiNiuPath).ToList();
                var rqResult = QiNiuUtils.refreshCDN(txtAccessKey.Text, txtSecretKey.Text, domain, fileList.ToArray());
                if (rqResult)
                {
                    MessageBox.Show("文件列表已请求刷新CDN，全网生效10分钟左右", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("刷新失败", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            this.btnRefreshUpdateFileXMl.Click += delegate
            {
                string domain = txtQiNiuDomain.Text.Substring(0, txtQiNiuDomain.Text.Length - 2);
                var fileList = new List<string>();
                // 增加服务端文件刷新
                fileList.Add(lblServerUrl.Text);
                var rqResult = QiNiuUtils.refreshCDN(txtAccessKey.Text, txtSecretKey.Text, domain, fileList.ToArray());
                if (rqResult)
                {
                    MessageBox.Show("发布文件已请求刷新CDN，全网生效10分钟左右", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("刷新失败", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            #region 右键菜单
            this.menuAdd.Click += delegate { ShowSelectFile(); };
            this.menuEdit.Click += delegate
            {
                if (this.dataGridView1.SelectedRows.Count > 0)
                    this.dataGridView1.BeginEdit(false);
            };
            this.menuDelete.Click += delegate
            {
                DialogResult dr = MessageBox.Show("确定要删除吗？", "提示信息", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr == DialogResult.OK)
                    this.dataGridView1.Rows.Remove(this.dataGridView1.SelectedRows[0]);
            };
            this.menuUpload.Click += delegate
            {
                // 上传文件到七牛云
                if (string.IsNullOrEmpty(this.txtAccessKey.Text) || string.IsNullOrEmpty(this.txtSecretKey.Text) || string.IsNullOrEmpty(this.txtBuckteName.Text))
                {
                    MessageBox.Show("请先配置七牛云信息", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (this.dataGridView1.SelectedRows.Count > 0)
                {
                    DataGridViewRow row = this.dataGridView1.SelectedRows[0];
                    if (row != null)
                    {
                        if (row.DataBoundItem != null)
                        {
                            ServerUpdateFileInfo serverUpdateFileInfo = (ServerUpdateFileInfo)row.DataBoundItem;
                            if (null != serverUpdateFileInfo)
                            {
                                if (File.Exists(serverUpdateFileInfo.LocalPath))
                                {
                                    string fileName = QiNiuUtils.uploadFile(this.txtAccessKey.Text, this.txtSecretKey.Text, this.txtBuckteName.Text, serverUpdateFileInfo.LocalPath, serverUpdateFileInfo.Name);
                                    serverUpdateFileInfo.QiNiuPath = txtQiNiuDomain.Text + "/" + fileName;
                                    string remoteMd5 = QiNiuUtils.getUploadFileMd5(txtAccessKey.Text, txtSecretKey.Text, txtBuckteName.Text, fileName);
                                    serverUpdateFileInfo.IsEquals = serverUpdateFileInfo.LocalMd5 == remoteMd5;
                                }
                                else
                                {
                                    serverUpdateFileInfo.LocalPath = "文件不存在！";
                                }
                            }
                        }
                    }
                }
            };
            #endregion
        }

        /// <summary>
        /// 更新本地文件Md5
        /// </summary>
        /// <param name="state"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void updateLocalFileMd5(object state)
        {
            if (FileList != null && FileList.Count > 0)
            {
                foreach (var item in FileList)
                {
                    string oldMd5 = item.LocalMd5;
                    string localMd5 = FileUtil.GetFileMd5Code(item.LocalPath);
                    item.IsEquals = oldMd5 == localMd5;
                    item.LocalMd5 = localMd5;
                    Thread.Sleep(200);
                }
            }
        }

        private void uploadFileToQiNiu(object state)
        {
            DialogResult dr = MessageBox.Show("确定要发布更新？操作不可逆，请谨慎", "提示信息", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (dr == DialogResult.OK)
            {
                if (FileList != null && FileList.Count > 0)
                {
                    var noList = FileList.Where(t => t.IsEquals == false).ToList();
                    if (noList.Count > 0)
                    {
                        foreach (var item in noList)
                        {
                            string key = QiNiuUtils.uploadFile(txtAccessKey.Text, txtSecretKey.Text, txtBuckteName.Text, item.LocalPath, item.Name);
                            item.QiNiuPath = txtQiNiuDomain.Text + "/" + key;
                            string remoteMd5 = QiNiuUtils.getUploadFileMd5(txtAccessKey.Text, txtSecretKey.Text, txtBuckteName.Text, key);
                            item.IsEquals = item.LocalMd5 == remoteMd5;
                            Thread.Sleep(200);
                        }
                    }
                }

                // 保存文件更新
                applyFile();

                // 接着上传配置文件
                var xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateServer.xml";
                // 上传文件到远端服务器
                string fileName = QiNiuUtils.uploadFile(txtAccessKey.Text, txtSecretKey.Text, txtBuckteName.Text, xmlPath, txtServerURLFileName.Text);
                if (!string.IsNullOrEmpty(fileName))
                {
                    MessageBox.Show(fileName + " 文件已更新至七牛云", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("上传失败", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 保存配置到本地
        /// </summary>
        private void applyFile()
        {
            var xmlPath = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateServer.xml";
            //保存设置到XML
            var xmlText = "";

            var fileListSort = FileList.OrderBy(t => t.SortIndex).ToList();
            foreach (var item in fileListSort)
            {
                xmlText += String.Format("<Item Name=\"{0}\" Version=\"{1}\" Date=\"{2}\" MD5=\"{3}\"></Item>", item.Name, item.Version, item.Date, item.LocalMd5);
            }
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath); //加载XML文档
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config", "File", xmlText);
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config", "ServerURL", this.txtQiNiuDomain.Text);
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config", "ServerFile", this.txtServerURLFileName.Text);
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config", "Setup", this.txtSetupURL.Text);
            XMLHelper.CreateOrUpdateXmlAttributeByXPath(xmlDoc, "/Config/Setup", "Version", this.txtSetupVersion.Text);
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config/Version/Item", "ReleaseNote", this.txtDescription.Text);
            XMLHelper.CreateOrUpdateXmlAttributeByXPath(xmlDoc, "/Config/Version/Item", "Version", this.txtVersion.Text);
            XMLHelper.CreateOrUpdateXmlAttributeByXPath(xmlDoc, "/Config/Version/Item", "VersionValue", this.lblVersionValue.Text);
            XMLHelper.CreateOrUpdateXmlNodeByXPath(xmlDoc, "/Config/Version/Item", "IsMustUpdate", this.chkIsMustUpdate.Checked.ToString());
            xmlDoc.Save(xmlPath);
            // 保存文件列表
            var configPath = System.AppDomain.CurrentDomain.BaseDirectory + "config.json";
            ConfigSaveInfo configSaveInfo = new ConfigSaveInfo()
            {
                AK = txtAccessKey.Text,
                SK = txtSecretKey.Text,
                BucketName = txtBuckteName.Text,
                FileList = FileList
            };
            string configJson = JsonConvert.SerializeObject(configSaveInfo);
            FileUtil.SaveFile(configPath, configJson);
            MessageBox.Show("本地文件已更新", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void getFileMd5ThreadMethod(object fileItem)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    if (fileItem != null)
                    {
                        ServerUpdateFileInfo serverUpdateFileInfo = (ServerUpdateFileInfo)fileItem;
                        if (serverUpdateFileInfo != null)
                        {
                            string localMd5 = FileUtil.GetFileMd5Code(serverUpdateFileInfo.LocalPath);
                            serverUpdateFileInfo.LocalMd5 = localMd5;
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            btnCopy.Text = "复制";
            timer.Stop();
        }

        /// <summary>
        /// 选择文件
        /// </summary>
        private void ShowSelectFile()
        {
            var currentPath = System.AppDomain.CurrentDomain.BaseDirectory;
            //弹窗选择文件
            OpenFileDialog file = new OpenFileDialog();
            //file.InitialDirectory = currentPath;
            file.Multiselect = true;
            DialogResult rs = file.ShowDialog();
            if (rs == DialogResult.OK)
            {
                var files = file.FileNames;
                //判断选择的文件路径是否是当前运行程序中的文件夹路径
                for (int i = 0; i < files.Length; i++)
                {
                    //添加到集合
                    //if (files[i].Contains(currentPath))
                    //{
                    //添加
                    var fileItem = new ServerUpdateFileInfo()
                    {
                        Name = file.SafeFileNames[i],
                        Version = 1,
                        Date = DateTime.Now.ToString("yyyy-MM-dd"),
                        LocalPath = files[i],
                        SortIndex = FileList.Count
                    };
                    FileList.Insert(0, fileItem);
                    // 解析文件
                    ThreadPool.QueueUserWorkItem(getFileMd5ThreadMethod, fileItem);
                    //}
                    //else
                    //    MessageBox.Show(String.Format("所选 {0} 文件在当前程序所在目录，请复制到当前程序所在目录", file.SafeFileNames[i]), "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 通知属性已改变
        /// </summary>
        /// <param name="propertyName"></param>
        public void ChangedProperty(string propertyName)
        {
            if (PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private void txtAccessKey_TextChanged(object sender, EventArgs e)
        {
            // 根据填的数据，抓取空间绑定域名
            string AK = this.txtAccessKey.Text;
            string SK = this.txtSecretKey.Text;
            string bucketName = this.txtBuckteName.Text;
            String domain = QiNiuUtils.getBucketDomain(AK, SK, bucketName);
            txtQiNiuDomain.Text = domain;
        }

        private void txtQiNiuDomain_TextChanged(object sender, EventArgs e)
        {
            lblServerUrl.Text = txtQiNiuDomain.Text + this.txtServerURLFileName.Text;
        }

        private System.Windows.Forms.Timer timer;
        private void btnCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(lblServerUrl.Text);
            btnCopy.Text = "已复制";
            timer.Start();
        }
    }
}
