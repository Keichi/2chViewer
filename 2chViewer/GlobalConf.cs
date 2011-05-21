using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace The2chViewer
{
    /// <summary>
    /// BbsThread,BbsMenu,BbsBoard全てに関わる設定
    /// </summary>
    public static class GlobalConf
    {
        public static string BrowserName { get; set; }
        public static string BrowserVersion { get; set; }
        public static string UserAgent { get; set; }
        /// <summary>
        /// Bg20サーバを用いるか（バーボン規制を食らわない）
        /// </summary>
        public static bool UseBg20 { get; set; }

        static GlobalConf()
        {
            BrowserName = "lib2ch";
            BrowserVersion = "0.00";
            UserAgent = string.Format("Monazilla/1.00 ({0}/{1})", BrowserName, BrowserVersion);
            UseBg20 = false;
            System.Net.WebRequest.DefaultWebProxy = null;
        }
    }
}
