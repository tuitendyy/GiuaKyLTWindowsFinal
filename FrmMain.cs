using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

namespace UI_Design
{
    public partial class FrmMain : Form
    {
        
        private const int TimeLimitSeconds = 90;     
        private const int PreviewMs = 2000;          
        private const int FlipBackDelayMs = 800;     
        private const int ScoreMatch = 10;           
        private const int ScoreMismatch = -1;        
        private const int BonusPerSecondLeft = 2;    

        private const string ImgFolder = "img";
        private const string SoundFolder = "bg";
        private const string BackImageBaseName = "no-image";  
        private const string BgMusicName = "bg.wav";          
        private const string WinSoundName = "win.wav";        
        private const string LoseSoundName = "lose.wav";      
        private const string ScoreFileName = "thanhtichs.txt";

        
        private const int CardRadius = 14;
        private static readonly Color ColDark = Color.FromArgb(20, 58, 48);        
        private static readonly Color ColBoard = Color.FromArgb(234, 246, 240);    
        private static readonly Color ColAccent = Color.FromArgb(38, 190, 130);
        private static readonly Color ColAccentHover = Color.FromArgb(30, 160, 110);
        private static readonly Color ColAccentDown = Color.FromArgb(22, 130, 90);
        private static readonly Color ColBackA = Color.FromArgb(52, 211, 153);     // mặt úp (trên)
        private static readonly Color ColBackB = Color.FromArgb(16, 135, 105);     // mặt úp (dưới)
        private static readonly Color ColWarn = Color.FromArgb(255, 193, 7);
        private static readonly Color ColDanger = Color.FromArgb(255, 107, 107);
        private static readonly Color ColMuted = Color.FromArgb(90, 130, 115);

        
        private readonly Random rnd = new Random();
        private readonly List<PictureBox> cards = new List<PictureBox>();
        private readonly HashSet<PictureBox> matched = new HashSet<PictureBox>();
        private readonly List<Image> allFaces = new List<Image>();   
        private List<Image> roundFaces = new List<Image>();          
        private readonly Timer tmrFlipBack = new Timer();
        private readonly Timer tmrCountdown = new Timer();
        private readonly Timer tmrPreview = new Timer();

        private Panel pnlStart;
        private Label lblTitle, lblSub, lblHint, lblThoiGian;
        private RoundedButton btnStart;
        private Font markFont;

        private Image backImage;
        private SoundPlayer bgPlayer, winPlayer, losePlayer;

        private PictureBox first, second;   
        private PictureBox hoverCard;       
        private bool isBusy;                
        private bool gameRunning;
        private bool soundOn = true;
        private bool resourcesOk;
        private int pairCount;
        private int score, turns, timeLeft;

