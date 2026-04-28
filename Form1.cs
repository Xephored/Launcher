namespace Launcher
{
    using Launcher.Functions;
    using Mywebext.Language;
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public partial class Form1 : Form
    {
        private TcpListener listener;
        private TextBox txtLog;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtGamePath;
        private Button btnPlay;
        private Button btnBrowseFolder;

        private byte[] sbox;
        private bool formReady = false;

        // 🔥 FIX IMPORTANT: RC4 activation state
        private bool rc4Activated = false;

        public Form1()
        {
            InitializeComponent();

            this.Text = LanguageService.T("form.title", "Origins Game Launcher");
            this.Width = 800;
            this.Height = 650;

            TableLayoutPanel topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 120,
                ColumnCount = 4,
                RowCount = 3
            };

            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            // Username
            Label lblUser = new Label { Text = "Username:", AutoSize = true };
            txtUsername = new TextBox { Text = Settings.launcher.Default.UserName, Dock = DockStyle.Fill };

            // Password
            Label lblPass = new Label { Text = "Password:", AutoSize = true };
            txtPassword = new TextBox
            {
                Text = Settings.launcher.Default.Password,
                UseSystemPasswordChar = true,
                Dock = DockStyle.Fill
            };

            // Game path
            Label lblGame = new Label { Text = "Game Path:", AutoSize = true };
            txtGamePath = new TextBox { Text = Settings.launcher.Default.Directory, Dock = DockStyle.Fill };

            btnBrowseFolder = new Button { Text = "Browse" };
            btnBrowseFolder.Click += BtnBrowseFolder_Click;

            btnPlay = new Button { Text = "Play" };
            btnPlay.Click += BtnPlay_Click;

            // Layout restore
            topPanel.Controls.Add(lblUser, 0, 0);
            topPanel.Controls.Add(txtUsername, 1, 0);
            topPanel.Controls.Add(lblPass, 0, 1);
            topPanel.Controls.Add(txtPassword, 1, 1);
            topPanel.Controls.Add(lblGame, 0, 2);
            topPanel.Controls.Add(txtGamePath, 1, 2);
            topPanel.Controls.Add(btnBrowseFolder, 2, 2);
            topPanel.Controls.Add(btnPlay, 3, 0);
            topPanel.SetRowSpan(btnPlay, 3);

            txtLog = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            this.Controls.Add(txtLog);
            this.Controls.Add(topPanel);

            // RC4 init
            string rc4 = string.IsNullOrEmpty(Settings.launcher.Default.RC4)
                ? "DAoC"
                : Settings.launcher.Default.RC4;

            sbox = GenerateSBox(Encoding.ASCII.GetBytes(rc4));

            Log("[INIT] Launcher ready");
        }

        // ================= LOG =================
        private void Log(string msg)
        {
            if (txtLog != null && txtLog.IsHandleCreated)
            {
                txtLog.BeginInvoke((MethodInvoker)(() =>
                {
                    txtLog.AppendText(msg + Environment.NewLine);
                }));
            }
        }

        // ================= BROWSE =================
        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog fbd = new FolderBrowserDialog();

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtGamePath.Text = fbd.SelectedPath;
                Settings.launcher.Default.Directory = fbd.SelectedPath;
                Settings.launcher.Default.Save();

                Log("[PATH] Game folder set");
            }
        }

        // ================= PLAY =================
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Username/password missing");
                return;
            }

            btnPlay.Enabled = false;

            Log($"[LAUNCH] User={user}");

            try
            {
                LaunchGame(user, pass);
            }
            catch (Exception ex)
            {
                Log("[LAUNCH ERROR] " + ex);
                btnPlay.Enabled = true;
                return;
            }

            try
            {
                StartProxy();
            }
            catch (Exception ex)
            {
                Log("[PROXY ERROR] " + ex);
                btnPlay.Enabled = true;
            }

            this.ActiveControl = txtLog;
        }

        // ================= LAUNCH =================
        private void LaunchGame(string username, string password)
        {
            string gameDir = txtGamePath.Text;

            if (string.IsNullOrEmpty(gameDir))
            {
                Log("[ERROR] Invalid game path");
                return;
            }

            var login = new Login();
            bool started = login.Start(username, password);

            string GameFile = Settings.launcher.Default.FileName;

            Log($"Game launched for user '{username}' from '{gameDir}\\{GameFile}'");
        }

        // ================= PROXY =================
        private bool proxyStarted = false;

        private void StartProxy()
        {
            if (proxyStarted)
            {
                Log("[PROXY] Already running - ignored second call");
                return;
            }

            proxyStarted = true;

            int localPort = Settings.launcher.Default.LocalPort;
            string localIP = GetLocalIPAddress();

            try
            {
                listener = new TcpListener(IPAddress.Parse(localIP), localPort);
                listener.Start();

                Log($"[PROXY] Listening on {localIP}:{localPort}");

                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var client = await listener.AcceptTcpClientAsync();
                            _ = HandleClient(client);
                        }
                        catch (Exception ex)
                        {
                            Log("[PROXY LOOP ERROR] " + ex.Message);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log("[PROXY START ERROR] " + ex);
                proxyStarted = false;
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            Log("[PROXY] Client connected");

            var server = new TcpClient();

            string remoteIP = Settings.launcher.Default.RemoteIP;
            int remotePort = Settings.launcher.Default.RemotePort;

            await server.ConnectAsync(remoteIP, remotePort);

            Log($"[PROXY] Connected to server {remoteIP}:{remotePort}");

            var cStream = client.GetStream();
            var sStream = server.GetStream();

            _ = Pipe(cStream, sStream, "C->S");
            _ = Pipe(sStream, cStream, "S->C");
        }
        // ================= PIPE (FIXED RC4 + CHECKSUM SAFE) =================
        private async Task Pipe(NetworkStream input, NetworkStream output, string tag)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (true)
                {
                    int bytesRead = await input.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                        break;

                    byte[] packet = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, packet, 0, bytesRead);

                    // ✅ DEBUG ICI (AVANT TOUT)
                    Log($"[DEBUG {tag}] first bytes: {BitConverter.ToString(packet, 0, Math.Min(16, bytesRead))}");

                    // decode éventuel
                    DAoC125Decode(packet, 0, bytesRead, sbox);

                    Log($"[PIPE {tag}] {bytesRead} bytes");

                    await output.WriteAsync(packet, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                Log($"[PIPE {tag} ERROR] {ex.Message}");
            }
        }
        // ================= DAOC 1.126 PSEUDO RC4 =================
        private static void DAoC125Decode(byte[] buf, int offset, int len, byte[] sbox)
        {
            byte[] s = new byte[256];
            Buffer.BlockCopy(sbox, 0, s, 0, 256);

            byte x = 0, y = 0;
            int mid = len / 2;

            for (int i = mid; i < len; i++)
            {
                x++;
                y += s[x];
                (s[x], s[y]) = (s[y], s[x]);

                buf[i + offset] ^= s[(byte)(s[x] + s[y])];
            }

            for (int i = 0; i < mid; i++)
            {
                x++;
                y += s[x];
                (s[x], s[y]) = (s[y], s[x]);

                buf[i + offset] ^= s[(byte)(s[x] + s[y])];
            }
        }

        // ================= SBOX =================
        private static byte[] GenerateSBox(byte[] key)
        {
            byte[] sbox = new byte[256];

            for (int i = 0; i < 256; i++)
                sbox[i] = (byte)i;

            int j = 0;

            for (int i = 0; i < 256; i++)
            {
                j = (j + sbox[i] + key[i % key.Length]) & 0xFF;

                byte tmp = sbox[i];
                sbox[i] = sbox[j];
                sbox[j] = tmp;
            }

            return sbox;
        }

        // ================= LOCAL IP =================
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            throw new Exception("No IPv4 found");
        }
    }
}