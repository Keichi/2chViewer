using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;

namespace The2chViewer
{
    public partial class BbsBrowser : Form
    {
        private BbsMenu _menu;
        private BbsBoard _currentBoard;
        private int _selectedThreadIndex;

        public BbsBrowser()
        {
            InitializeComponent();

            lvwBoard.Columns[0].Width = 200;
            lvwBoard.Columns[1].Width = 200;
            lvwBoard.Columns[2].Width = 200;

            lvwBoard.FullRowSelect = true;
            lvwBoard.MultiSelect = false;
        }

        private void btnBoardsLoad_Click(object sender, EventArgs e)
        {
            _menu = new BbsMenu();
            _menu.Download();

            tvwMenu.Nodes.Clear();
            tvwMenu.BeginUpdate();
            foreach (var category in _menu.Boards) {
                var nodes = new List<TreeNode>();
                foreach (var board in category.Value) {
                    nodes.Add(new TreeNode(board.Name));
                }
                var catnode = new TreeNode(category.Key, nodes.ToArray());

                tvwMenu.Nodes.Add(catnode);
            }
            tvwMenu.EndUpdate();
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (tvwMenu.SelectedNode.Parent == null) {
                return;
            }

            toolStripStatusLabel1.Text = String.Format("Loading {0} - {1}", tvwMenu.SelectedNode.Parent.Text,
                                                       tvwMenu.SelectedNode.Text);
            var category = _menu.Boards[tvwMenu.SelectedNode.Parent.Text];
            _currentBoard = category.Where(x => x.Name == tvwMenu.SelectedNode.Text).First();

            _currentBoard.DownladCompleted += displayBoard;

            _currentBoard.DownloadCancel();
            _currentBoard.DownloadAsync();
        }

        private void displayBoard(object sender, DownloadCompletedEventArgs args)
        {
            var list = new List<ListViewItem>();

            _currentBoard.Threads.Sort((x, y) => y.Speed.CompareTo(x.Speed));
            foreach (var thread in _currentBoard.Threads) {
                var item =
                    new ListViewItem(new[] { thread.Name, thread.Speed.ToString(), thread.ResNum.ToString() });
                if (thread.ResNum == 1001) {
                    item.BackColor = Color.Red;
                }
                list.Add(item);
            }

            Invoke(new Action(lvwBoard.Items.Clear));
            Invoke(new Action(() => lvwBoard.Items.AddRange(list.ToArray())));
        }

        private void lvwBoard_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvwBoard.SelectedIndices.Count < 1) return;
            _selectedThreadIndex = lvwBoard.SelectedIndices[0];

            if (_selectedThreadIndex >= 0) {
                _currentBoard.Threads[_selectedThreadIndex].DownloadCancel();
                _currentBoard.Threads[_selectedThreadIndex].DownladCompleted += displayThread;
                _currentBoard.Threads[_selectedThreadIndex].DownloadAsync(_currentBoard.HostName, _currentBoard.BoardDir);
            }
        }

        private void displayThread(object sender, DownloadCompletedEventArgs args)
        {
            Invoke(new Action(() => wbThread.DocumentText = _currentBoard.Threads[_selectedThreadIndex].ToXml().ToString()));
            Invoke(new Action(() => wbThread.DocumentCompleted += wbScroll));
        }

        private void wbScroll(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            Invoke(new Action(() => wbThread.Document.Body.ScrollTop = wbThread.Document.Body.ScrollRectangle.Height));
        }
    }
}
