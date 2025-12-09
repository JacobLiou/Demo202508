using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace UpdateFileCommon
{
    public partial class PromptingForm : Form
    {
        public delegate void NextShowDelegate();
        public event NextShowDelegate NextShowEvent;
        public PromptingForm(string releaseNote, bool isMustUpdate)
        {
            InitializeComponent();
            this.txtDescription.Text = releaseNote;
            this.btnNextShow.Visible = !isMustUpdate;
            //下次提醒
            this.btnNextShow.Click += delegate
            {
                if (NextShowEvent != null)
                    NextShowEvent();
            };
            //更新
            this.btnUpdate.Click += delegate
            {
                string clientName = System.AppDomain.CurrentDomain.BaseDirectory + "UpdateFileClient.exe";
                if (File.Exists(clientName))
                { 
                    Process.Start(clientName);
                }
                Application.Exit();
            };
        }
    }
}
