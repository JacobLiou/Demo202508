using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace UpdateFileServerToQiNiu
{
    static class Program
    {
        // 查看位置，https://portal.qiniu.com/developer/user/key
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConfigForm());
        }

    }
}
