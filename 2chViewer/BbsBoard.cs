using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using ThreadState = System.Threading.ThreadState;

namespace The2chViewer
{
    /// <summary>
    /// 2chの板を表すクラス
    /// </summary>
    public class BbsBoard
    {
        public static string CacheDir;
        /// <summary>
        /// 板の名前
        /// </summary>
        public string Name { get; set;}
        /// <summary>
        /// 板の存在するサーバのホスト名
        /// </summary>
        public string HostName { get; set;}
        /// <summary>
        /// 板のサーバ内でのディレクトリ
        /// </summary>
        public string BoardDir { get; set; }
        /// <summary>
        /// スレッドのリスト
        /// </summary>
        public List<BbsThread> Threads { get; set;}

        private Thread _workerThread;

        public BbsBoard()
        {
            Threads = new List<BbsThread>();
        }

        public event Action<object, DownloadCompletedEventArgs> DownladCompleted;

        public void DownloadAsync()
        {
            _workerThread = new Thread(
                () =>
                {
                    Download();
                    DownladCompleted(this, new DownloadCompletedEventArgs());
                });
            _workerThread.Start();
        }

        public void DownloadCancel()
        {
            if (_workerThread != null && _workerThread.ThreadState == ThreadState.Running) {
                _workerThread.Abort();
            }
        }

        public override string ToString()
        {
            return String.Format("{0}:{1}/{2}", Name, HostName, BoardDir);
        }

        /// <summary>
        /// スレ一覧をダウンロードする
        /// </summary>
        public void Download()
        {
            Threads.Clear();
            var content = "";
            var subjectAddress = "";
            if (GlobalConf.UseBg20 && HostName.EndsWith(".2ch.net")) {
                subjectAddress = String.Format("http://bg20.2ch.net/test/p.so/{0}/{1}/", HostName, BoardDir);
            }
            else {
                subjectAddress = String.Format("http://{0}/{1}/subject.txt", HostName, BoardDir);
            }

            var sp = new Stopwatch();
            sp.Start();
            using (var client = new WebClient()) {
                client.Headers.Add(HttpRequestHeader.UserAgent, GlobalConf.UserAgent);
                content = client.DownloadString(subjectAddress);
            }
            sp.Stop();
            Debug.WriteLine("subject.txt loaded: taken " + sp.ElapsedMilliseconds / 1000.0 + "sec.");
            
            var lines = content.Split('\n');
            foreach (var line in lines) {
                var match = Regex.Match(line, "(?<epoch>\\d+)\\.dat<>(?<name>.+)\\((?<resnum>\\d+)\\)", RegexOptions.Compiled);
                if (!match.Success) continue;
                var thread = new BbsThread
                                 {
                                     Epoch = long.Parse(match.Groups["epoch"].Value),
                                     Name = match.Groups["name"].Value,
                                     ResNum = int.Parse(match.Groups["resnum"].Value)
                                 };

                Threads.Add(thread);
            }
        }
    }
}