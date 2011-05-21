using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using System.Web;

namespace The2chViewer
{
    /// <summary>
    /// 2chのスレッドを表すクラス
    /// </summary>
    public class BbsThread
    {
        public static string CacheDir;
        /// <summary>
        /// スレの名前
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// スレが作成された時刻のエポック秒
        /// </summary>
        public long Epoch { get; set; }
        /// <summary>
        /// レスの数
        /// </summary>
        public int ResNum { get; set; }
        /// <summary>
        /// スレの勢い
        /// </summary>
        public double Speed
        {
            get
            {
                return ResNum / Age;
            }
        }
        /// <summary>
        /// スレが立ってからの経過日数
        /// </summary>
        public double Age
        {
            get
            {
                var start = new DateTime(1970, 1, 1).ToLocalTime();
                start = start.AddSeconds(Epoch);
                return (DateTime.Now - start).TotalDays;
            }
        }
        /// <summary>
        /// レスのリスト
        /// </summary>
        public List<BbsResponse> Responses { get; set; }
        private Thread _workerThread;
        public event Action<object, DownloadCompletedEventArgs> DownladCompleted;

        public override string ToString()
        {
            return String.Format("{0}({1}):{2}", Name, ResNum, Epoch);
        }

        public BbsThread()
        {
            Responses = new List<BbsResponse>();
        }

        /// <summary>
        /// 全てのレスをダウンロードする（差分取得未実装）
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="boardDir"></param>
        public void Download(string hostName, string boardDir)
        {
            Responses.Clear();
            var content = "";
            var datAddress = "";
            if (GlobalConf.UseBg20 && hostName.EndsWith(".2ch.net")) {
                datAddress = String.Format("http://bg20.2ch.net/test/r.so/{0}/{1}/{2}/", hostName, boardDir, Epoch);
            }
            else {
                datAddress = String.Format("http://{0}/{1}/dat/{2}.dat", hostName, boardDir, Epoch);
            }

            using (var client = new WebClient()) {
                client.Headers.Add(HttpRequestHeader.UserAgent, GlobalConf.UserAgent);
                content = client.DownloadString(datAddress);
            }

            var lines = content.Split('\n');
            foreach(var line in lines) {
                if (line == "") {
                    continue;
                }
                var entries = line.Split(new [] {"<>"}, StringSplitOptions.None);
                if (entries.Count() < 5) {
                    throw new Exception("dat file format error.");
                }

                var response = new BbsResponse();
                response.Author = entries[0];
                response.Mail = entries[1];

                var match = Regex.Match(entries[2], @"(?<date>\d\d\d\d/\d\d/\d\d\([月火水木金土日]\)\s\d\d:\d\d:\d\d(\.\d\d)?)?(\sID:(?<id>[a-zA-Z0-9+/]+))?(\sBE:(?<beid>.+))?");
                DateTime.TryParse(match.Groups["date"].Value, out response.date);
                response.Id = match.Groups["id"].Value;
                response.BeId = match.Groups["beid"].Value;

                var body = entries[3];
                body = Regex.Replace(body, " <br> ", "\r\n");
                body = Regex.Replace(body, "<a.+?>(?<name>.+?)</a>", "${name}");
                body = HttpUtility.HtmlDecode(body);
                response.Body = body;
                Responses.Add(response);
            }
        }

        /// <summary>
        /// スレをhtml化する
        /// </summary>
        /// <returns>xml化されたスレのデータ。ToStringするとWebBrowser.DocumentTextに流せる</returns>
        public XDocument ToXml()
        {
            var responses = new XElement("dl");
            responses.SetAttributeValue("class", "thread");
            for (var i = 0; i < Responses.Count; i++) {
                XElement name;
                if (Responses[i].Mail != "") {
                    name = new XElement("a");
                    name.SetAttributeValue("href", "mailto:" + Responses[i].Mail);
                }
                else {
                    name = new XElement("font");
                    name.SetAttributeValue("color", "green");
                }

                name.Add(new XElement("b", Responses[i].Author));
                var anchor = new XElement("a");
                anchor.SetAttributeValue("name", i + 1);
                responses.Add(new XElement("dt", anchor, String.Format("{0} ：", i + 1), name));
                responses.Add("：", Responses[i].date.ToString("yyyy/MM/dd(ddd) HH:mm:ss"));
                if (Responses[i].Id != "") {
                    responses.Add(" ID:" + Responses[i].Id);
                }
                var content = new XElement("dd");
                
                foreach (var line in Responses[i].Body.Split(new [] {"\r\n"}, StringSplitOptions.None)) {
                    content.Add(line);
                    content.Add(new XElement("br"));
                }
                responses.Add(content);
            }

            var doc = new XDocument(new XDeclaration("1.0", "Shift-JIS", "yes"), new XComment("Generated by lib2ch"));

            var title = new XElement("h1", Name);
            title.SetAttributeValue("style", "color:red;font-size:larger;font-weight:normal;margin:-.5em 0 0;");
            var head = new XElement("head", new XElement("title", Name));
            var body = new XElement("body", title, responses);
            body.SetAttributeValue("bgcolor", "#efefef");
            body.SetAttributeValue("text", "black");
            body.SetAttributeValue("link", "blue");
            body.SetAttributeValue("alink", "red");
            body.SetAttributeValue("vlink", "#660099");

            doc.Add(new XElement("html", head, body));

            return doc;
        }

        public void DownloadAsync(string serverName, string boardDir)
        {
            _workerThread = new Thread(
                () =>
                {
                    Download(serverName, boardDir);
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
    }

    /// <summary>
    /// 2chのレスを表す構造体
    /// </summary>
    public struct BbsResponse
    {
        /// <summary>
        /// 名前
        /// </summary>
        public string Author;
        /// <summary>
        /// メール
        /// </summary>
        public string Mail;
        /// <summary>
        /// 本文
        /// </summary>
        public string Body;
        /// <summary>
        /// ID
        /// </summary>
        public string Id;
        /// <summary>
        /// 投稿日付
        /// </summary>
        public DateTime date;
        /// <summary>
        /// BE-ID
        /// </summary>
        public string BeId;
    }
}
