using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;

namespace The2chViewer
{
    public class DownloadCompletedEventArgs
    {
        
    }

    /// <summary>
    /// 2chの板一覧を表すクラス
    /// </summary>
    public class BbsMenu
    {
        public static string CacheDir;
        /// <summary>
        /// カテゴリのリスト
        /// </summary>
        public List<string> Categories { get; set; }
        /// <summary>
        /// カテゴリとカテゴリに属する板の辞書
        /// </summary>
        public Dictionary<string, List<BbsBoard>> Boards { get; set; }

        private Thread _workerThread;

        public BbsMenu()
        {
            Categories = new List<string>();
            Boards = new Dictionary<string, List<BbsBoard>>();
        }

        public event Action<object, DownloadCompletedEventArgs> DownladCompleted;

        public void DownloadAsync()
        {
            _workerThread = new Thread(
                () => {
                    Download();
                    DownladCompleted(this, new DownloadCompletedEventArgs());
                });
            _workerThread.Start();
        }

        public void DownloadCancel()
        {
            if (_workerThread != null && _workerThread.ThreadState == System.Threading.ThreadState.Running) {
                _workerThread.Abort();
            }
        }

        /// <summary>
        /// 板一覧をダウンロードする
        /// </summary>
        public void Download()
        {
            Categories.Clear();
            var menuAddress = "http://menu.2ch.net/bbsmenu.html";
            var content = "";

            var sp = new Stopwatch();
            sp.Start();
            using (var client = new WebClient()) {
                client.Headers.Add(HttpRequestHeader.UserAgent, GlobalConf.UserAgent);
                content = client.DownloadString(menuAddress);
            }
            sp.Stop();
            Debug.WriteLine("BBS menu loaded: taken " + sp.ElapsedMilliseconds / 1000.0 + "sec.");

            var splitted = Regex.Split(content, "<B>(?<category>.+?)</B>");
            for (var i = 1; i < splitted.Count() - 1; i += 2) {
                var category = splitted[i];
                Categories.Add(category);

                var pattern = "<A HREF=http://(?<server>[a-zA-Z0-9]+?.2ch.net)/(?<dir>[a-zA-Z0-9]+?)/>(?<name>.+?)</A>";
                if (category == "BBSPINK") {
                    pattern = "<A HREF=http://(?<server>[a-zA-Z0-9]+?.bbspink.com)/(?<dir>[a-zA-Z0-9]+?)/>(?<name>.+?)</A>";
                }
                var matches = Regex.Matches(splitted[i + 1], pattern);
                if (matches.Count == 0) {
                    continue;
                }
                var boards = new List<BbsBoard>();
                foreach (Match match in matches) {
                    var board = new BbsBoard
                                    {
                                        Name = match.Groups["name"].Value,
                                        BoardDir = match.Groups["dir"].Value,
                                        HostName = match.Groups["server"].Value
                                    };
                    boards.Add(board);
                }
                Boards.Add(category, boards);
            }
        }
    }
}