        public FrmMain()
        {
            InitializeComponent();
            BuildModernUi();
            SetupEvents();
        }

      
        private void BuildModernUi()
        {
            Font = new Font("Segoe UI", 10F);
            BackColor = ColBoard;
            DoubleBuffered = true;
            MinimumSize = new Size(720, 600);
            StartPosition = FormStartPosition.CenterScreen;

            
            var mnuMoi = new ToolStripMenuItem("Ván Mới");
            mnuMoi.Click += (s, e) => RestartRequested();
            menuStripThanhCongCu.Items.Insert(0, mnuMoi);

            menuStripThanhCongCu.Renderer = new ToolStripProfessionalRenderer(new ModernColors()) { RoundedEdges = false };
            menuStripThanhCongCu.BackColor = ColDark;
            menuStripThanhCongCu.ForeColor = Color.White;
            menuStripThanhCongCu.Font = new Font("Segoe UI", 10.5F);
            menuStripThanhCongCu.Padding = new Padding(10, 4, 0, 4);
            foreach (ToolStripItem item in menuStripThanhCongCu.Items)
                item.ForeColor = Color.White;

            panelBangThongTin.BackColor = ColDark;
            panelBangThongTin.Height = 64;

            lblThoiGian = new Label { AutoSize = true, Text = "Thời gian: " + TimeLimitSeconds + "s" };
            panelBangThongTin.Controls.Add(lblThoiGian);
            foreach (var lbl in new[] { lblDiemSo, lblSoLuot, lblThoiGian })
            {
                lbl.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
                lbl.ForeColor = Color.White;
            }
            panelBangThongTin.Resize += (s, e) => LayoutInfoBar();

           
            tblpBang.BackColor = ColBoard;
            tblpBang.Padding = new Padding(14);
            markFont = new Font("Segoe UI", 28F, FontStyle.Bold);

          
            pnlStart = new Panel { Dock = DockStyle.Fill, BackColor = ColBoard };

            lblTitle = new Label
            {
                Text = "TRÚC XANH",
                AutoSize = true,
                Font = new Font("Segoe UI", 44F, FontStyle.Bold),
                ForeColor = ColDark,
                BackColor = ColBoard
            };
            lblSub = new Label
            {
                Text = "Lật thẻ  •  Ghép cặp  •  Phá kỷ lục",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F),
                ForeColor = ColMuted,
                BackColor = ColBoard
            };
            btnStart = new RoundedButton
            {
                Text = "BẮT ĐẦU",
                Size = new Size(260, 64),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                NormalColor = ColAccent,
                HoverColor = ColAccentHover,
                PressedColor = ColAccentDown,
                Radius = 32
            };
            btnStart.Click += (s, e) => StartGame();
            lblHint = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 11F),
                ForeColor = ColMuted,
                BackColor = ColBoard
            };

            pnlStart.Controls.AddRange(new Control[] { lblTitle, lblSub, btnStart, lblHint });
            pnlStart.Resize += (s, e) => LayoutStartScreen();
            Controls.Add(pnlStart);
            pnlStart.BringToFront();
        }

        private void LayoutStartScreen()
        {
            if (pnlStart == null || lblHint == null) return;
            int gap1 = 6, gap2 = 36, gap3 = 26;
            int total = lblTitle.Height + gap1 + lblSub.Height + gap2 + btnStart.Height + gap3 + lblHint.Height;
            int y = Math.Max(10, (pnlStart.ClientSize.Height - total) / 2);

            Action<Control> centerX = c => c.Left = (pnlStart.ClientSize.Width - c.Width) / 2;

            lblTitle.Top = y; centerX(lblTitle); y += lblTitle.Height + gap1;
            lblSub.Top = y; centerX(lblSub); y += lblSub.Height + gap2;
            btnStart.Top = y; centerX(btnStart); y += btnStart.Height + gap3;
            lblHint.Top = y; centerX(lblHint);
        }

        private void LayoutInfoBar()
        {
            if (lblThoiGian == null) return;
            var labels = new[] { lblDiemSo, lblSoLuot, lblThoiGian };
            for (int i = 0; i < labels.Length; i++)
            {
                var c = labels[i];
                c.Left = (int)(panelBangThongTin.ClientSize.Width * (2 * i + 1) / 6.0 - c.Width / 2.0);
                c.Top = (panelBangThongTin.ClientSize.Height - c.Height) / 2;
            }
        }

  

        private void SetupEvents()
        {
           
            cards.AddRange(tblpBang.Controls.OfType<PictureBox>()
                .OrderBy(p => tblpBang.GetRow(p))
                .ThenBy(p => tblpBang.GetColumn(p)));
            pairCount = cards.Count / 2;

            foreach (var pb in cards)
            {
                pb.BorderStyle = BorderStyle.None;
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                pb.Dock = DockStyle.Fill;
                pb.Margin = new Padding(8);
                pb.BackColor = ColBackB;
                pb.Click += Card_Click;
                pb.Paint += Card_Paint;
                pb.MouseEnter += (s, e) => { hoverCard = (PictureBox)s; ((PictureBox)s).Invalidate(); };
                pb.MouseLeave += (s, e) => { hoverCard = null; ((PictureBox)s).Invalidate(); };
            }

            tmrFlipBack.Interval = FlipBackDelayMs;
            tmrFlipBack.Tick += TmrFlipBack_Tick;
            tmrCountdown.Interval = 1000;
            tmrCountdown.Tick += TmrCountdown_Tick;
            tmrPreview.Interval = Math.Max(1, PreviewMs);
            tmrPreview.Tick += TmrPreview_Tick;

            âmThanhBậtToolStripMenuItem.Click += (s, e) => ToggleSound();
            bảngThànhTíchToolStripMenuItem.Click += (s, e) => PauseDuringDialog(ShowLeaderboard);
            hướngDẫnChơiToolStripMenuItem.Click += (s, e) => PauseDuringDialog(ShowGuide);

            Load += (s, e) =>
            {
                LoadResources();
                UpdateLabels();
                ShowStartScreen(null);
            };
            FormClosed += (s, e) => CleanUp();
        }

        

        private void LoadResources()
        {
            var missing = new List<string>();
            string imgDir = Path.Combine(Application.StartupPath, ImgFolder);
            string soundDir = Path.Combine(Application.StartupPath, SoundFolder);

            
            try
            {
                if (!Directory.Exists(imgDir))
                {
                    missing.Add("Thư mục " + ImgFolder + "/");
                }
                else
                {
                    var exts = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                    var imageFiles = Directory.GetFiles(imgDir)
                        .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => f)
                        .ToList();

                    Func<string, bool> isBack = f => string.Equals(
                        Path.GetFileNameWithoutExtension(f), BackImageBaseName, StringComparison.OrdinalIgnoreCase);

                   
                    string backFile = imageFiles.FirstOrDefault(isBack);
                    if (backFile != null) backImage = LoadImageNoLock(backFile);

                    foreach (var f in imageFiles.Where(f => !isBack(f)))
                        allFaces.Add(LoadImageNoLock(f));
                }
            }
            catch (Exception ex)
            {
                missing.Add("Lỗi đọc ảnh: " + ex.Message);
            }

           
            bgPlayer = LoadSound(Path.Combine(soundDir, BgMusicName), missing);
            winPlayer = LoadSound(Path.Combine(soundDir, WinSoundName), missing);
            losePlayer = LoadSound(Path.Combine(soundDir, LoseSoundName), missing);

            resourcesOk = allFaces.Count >= pairCount;
            if (!resourcesOk)
                missing.Add("Cần ít nhất " + pairCount + " ảnh mặt thẻ trong thư mục " + ImgFolder + "/ (hiện có " + allFaces.Count + ")");

            if (missing.Count > 0)
            {
                MessageBox.Show(
                    "Thiếu hoặc không đọc được tài nguyên:\n- " + string.Join("\n- ", missing) +
                    "\n\nThư mục tìm kiếm: " + Application.StartupPath +
                    (resourcesOk ? "\nGame vẫn chạy nhưng thiếu một số hiệu ứng." : "\nKhông thể bắt đầu game."),
                    "Thiếu tài nguyên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        
        private static Image LoadImageNoLock(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var img = Image.FromStream(fs))
            {
                return new Bitmap(img);
            }
        }

        private static SoundPlayer LoadSound(string path, List<string> missing)
        {
            if (!File.Exists(path))
            {
                missing.Add(SoundFolder + "/" + Path.GetFileName(path));
                return null;
            }
            try
            {
                var p = new SoundPlayer(path);
                p.Load(); 
                return p;
            }
            catch (Exception)
            {
                missing.Add(SoundFolder + "/" + Path.GetFileName(path) + " (file .wav không hợp lệ)");
                return null;
            }
        }

      

        private void ShowStartScreen(string lastResult)
        {
            tmrFlipBack.Stop();
            tmrCountdown.Stop();
            tmrPreview.Stop();
            StopSound();
            gameRunning = false;
            isBusy = true;

            lblHint.Text = lastResult ?? ("Ghi nhớ vị trí các thẻ - bạn có " + TimeLimitSeconds +
                                          " giây để ghép hết " + pairCount + " cặp!");
            btnStart.Text = lastResult == null ? "BẮT ĐẦU" : "CHƠI LẠI";
            pnlStart.Visible = true;
            pnlStart.BringToFront();
            LayoutStartScreen();
        }

        private void StartGame()
        {
            if (!resourcesOk)
            {
                MessageBox.Show(
                    "Chưa đủ tài nguyên để bắt đầu.\nCần ít nhất " + pairCount + " ảnh mặt thẻ trong thư mục " +
                    ImgFolder + "/ (đặt cạnh file .exe).",
                    "Thiếu tài nguyên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            tmrFlipBack.Stop();
            tmrCountdown.Stop();
            tmrPreview.Stop();
            StopSound();

            first = second = null;
            matched.Clear();
            score = 0;
            turns = 0;
            timeLeft = TimeLimitSeconds;

       
            roundFaces = allFaces.OrderBy(x => rnd.Next()).Take(pairCount).ToList();
            var ids = new List<int>();
            for (int i = 0; i < pairCount; i++)
            {
                ids.Add(i);
                ids.Add(i);
            }
            Shuffle(ids);

            for (int k = 0; k < cards.Count; k++)
            {
                cards[k].Tag = ids[k];       
                cards[k].Cursor = Cursors.Hand;
                SetFace(cards[k], false);    
            }

            pnlStart.Visible = false;
            gameRunning = true;
            UpdateLabels();
            PlayBackground();

            if (PreviewMs > 0)
            {
               
                isBusy = true;
                foreach (var pb in cards) SetFace(pb, true);
                tmrPreview.Start();
            }
            else
            {
                isBusy = false;
                tmrCountdown.Start();
            }
        }

      
        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void TmrPreview_Tick(object sender, EventArgs e)
        {
            tmrPreview.Stop();
            foreach (var pb in cards) SetFace(pb, false);
            isBusy = false;
            tmrCountdown.Start();
        }

        private void Card_Click(object sender, EventArgs e)
        {
            var pb = (PictureBox)sender;

           
            if (!gameRunning || isBusy || matched.Contains(pb) || pb == first)
                return;

            SetFace(pb, true);

            if (first == null)
            {
                first = pb; 
                pb.Invalidate();
                return;
            }

            
            second = pb;
            isBusy = true;
            turns++;

            if ((int)first.Tag == (int)second.Tag)
            {
                matched.Add(first);
                matched.Add(second);
                first.Cursor = Cursors.Default;
                second.Cursor = Cursors.Default;
                first.Invalidate();
                second.Invalidate();
                score += ScoreMatch;
                first = second = null;
                isBusy = false;
                UpdateLabels();

                if (matched.Count == cards.Count)
                    EndGame(true);
            }
            else
            {
                score = Math.Max(0, score + ScoreMismatch);
                UpdateLabels();
                tmrFlipBack.Start(); 
            }
        }

        private void TmrFlipBack_Tick(object sender, EventArgs e)
        {
            tmrFlipBack.Stop();
            if (first != null) SetFace(first, false);
            if (second != null) SetFace(second, false);
            first = second = null;
            isBusy = false;
        }

        private void TmrCountdown_Tick(object sender, EventArgs e)
        {
            timeLeft--;
            UpdateLabels();
            if (timeLeft <= 0)
                EndGame(false);
        }

        private void SetFace(PictureBox pb, bool faceUp)
        {
            if (faceUp)
            {
                pb.Image = roundFaces[(int)pb.Tag];
                pb.BackColor = Color.White;
            }
            else
            {
                pb.Image = backImage;     
                pb.BackColor = ColBackB;
            }
            pb.Invalidate();
        }

        private void UpdateLabels()
        {
            lblDiemSo.Text = "Điểm: " + score;
            lblSoLuot.Text = "Lượt: " + turns;
            lblThoiGian.Text = "Thời gian: " + Math.Max(0, timeLeft) + "s";
            lblThoiGian.ForeColor = (gameRunning && timeLeft <= 10) ? ColDanger : Color.White;
            LayoutInfoBar();
        }

  

        private void Card_Paint(object sender, PaintEventArgs e)
        {
            var pb = (PictureBox)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(2, 2, pb.Width - 5, pb.Height - 5);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var path = RoundedRect(rect, CardRadius))
            {
                
                if (pb.Image == null)
                {
                    using (var lg = new LinearGradientBrush(rect, ColBackA, ColBackB, 90f))
                        g.FillPath(lg, path);

                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var br = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                        g.DrawString("?", markFont, br, new RectangleF(0, 0, pb.Width, pb.Height), sf);
                }

                using (var outside = new GraphicsPath())
                {
                    outside.AddRectangle(new Rectangle(-1, -1, pb.Width + 2, pb.Height + 2));
                    outside.AddPath(path, false);
                    using (var br = new SolidBrush(ColBoard))
                        g.FillPath(br, outside);
                }

                Color border;
                float width;
                if (matched.Contains(pb))
                {
                    using (var br = new SolidBrush(Color.FromArgb(70, 38, 190, 130)))
                        g.FillPath(br, path);
                    border = ColAccent;
                    width = 4f;
                }
                else if (pb == first)
                {
                    border = ColWarn;
                    width = 4f;
                }
                else if (pb == hoverCard && gameRunning && !isBusy)
                {
                    border = ColAccent;
                    width = 3f;
                }
                else
                {
                    border = Color.FromArgb(60, 20, 58, 48);
                    width = 1.5f;
                }
                using (var pen = new Pen(border, width))
                    g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

       

        private void EndGame(bool won)
        {
            gameRunning = false;
            isBusy = true;
            tmrFlipBack.Stop();
            tmrCountdown.Stop();
            tmrPreview.Stop();
            StopSound();

            if (won)
                score += timeLeft * BonusPerSecondLeft;
            int timeUsed = TimeLimitSeconds - Math.Max(0, timeLeft);
            UpdateLabels();

            PlayEnding(won ? winPlayer : losePlayer);

            string title = won ? "Chiến thắng!" : "Hết giờ!";
            string msg = (won ? "Chúc mừng! Bạn đã ghép hết các cặp thẻ." : "Rất tiếc, bạn đã hết thời gian.")
                         + "\nĐiểm: " + score + "  |  Lượt: " + turns + "  |  Thời gian: " + timeUsed + "s";

            string name = PromptName(title, msg + "\n\nNhập tên của bạn để lưu thành tích:");
            if (name != null)
                SaveScore(name, score, timeUsed, turns);

            var again = MessageBox.Show("Bạn có muốn chơi lại không?", "Trúc Xanh",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (again == DialogResult.Yes)
                StartGame();
            else
                ShowStartScreen("Ván vừa rồi: " + score + " điểm - " + (won ? "chiến thắng" : "hết giờ"));
        }

        private void RestartRequested()
        {
            if (gameRunning)
            {
                bool wasRunning = tmrCountdown.Enabled;
                tmrCountdown.Stop();
                var r = MessageBox.Show("Ván hiện tại sẽ bị hủy. Chơi ván mới?", "Trúc Xanh",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes)
                {
                    if (wasRunning && gameRunning) tmrCountdown.Start();
                    return;
                }
            }
            StartGame();
        }

    
        private string PromptName(string title, string message)
        {
            using (var dlg = new Form())
            {
                var lbl = new Label { Text = message, AutoSize = false, Left = 20, Top = 18, Width = 360, Height = 90, ForeColor = ColDark };
                var txt = new TextBox { Left = 20, Top = 115, Width = 360, MaxLength = 20, Font = new Font("Segoe UI", 12F), BorderStyle = BorderStyle.FixedSingle };
                var ok = new RoundedButton
                {
                    Text = "Lưu",
                    Left = 190,
                    Top = 160,
                    Size = new Size(90, 36),
                    Radius = 18,
                    NormalColor = ColAccent,
                    HoverColor = ColAccentHover,
                    PressedColor = ColAccentDown,
                    ForeColor = Color.White,
                    DialogResult = DialogResult.OK
                };
                var skip = new RoundedButton
                {
                    Text = "Bỏ qua",
                    Left = 290,
                    Top = 160,
                    Size = new Size(90, 36),
                    Radius = 18,
                    NormalColor = Color.FromArgb(150, 170, 162),
                    HoverColor = Color.FromArgb(125, 148, 139),
                    PressedColor = Color.FromArgb(100, 125, 116),
                    ForeColor = Color.White,
                    DialogResult = DialogResult.Cancel
                };

                dlg.Text = title;
                dlg.Font = Font;
                dlg.BackColor = ColBoard;
                dlg.ClientSize = new Size(400, 215);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.AcceptButton = ok;
                dlg.CancelButton = skip;
                dlg.Controls.AddRange(new Control[] { lbl, txt, ok, skip });

                while (true)
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return null;

                    string name = txt.Text.Trim().Replace("|", " ");
                    if (name.Length > 0)
                        return name;

                    MessageBox.Show("Vui lòng nhập tên (hoặc bấm Bỏ qua).", "Trúc Xanh",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private string ScoreFilePath
        {
            get { return Path.Combine(Application.StartupPath, ScoreFileName); }
        }

      
        private void SaveScore(string name, int points, int seconds, int turnCount)
        {
            try
            {
                string line = string.Format("{0}|{1}|{2}|{3}|{4:dd/MM/yyyy HH:mm}",
                    name, points, seconds, turnCount, DateTime.Now);
                File.AppendAllText(ScoreFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể lưu thành tích:\n" + ex.Message, "Lỗi ghi file",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private class ScoreRecord
        {
            public string Name;
            public int Score;
            public int Seconds;
            public int Turns;
            public string Date;
        }

        private void ShowLeaderboard()
        {
            var records = new List<ScoreRecord>();
            try
            {
                if (!File.Exists(ScoreFilePath))
                {
                    MessageBox.Show("Chưa có thành tích nào được lưu.", "Bảng Thành Tích",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                foreach (var line in File.ReadAllLines(ScoreFilePath, Encoding.UTF8))
                {
                    var p = line.Split('|');
                    int sc, sec, tn;
                    if (p.Length >= 5 &&
                        int.TryParse(p[1], out sc) &&
                        int.TryParse(p[2], out sec) &&
                        int.TryParse(p[3], out tn))
                    {
                        records.Add(new ScoreRecord { Name = p[0], Score = sc, Seconds = sec, Turns = tn, Date = p[4] });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể đọc bảng thành tích:\n" + ex.Message, "Lỗi đọc file",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

        
            var top = records.OrderByDescending(r => r.Score).ThenBy(r => r.Seconds).Take(20).ToList();

            using (var dlg = new Form())
            using (var grid = new DataGridView())
            {
                grid.Dock = DockStyle.Fill;
                grid.ReadOnly = true;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.AllowUserToResizeRows = false;
                grid.RowHeadersVisible = false;
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.BackgroundColor = Color.White;
                grid.BorderStyle = BorderStyle.None;
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                grid.GridColor = Color.FromArgb(220, 235, 228);
                grid.RowTemplate.Height = 32;

                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                grid.ColumnHeadersHeight = 40;
                grid.ColumnHeadersDefaultCellStyle.BackColor = ColDark;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColDark;
                grid.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 238, 220);
                grid.DefaultCellStyle.SelectionForeColor = ColDark;
                grid.AlternatingRowsDefaultCellStyle.BackColor = ColBoard;

                grid.Columns.Add("hang", "Hạng");
                grid.Columns.Add("ten", "Tên");
                grid.Columns.Add("diem", "Điểm");
                grid.Columns.Add("tg", "Thời gian (s)");
                grid.Columns.Add("luot", "Lượt");
                grid.Columns.Add("ngay", "Ngày");

                for (int i = 0; i < top.Count; i++)
                    grid.Rows.Add(i + 1, top[i].Name, top[i].Score, top[i].Seconds, top[i].Turns, top[i].Date);

                dlg.Text = "Bảng Thành Tích - Top 20";
                dlg.Font = Font;
                dlg.ClientSize = new Size(700, 440);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Controls.Add(grid);
                dlg.ShowDialog(this);
            }
        }


        private void ToggleSound()
        {
            soundOn = !soundOn;
            âmThanhBậtToolStripMenuItem.Text = soundOn ? "Âm Thanh: Bật" : "Âm Thanh: Tắt";
            if (soundOn)
            {
                if (gameRunning) PlayBackground();
            }
            else
            {
                StopSound();
            }
        }

        private void PlayBackground()
        {
            if (!soundOn || bgPlayer == null) return;
            try { bgPlayer.PlayLooping(); }
            catch (Exception) { /* bỏ qua lỗi âm thanh, không làm crash game */ }
        }

   
        private void PlayEnding(SoundPlayer p)
        {
            if (!soundOn || p == null) return;
            try { p.Play(); }
            catch (Exception) { }
        }

        private void StopSound()
        {
            try
            {
                if (bgPlayer != null) bgPlayer.Stop();
                if (winPlayer != null) winPlayer.Stop();
                if (losePlayer != null) losePlayer.Stop();
            }
            catch (Exception) { }
        }

     

        private void ShowGuide()
    {
        MessageBox.Show(
            "CÁCH CHƠI:\n" +
            "• Bấm BẮT ĐẦU. Các thẻ sẽ hiện ra trong " + (PreviewMs / 1000.0).ToString("0.#") + " giây để bạn ghi nhớ, sau đó úp lại.\n" +
            "• Click vào thẻ để lật. Lật 2 thẻ giống nhau để ghép cặp.\n" +
            "• Ghép đúng: +" + ScoreMatch + " điểm. Ghép sai: " + ScoreMismatch + " điểm và thẻ tự úp lại.\n" +
            "• Bạn có " + TimeLimitSeconds + " giây để ghép hết các cặp.\n" +
            "• Thắng sớm sẽ được cộng điểm thưởng theo thời gian còn lại.\n" +
            "• Cuối ván, nhập tên để lưu vào Bảng Thành Tích.\n\n" +
            "THÀNH VIÊN NHÓM:\n" +
            "1. Nguyễn Lê Duy - 51.01.104.014 - Logic game và thuật toán\n" +
            "2. Nguyễn Quang Duy - 51.01.104.015 - Timer/Âm thanh/Trạng thái game\n" +
            "3. Nguyễn Phước Vũ - 51.01.104.122 - Giao diện\n" +
            "4. Nguyễn Ngọc Long - 51.01.104.141 - Bảng xếp hạng và lưu dữ liệu",
            "Hướng Dẫn Chơi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

        private void PauseDuringDialog(Action show)
        {
            bool wasRunning = tmrCountdown.Enabled;
            tmrCountdown.Stop();
            try { show(); }
            finally
            {
                if (wasRunning && gameRunning) tmrCountdown.Start();
            }
        }

        private void CleanUp()
        {
            tmrFlipBack.Stop();
            tmrCountdown.Stop();
            tmrPreview.Stop();
            StopSound();
            foreach (var img in allFaces) img.Dispose();
            if (backImage != null) backImage.Dispose();
            if (markFont != null) markFont.Dispose();
        }

        private class RoundedButton : Button
        {
            public Color NormalColor = ColAccent;
            public Color HoverColor = ColAccentHover;
            public Color PressedColor = ColAccentDown;
            public int Radius = 20;
            private bool hover, down;

            public RoundedButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : BackColor);

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = RoundedRect(rect, Math.Min(Radius, Height / 2)))
                using (var br = new SolidBrush(down ? PressedColor : (hover ? HoverColor : NormalColor)))
                    g.FillPath(br, path);

                TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        /// <summary>Bảng màu cho thanh menu (nền xanh tre đậm).</summary>
        private class ModernColors : ProfessionalColorTable
        {
            private static readonly Color Hot = Color.FromArgb(45, 100, 84);

            public override Color MenuStripGradientBegin { get { return ColDark; } }
            public override Color MenuStripGradientEnd { get { return ColDark; } }
            public override Color ToolStripBorder { get { return ColDark; } }
            public override Color MenuBorder { get { return ColDark; } }
            public override Color MenuItemBorder { get { return Hot; } }
            public override Color MenuItemSelected { get { return Hot; } }
            public override Color MenuItemSelectedGradientBegin { get { return Hot; } }
            public override Color MenuItemSelectedGradientEnd { get { return Hot; } }
            public override Color MenuItemPressedGradientBegin { get { return ColAccentDown; } }
            public override Color MenuItemPressedGradientEnd { get { return ColAccentDown; } }
        }
    }
}
