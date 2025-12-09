using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace UpdateFileCommon
{
    public class ServerUpdateFileInfo : INotifyPropertyChanged
    {
        private string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; ChangedProperty("Name"); }
        }

        private int _version;

        public int Version
        {
            get { return _version; }
            set { _version = value; ChangedProperty("Version"); }
        }


        private string _date;

        public string Date
        {
            get { return _date; }
            set { _date = value; ChangedProperty("Date"); }
        }


        private string _localPath;

        public string LocalPath
        {
            get { return _localPath; }
            set { _localPath = value; ChangedProperty("LocalPath"); }
        }

        private string _qiNiuPath;

        public string QiNiuPath
        {
            get { return _qiNiuPath; }
            set { _qiNiuPath = value; ChangedProperty("QiNiuPath"); }
        }

        private bool _isEquals;

        public bool IsEquals
        {
            get { return _isEquals; }
            set { _isEquals = value; ChangedProperty("IsEquals"); }
        }


        private string _localMd5;

        public string LocalMd5
        {
            get { return _localMd5; }
            set { _localMd5 = value; ChangedProperty("LocalMd5"); }
        }

        private int _sortIndex;

        public int SortIndex
        {
            get { return _sortIndex; }
            set { _sortIndex = value; ChangedProperty("SortIndex"); }
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
    }
}
