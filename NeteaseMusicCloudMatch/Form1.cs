using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using NeteaseCloudMusicApi;

namespace NeteaseMusicCloudMatch
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public static string unikey = string.Empty, userId = string.Empty, nickName = string.Empty;
        static CloudMusicApi cloudMusicApi;
        static bool isQrChecking = false;

        private async void Form1_Load(object sender, EventArgs e)
        {
            string LoginCheck = CommonHelper.Read("NeteaseMusic", "LoginCheck");
            if (!string.IsNullOrWhiteSpace(LoginCheck))
            {
                checkBox1.Checked = Convert.ToBoolean(LoginCheck);
            }

            if (checkBox1.Checked)
            {
                string cookie = CommonHelper.Read("NeteaseMusic", "Cookie");
                if (!string.IsNullOrEmpty(cookie))
                {
                    cloudMusicApi = new CloudMusicApi(cookie);
                    await LoadUIDName();
                    await LoadCloudInfo();
                    button2_Click(sender, null);
                    timer1.Enabled = false;
                }
                else
                {
                    cloudMusicApi = new CloudMusicApi();
                    await LoadQrCodeImage();
                }
            }
            else
            {
                cloudMusicApi = new CloudMusicApi();
                await LoadQrCodeImage();
            }

            LoadDgvColumns();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            CommonHelper.Write("NeteaseMusic", "LoginCheck", checkBox1.Checked.ToString());
        }

        #region dataGridView1 加载标题

        private void LoadDgvColumns()
        {
            dataGridView1.RowHeadersVisible = false;
            DataGridViewTextBoxColumn colListId = new DataGridViewTextBoxColumn();
            colListId.Name = "colListId";
            //colListId.Width = 40;
            colListId.HeaderText = "#";
            colListId.ReadOnly = true;

            DataGridViewTextBoxColumn colSongId = new DataGridViewTextBoxColumn();
            colSongId.Name = "colSongId";
            //colSongId.Width = 80;
            colSongId.HeaderText = "ID";
            colSongId.ReadOnly = true;

            DataGridViewTextBoxColumn colFileName = new DataGridViewTextBoxColumn();
            colFileName.Name = "colFileName";
            //colFileName.Width = 200;
            colFileName.HeaderText = "文件名称";
            colFileName.ReadOnly = true;

            DataGridViewTextBoxColumn colFileSize = new DataGridViewTextBoxColumn();
            colFileSize.Name = "colFileSize";
            //colFileSize.Width = 68;
            colFileSize.HeaderText = "大小";
            colFileSize.ReadOnly = true;

            DataGridViewTextBoxColumn colAddTime = new DataGridViewTextBoxColumn();
            colAddTime.Name = "colAddTime";
            //colAddTime.Width = 130;
            colAddTime.HeaderText = "上传时间";
            colAddTime.ReadOnly = true;

            dataGridView1.Columns.AddRange(
                new DataGridViewColumn[]
                {
                    colListId, colSongId, colFileName, colFileSize, colAddTime
                });

            dataGridView1.Columns[0].FillWeight = 5;
            dataGridView1.Columns[1].FillWeight = 15;
            dataGridView1.Columns[2].FillWeight = 30;
            dataGridView1.Columns[3].FillWeight = 10;
            dataGridView1.Columns[4].FillWeight = 20;
        }

        #endregion

        #region 加载二维码图片

        private void LoadQrCodeImageLegacy()
        {
            try
            {
                string apiUrl = "https://music.163.com/api/login/qrcode/unikey?type=1";
                string html = CommonHelper.GetHtml(apiUrl);
                if (CommonHelper.CheckJson(html))
                {
                    var json = JObject.Parse(html);
                    if (json["code"]?.ToString() == "200")
                    {
                        unikey = json["unikey"]?.ToString();
                        string QrCodeUrl = "https://music.163.com/login?codekey=" + unikey;
                        pictureBox1.Image = CommonHelper.QrCodeCreate(QrCodeUrl);
                    }
                    else
                    {
                        MessageBox.Show("生成二维码unikey出错", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show(html, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }

        private async Task LoadQrCodeImage()
        {
            try
            {
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.LoginQrKey);
                if (CloudMusicApi.IsSuccess(result))
                {
                    unikey = result["unikey"]?.ToString();
                    string qrCodeUrl = "https://music.163.com/login?codekey=" + unikey;
                    pictureBox1.Image = CommonHelper.QrCodeCreate(qrCodeUrl);
                }
                else
                {
                    MessageBox.Show($"生成二维码unikey出错{result}", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region 加载UID和Name

        private async Task LoadUIDName()
        {
            try
            {
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.LoginStatus, throwIfFailed: false);
                if (CloudMusicApi.IsSuccess(result))
                {
                    var profile = result?["profile"];
                    userId = profile?["userId"]?.ToString() ?? string.Empty;
                    nickName = profile?["nickname"]?.ToString() ?? string.Empty;
                    Console.WriteLine($"账号ID： {userId}");
                    Console.WriteLine($"账号昵称： {nickName}");
                    string avatarUrl = profile?["avatarUrl"]?.ToString() ?? string.Empty;
                    label1.Text = "UID：" + userId + "，Name：" + nickName;
                    pictureBox1.Image = CommonHelper.GetImage(avatarUrl);
                }
                else
                {
                    string msg = $"请求{nameof(CloudMusicApiProviders.LoginStatus)}失败:{result}";
                    Console.WriteLine(msg);
                    label1.Text = "登录状态获取失败，但可继续尝试读取音乐网盘";
                }
            }
            catch (Exception ex)
            {
                string msg = $"请求{nameof(CloudMusicApiProviders.LoginStatus)}错误:{ex}";
                Console.WriteLine(msg);
                label1.Text = "登录状态获取异常，但可继续尝试读取音乐网盘";
            }
        }

        #endregion

        #region 加载音乐网盘信息

        private async Task LoadCloudInfo()
        {
            try
            {
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.UserCloud,
                    new Dictionary<string, object> {{"limit", 0}}, false);
                if (CloudMusicApi.IsSuccess(result))
                {
                    string size = result["size"]?.ToString();
                    string maxSize = result["maxSize"]?.ToString();
                    size = CommonHelper.GetFileSize(Convert.ToInt64(size));
                    maxSize = CommonHelper.GetFileSize(Convert.ToInt64(maxSize));
                    label2.Text = "音乐云盘容量：" + size + "  /  " + maxSize;
                }
                else
                {
                    string msg = $"请求{nameof(CloudMusicApiProviders.UserCloud)}失败:{result}";
                    Console.WriteLine(msg);
                    MessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"请求{nameof(CloudMusicApiProviders.UserCloud)}错误:{ex}";
                Console.WriteLine(msg);
                MessageBox.Show(msg);
            }
        }

        #endregion

        #region 检测扫码状态 / 重新扫码登录

        private async void timer1_Tick(object sender, EventArgs e)
        {
            if (isQrChecking) return;
            isQrChecking = true;
            try
            {
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.LoginQrCheck,
                    new Dictionary<string, object>() {{"key", unikey}}, false);
                isQrChecking = false;
                string code = result["code"]?.ToString();
                string message = result["message"]?.ToString();
                if (code == "800")
                {
                    await LoadQrCodeImage();
                }
                else if (code == "803")
                {
                    //wyCookie = result.Cookie.Replace(",", ";");
                    //CommonHelper.Write("NeteaseMusic", "Cookie", wyCookie);
                    string cookie = cloudMusicApi.ToCookieHeader();
                    CommonHelper.Write("NeteaseMusic", "Cookie", cookie);
                    await LoadUIDName();
                    await LoadCloudInfo();
                    button2_Click(sender, null);

                    timer1.Enabled = false;
                }

                string messStr = code + ", " + message;
                Console.WriteLine(messStr);
            }
            catch (Exception exception)
            {
                isQrChecking = false;
                Console.WriteLine(exception);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("确定要重新扫码登录吗？", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                //wyCookie = string.Empty;
                label1.Text = string.Empty;
                label2.Text = string.Empty;

                await LoadQrCodeImage();

                timer1.Enabled = true;
            }
        }

        #endregion

        #region 读取音乐网盘内容

        private async void button2_Click(object sender, EventArgs e)
        {
            pageIndex = 1;
            await GetCloudData();
        }

        int pageIndex = 1;

        private async Task GetCloudData()
        {
            try
            {
                if (pageIndex <= 1)
                {
                    dataGridView1.Rows.Clear();
                }

                int limit = 200;
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.UserCloud,
                    new Dictionary<string, object>() {{"limit", limit}, {"offset", (pageIndex - 1) * limit}}, false);
                if (CloudMusicApi.IsSuccess(result))
                {
                    if (result["count"]?.Value<int>() > 0)
                    {
                        if (!result.ContainsKey("data") || result["data"] == null) return;
                        var jarr = JArray.Parse(result["data"]?.ToString());
                        for (int i = 0; i < jarr.Count; i++)
                        {
                            var j = JObject.Parse(jarr[i].ToString());
                            string songId = j["songId"]?.ToString();
                            string fileName = j["fileName"]?.ToString();
                            string fileSize = j["fileSize"]?.ToString();
                            string addTime = j["addTime"]?.ToString();
                            int index = 0;
                            //this.Invoke(new MethodInvoker(delegate ()
                            {
                                index = dataGridView1.Rows.Add();
                                dataGridView1.Rows[index].Cells[0].Value = dataGridView1.Rows.Count;
                                dataGridView1.Rows[index].Cells[1].Value = songId;
                                dataGridView1.Rows[index].Cells[2].Value = fileName;
                                dataGridView1.Rows[index].Cells[3].Value = CommonHelper.GetFileSize(Convert.ToInt64(fileSize));
                                dataGridView1.Rows[index].Cells[4].Value = CommonHelper.UnixTimestampToDateTime(addTime);
                            }//));
                            await Task.Yield();
                        }
                    }
                }
                else
                {
                    string msg = $"请求{nameof(CloudMusicApiProviders.UserCloud)}失败:{result}";
                    Console.WriteLine(msg);
                    MessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"请求{nameof(CloudMusicApiProviders.UserCloud)}错误:{ex}";
                Console.WriteLine(msg);
                MessageBox.Show(msg);
            }
        }


        private async void ScrollReader(object sender, ScrollEventArgs e)
        {
            if (e.NewValue + dataGridView1.DisplayedRowCount(false) >= dataGridView1.RowCount)
            {
                pageIndex++;
                await GetCloudData();
            }
        }

        #endregion

        #region 音乐云盘搜索
        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                string searchValue = textBox3.Text.Trim();
                if (string.IsNullOrWhiteSpace(searchValue))
                {
                    MessageBox.Show("请先输入要搜索的内容", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                DataTable dataTable = DataGridViewToDataTable(dataGridView1);
                DataRow[] dataRows = dataTable.Select("文件名称 like '%" + searchValue + "%'");
                if (dataRows.Length > 0)
                {
                    foreach (var dataRow in dataRows)
                    {
                        dataGridView1.Rows.Clear();
                        int index = dataGridView1.Rows.Add();
                        dataGridView1.Rows[index].Cells[0].Value = dataRow[0].ToString();
                        dataGridView1.Rows[index].Cells[1].Value = dataRow[1].ToString();
                        dataGridView1.Rows[index].Cells[2].Value = dataRow[2].ToString();
                        dataGridView1.Rows[index].Cells[3].Value = dataRow[3].ToString();
                        dataGridView1.Rows[index].Cells[4].Value = dataRow[4].ToString();
                    }
                }
                else
                {
                    MessageBox.Show("未找到关于“" + searchValue + "”的内容", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region DataGridView转DataTable
        private DataTable DataGridViewToDataTable(DataGridView dgv)
        {
            DataTable dt = new DataTable();
            try
            {
                // 循环列标题名称，处理了隐藏的行不显示
                for (int count = 0; count < dgv.Columns.Count; count++)
                {
                    if (dgv.Columns[count].Visible == true)
                    {
                        dt.Columns.Add(dgv.Columns[count].HeaderText.ToString());
                    }
                }

                // 循环行，处理了隐藏的行不显示
                for (int count = 0; count < dgv.Rows.Count; count++)
                {
                    DataRow dr = dt.NewRow();
                    int curr = 0;
                    for (int countsub = 0; countsub < dgv.Columns.Count; countsub++)
                    {
                        if (dgv.Columns[countsub].Visible == true)
                        {
                            if (dgv.Rows[count].Cells[countsub].Value != null)
                            {
                                dr[curr] = dgv.Rows[count].Cells[countsub].Value.ToString();
                            }
                            else
                            {
                                dr[curr] = "";
                            }
                            curr++;
                        }
                    }
                    dt.Rows.Add(dr);
                }
                return dt;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
                return dt;
            }
        }
        #endregion

        #region dataGridView1 选中赋值给sid
        private void dataGridView1_Click(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.Rows.Count > 0)
                {
                    string sid = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
                    textBox1.Text = sid;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region 判断云盘文件是否存在
        private async Task<bool> CheckCloudFileStatus(string songId)
        {
            try
            {
                var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.UserCloudDetail,
                    new Dictionary<string, object> {{"limit", 0},{"id", songId}}, false);
                if (CloudMusicApi.IsSuccess(result))
                {
                    if (!result.ContainsKey("data") || result["data"] == null) return false;
                    var jarr = JArray.Parse(result["data"]?.ToString());
                    if (jarr.Count > 0)
                    {
                        return true;
                    }
                }
                else
                {
                    string msg = $"请求{nameof(CloudMusicApiProviders.UserCloudDetail)}失败:{result}";
                    Console.WriteLine(msg);
                    MessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                string msg = $"请求{nameof(CloudMusicApiProviders.UserCloudDetail)}错误:{ex}";
                Console.WriteLine(msg);
                MessageBox.Show(msg);
            }
            return false;
        }
        #endregion

        #region 判断歌曲是否存在
        private async Task<int> CheckSongStatus(string songId)
        {
            try
            {
                if (songId == "0")
                {
                    if (MessageBox.Show("确定要取消匹配吗？", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else
                {
                    var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.SongDetail,
                        new Dictionary<string, object> {{"ids", songId}}, false);
                    if (CloudMusicApi.IsSuccess(result))
                    {
                        var jarr = JArray.Parse(result["songs"]?.ToString());
                        if (jarr.Count > 0)
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        string msg = $"请求{nameof(CloudMusicApiProviders.SongDetail)}失败:{result}";
                        Console.WriteLine(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = $"请求{nameof(CloudMusicApiProviders.SongDetail)}错误:{ex}";
                Console.WriteLine(msg);
            }
            return 0;
        }
        #endregion

        #region 根据URL正则匹配歌曲ID
        private string GetUrlMatchId(string url)
        {
            string item = string.Empty, id = string.Empty;
            if (url.Contains("/m/"))
            {
                var match = Regex.Match(url, "m/(\\w+)\\?[\\s\\S]*?&id=(\\d+)");
                if (match.Success)
                {
                    item = match.Groups[1].Value;
                    id = match.Groups[2].Value;
                }
                match = Regex.Match(url, "/(\\w+)\\?id=(\\d+)");
                if (match.Success)
                {
                    item = match.Groups[1].Value;
                    id = match.Groups[2].Value;
                }
            }
            else
            {
                var match = Regex.Match(url, "/(\\w+)\\?id=(\\d+)");
                if (match.Success)
                {
                    item = match.Groups[1].Value;
                    id = match.Groups[2].Value;
                }
                match = Regex.Match(url, "com/(\\w+)/(\\w+)");
                if (match.Success)
                {
                    item = match.Groups[1].Value;
                    id = match.Groups[2].Value;
                }
            }

            if (item.Contains("toplist"))
            {
                MessageBox.Show("这是排行榜链接，请输入单曲链接");
            }
            else if (item.Contains("playlist"))
            {
                MessageBox.Show("这是歌单链接，请输入单曲链接");
            }
            else if (item.Contains("album"))
            {
                MessageBox.Show("这是专辑链接，请输入单曲链接");
            }
            else if (item.Contains("song"))
            {
                return id;
            }
            return string.Empty;
        }
        #endregion

        #region 匹配纠正
        private async void button3_Click(object sender, EventArgs e)
        {
            try
            {
                string uid = userId;
                string sid = textBox1.Text.Trim();
                string asid = textBox2.Text.Trim();

                if (string.IsNullOrEmpty(sid))
                {
                    MessageBox.Show("请选择云盘文件", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (string.IsNullOrEmpty(asid))
                {
                    MessageBox.Show("请输入歌曲ID", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (sid.Equals(asid))
                {
                    MessageBox.Show("已经匹配成功，无需再次匹配。", "", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }
                else if (asid.StartsWith("http"))
                {
                    asid = GetUrlMatchId(asid);
                    if (string.IsNullOrWhiteSpace(asid))
                    {
                        return;
                    }
                }

                if (await CheckCloudFileStatus(sid))
                {
                    int songStatus = await CheckSongStatus(asid);
                    if (songStatus == 1)
                    {
                        var result = await cloudMusicApi.RequestAsync(CloudMusicApiProviders.UserCloudMatch,
                            new Dictionary<string, object> {{"userId", uid},{"songId", sid},{"adjustSongId", asid}}, false);
                        if (CloudMusicApi.IsSuccess(result))
                        {
                            MessageBox.Show("匹配纠正成功！");

                            button2_Click(sender, null);
                        }
                        else
                        {
                            string msg = $"请求{nameof(CloudMusicApiProviders.UserCloudMatch)}失败:{result}";
                            Console.WriteLine(msg);
                            MessageBox.Show(msg, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else if (songStatus == 0)
                    {
                        MessageBox.Show("输入的歌曲ID不存在", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("云盘文件不存在", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region 版权信息
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            /*string url = "https://www.52pojie.cn/home.php?mod=space&uid=381706";
            try
            {
                //调用默认浏览器
                System.Diagnostics.Process.Start(url);

                ////从注册表中读取默认浏览器可执行文件路径  
                //RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command\");
                //string s = key.GetValue("").ToString();
                ////s就是你的默认浏览器，不过后面带了参数，把它截去，不过需要注意的是：不同的浏览器后面的参数不一样！  
                ////"D:\Program Files (x86)\Google\Chrome\Application\chrome.exe" -- "%1"  
                //System.Diagnostics.Process.Start(s.Substring(0, s.Length - 8), url);
            }
            catch (Exception)
            {
                //调用IE浏览器
                System.Diagnostics.Process.Start("iexplore.exe", url);
            }*/
        }
        #endregion

    }
}
