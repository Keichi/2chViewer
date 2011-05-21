using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace The2chViewer
{
    public partial class ImageFinder : Form
    {
        private BbsMenu _menu;
        private BbsBoard _board;
        private readonly Dictionary<string, string> _contentTypes;

        public ImageFinder()
        {
            InitializeComponent();

            _menu = new BbsMenu();
            GlobalConf.UseBg20 = true;
            _contentTypes = new Dictionary<string, string>
                                {
                                    {"image/jpeg", "jpg"},
                                    {"image/png", "png"},
                                    {"image/gif", "gif"},
                                    {"audio/mpg", "mpg"},
                                    {"video/mpg", "mpg"},
                                    {"text/plain", "txt"}
                                };
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            txtLog.Text += "Menu download start\r\n";
            _menu.Download();
            txtLog.Text += "Menu download finish\r\n";

            _board = _menu.Boards["雑談系２"].Where(x => x.Name == "ニュー速VIP").First();

            txtLog.Text += "Board download start\r\n";
            _board.Download();
            txtLog.Text += "Board download start\r\n";

            _board.Threads.Sort((x, y) => y.Speed.CompareTo(x.Speed));

            foreach (var thread in _board.Threads) {
                var dirname = thread.Name
                    .Where(x => !Path.GetInvalidFileNameChars().Contains(x))
                    .Select(x => x.ToString())
                    .Aggregate((x, y) => x + y);
                if (Directory.Exists(dirname)) continue;

                txtLog.Text += "Thread " + thread.Name + " download start\r\n";
                try {
                    thread.Download(_board.HostName, _board.BoardDir);
                }
                catch(Exception) {
                    continue;
                }
                txtLog.Text += "Thread " + thread.Name + " download finish\r\n";
                
                var matches = Regex.Matches(thread.ToXml().ToString(), "ttps?://[-_.!~*'()a-zA-Z0-9;/?:@&=+$,%# ]+");
                var links = new List<string>();
                foreach (Match match in matches) {
                    var s = "h" + match.Value;
                    if (Regex.IsMatch(s, @"http://imepita.jp/\d+/\d+")) {
                        s = s.Replace("http://imepita.jp/", "http://imepita.jp/image/");
                    }
                    txtLog.Text += "\t" + s + "\r\n";
                    links.Add(s);
                }

                links = links.Distinct().ToList();

                
                Directory.CreateDirectory(dirname);
                using (var stream = new StreamWriter(Path.Combine(dirname, "index.html"), false, Encoding.GetEncoding(932))) {
                    thread.ToXml().Save(stream);
                }
                File.WriteAllLines(Path.Combine(dirname, "links.lst"), links);

                Parallel.ForEach(links, link =>
                {
                    try {
                        var req = (HttpWebRequest)HttpWebRequest.Create(link);
                        req.Timeout = 5000;
                        var resp = req.GetResponse();
                        var type = resp.Headers["Content-Type"];

                        if (_contentTypes.Keys.Any(type.Contains)) {
                            var ext = _contentTypes[_contentTypes.Keys.Where(type.Contains).First()];
                            var s = Path.GetRandomFileName();
                            s = Path.ChangeExtension(s, ext);
                            using (var fs = new FileStream(Path.Combine(dirname, s), FileMode.CreateNew)) {
                                resp.GetResponseStream().CopyTo(fs);
                            }
                        }
                    }
                    catch (Exception ex) {

                    }
                }
                );
            }
        }
    }
}
