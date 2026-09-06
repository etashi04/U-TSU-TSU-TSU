using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

internal static class Program
{
    const string Version = "v1.0.0-rc1";
    const string ExeHash = "67C89E57ED43C3F0AB90510EC2DD742537FAABA14FAF8246296D139674DFADE7";
    const string MarkerDir = "_KR_PATCH_v1.0.0-rc1";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && (args[0].Equals("/install", StringComparison.OrdinalIgnoreCase) || args[0].Equals("/restore", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using (var form = new InstallerForm()) form.RunCommand(args[0], args[1]);
            }
            catch { Environment.ExitCode = 1; }
            return;
        }
        Application.Run(new InstallerForm());
    }

    sealed class InstallerForm : Form
    {
        readonly TextBox path = new TextBox();
        readonly Label status = new Label();
        bool silent;

        public InstallerForm()
        {
            Text = "うつらうつらとつつうらうらと 한국어 패치 " + Version;
            Icon = LoadInstallerIcon();
            ClientSize = new Size(640, 285);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = true; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 245, 245);
            Font = new Font("Malgun Gothic", 9F);

            var title = new Label { Text = "うつらうつらとつつうらうらと\r\n비공식 한국어 패치", Left = 28, Top = 22, Width = 420, Height = 58, Font = new Font("Malgun Gothic", 15, FontStyle.Bold) };
            var logoPanel = new Panel { Left = 465, Top = 15, Width = 150, Height = 68, BackColor = Color.FromArgb(34, 57, 78), BorderStyle = BorderStyle.FixedSingle };
            var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = LoadLogo(), Margin = new Padding(6) };
            logoPanel.Padding = new Padding(7);
            logoPanel.Controls.Add(logo);
            var info = new Label { Text = "지원 게임: Steam 2026-08-29 빌드 · 원본 파일은 자동 백업됩니다.", Left = 28, Top = 91, Width = 585, Height = 24 };
            var folderLabel = new Label { Text = "게임 설치 폴더", Left = 28, Top = 125, Width = 150, Height = 22 };
            path.Left = 28; path.Top = 148; path.Width = 475; path.Height = 25; path.Text = GuessPath();
            var browse = new Button { Text = "찾아보기", Left = 513, Top = 146, Width = 100, Height = 29 };
            browse.Click += delegate { using (var d = new FolderBrowserDialog()) { d.SelectedPath = path.Text; if (d.ShowDialog() == DialogResult.OK) path.Text = d.SelectedPath; } };
            var install = new Button { Text = "한국어 패치 설치", Left = 28, Top = 197, Width = 178, Height = 38, Font = new Font("Malgun Gothic", 9, FontStyle.Bold) };
            var restore = new Button { Text = "원본 복구", Left = 218, Top = 197, Width = 128, Height = 38, Font = new Font("Malgun Gothic", 9, FontStyle.Bold) };
            install.Click += delegate { RunSafe(Install); };
            restore.Click += delegate { RunSafe(Restore); };
            status.Left = 28; status.Top = 245; status.Width = 585; status.Height = 26; status.TextAlign = ContentAlignment.MiddleLeft;
            Controls.AddRange(new Control[] { title, logoPanel, info, folderLabel, path, browse, install, restore, status });
        }

        static Image LoadLogo()
        {
            Stream stream = typeof(Program).Assembly.GetManifestResourceStream("InstallerLogo");
            return stream == null ? null : Image.FromStream(stream);
        }

        static Icon LoadInstallerIcon()
        {
            Stream stream = typeof(Program).Assembly.GetManifestResourceStream("InstallerIcon");
            return stream == null ? null : new Icon(stream);
        }

        public void RunCommand(string command, string targetPath)
        {
            silent = true;
            path.Text = targetPath;
            if (command.Equals("/install", StringComparison.OrdinalIgnoreCase)) Install();
            else Restore();
        }

        string GuessPath()
        {
            string here = AppDomain.CurrentDomain.BaseDirectory;
            string common = Path.GetFullPath(Path.Combine(here, @"..\..\..\..\..\.."));
            string candidate = Path.Combine(common, "うつらうつらとつつうらうらと");
            return File.Exists(Path.Combine(candidate, "UTSUTSUTSU.exe")) ? candidate : "";
        }

        void RunSafe(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = "실패: " + ex.Message; MessageBox.Show(ex.Message, "작업 실패", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        string ValidateTarget()
        {
            string target = Path.GetFullPath(path.Text.Trim());
            string exe = Path.Combine(target, "UTSUTSUTSU.exe");
            if (!File.Exists(exe)) throw new InvalidOperationException("선택한 폴더에서 UTSUTSUTSU.exe를 찾지 못했습니다.");
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(exe))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!hash.Equals(ExeHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("지원하지 않는 게임 실행 파일입니다. Steam 파일 무결성 검사를 먼저 해 주세요.");
            }
            return target;
        }

        void Install()
        {
            string target = ValidateTarget();
            string temp = Path.Combine(Path.GetTempPath(), "UtsutsutsuKoreanPatch_" + Guid.NewGuid().ToString("N"));
            string payload = Path.Combine(temp, "패치파일");
            string marker = Path.Combine(target, MarkerDir);
            string backup = Path.Combine(marker, "원본백업");
            var installed = new List<string>();
            try
            {
                ExtractEmbeddedPayload(payload);
                Directory.CreateDirectory(backup);
                foreach (string source in Directory.GetFiles(payload, "*", SearchOption.AllDirectories))
                {
                    string relative = source.Substring(payload.Length).TrimStart(Path.DirectorySeparatorChar);
                    string destination = Path.Combine(target, relative);
                    string backupFile = Path.Combine(backup, relative);
                    if (File.Exists(destination) && !File.Exists(backupFile))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
                        File.Copy(destination, backupFile, true);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(source, destination, true);
                    installed.Add(relative);
                }
                File.WriteAllLines(Path.Combine(marker, "설치파일목록.txt"), installed.ToArray());
                File.WriteAllText(Path.Combine(marker, "버전.txt"), Version);
                status.Text = "한국어 패치 설치가 완료되었습니다.";
                if (!silent) MessageBox.Show("설치가 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                Rollback(target, marker, installed);
                throw;
            }
            finally
            {
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        static void ExtractEmbeddedPayload(string payload)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
            if (stream == null) throw new InvalidDataException("설치기에 내장된 패치 데이터를 찾지 못했습니다.");
            Directory.CreateDirectory(payload);
            string root = Path.GetFullPath(payload) + Path.DirectorySeparatorChar;
            using (stream)
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string destination = Path.GetFullPath(Path.Combine(payload, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("잘못된 내장 패치 경로입니다.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream source = entry.Open())
                    using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write)) source.CopyTo(output);
                }
            }
        }

        void Restore()
        {
            string target = ValidateTarget();
            string marker = Path.Combine(target, MarkerDir);
            string manifest = Path.Combine(marker, "설치파일목록.txt");
            if (!File.Exists(manifest)) throw new InvalidOperationException("이 버전의 설치 기록을 찾지 못했습니다.");
            Rollback(target, marker, new List<string>(File.ReadAllLines(manifest)));
            status.Text = "원본 복구가 완료되었습니다.";
            if (!silent) MessageBox.Show("원본 복구가 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static void Rollback(string target, string marker, IList<string> installed)
        {
            string backup = Path.Combine(marker, "원본백업");
            for (int i = installed.Count - 1; i >= 0; i--)
            {
                string relative = installed[i];
                string destination = Path.Combine(target, relative);
                string backupFile = Path.Combine(backup, relative);
                if (File.Exists(backupFile)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(backupFile, destination, true); }
                else if (File.Exists(destination)) File.Delete(destination);
            }

            // BepInEx creates these disposable files on first launch. They are not
            // part of the payload manifest, but leaving them behind prevents a
            // clean restoration on an otherwise unmodified game install.
            DeleteFileIfPresent(Path.Combine(target, @"BepInEx\LogOutput.log"));
            DeleteDirectoryIfPresent(Path.Combine(target, @"BepInEx\cache"));

            if (Directory.Exists(marker)) Directory.Delete(marker, true);

            // Remove only directories that are empty after restoring the files.
            // Existing mod/config files therefore remain untouched.
            RemoveEmptyDirectories(Path.Combine(target, "BepInEx"));
        }

        static void DeleteFileIfPresent(string file)
        {
            if (File.Exists(file)) File.Delete(file);
        }

        static void DeleteDirectoryIfPresent(string directory)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static void RemoveEmptyDirectories(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (string child in Directory.GetDirectories(directory)) RemoveEmptyDirectories(child);
            if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
        }
    }
}
