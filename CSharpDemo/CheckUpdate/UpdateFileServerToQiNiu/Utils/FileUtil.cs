using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UpdateFileServerToQiNiu.Utils
{
    internal class FileUtil
    {
        /// <summary>
        /// 生成MD5码版本号:读取目前软件执行档然后产生MD5码，作为软件版本。
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public static string GetFileMd5Code(string filePath)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            if (filePath != "" && File.Exists(filePath))
            {
                using (var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                {
                    string backfilename = filePath + "e";
                    if (System.IO.File.Exists(backfilename) == true)
                    {
                        System.IO.File.Delete(backfilename);
                    }

                    System.IO.File.Copy(filePath, backfilename);//复制一份，防止占用

                    //利用复制的执行档建立MD5码
                    using (System.IO.FileStream fs = new System.IO.FileStream(filePath + "e", FileMode.Open))
                    {
                        byte[] bt = md5.ComputeHash(fs);
                        for (int i = 0; i < bt.Length; i++)
                        {
                            builder.Append(bt[i].ToString("x2"));
                        }
                    }
                    System.IO.File.Delete(filePath + "e");//删除复制的文件，这里没处理异常.
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static void SaveFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }
}
