using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileCheckSum
{
    public partial class Form1 : Form
    {
        #region Variables
        private Timer timer;

        private long TotalBytes = 0;
        private long BytePassed = 0;

        #endregion

        #region Main Function
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        #endregion

        #region Events
        private void buttonBrowseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.ShowNewFolderButton = false;
                fbd.SelectedPath = textBoxFolder.Text;
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBoxFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            BackgroundWorker bg = new BackgroundWorker();
            bg.WorkerReportsProgress = true;
            bg.DoWork += bg_DoWork;
            bg.RunWorkerCompleted += bg_RunWorkerCompleted;
            bg.ProgressChanged += bg_ProgressChanged;

            timer = new Timer();
            timer.Tick += timer_Tick;
            timer.Interval = 1000;

            buttonOK.Enabled = buttonBrowseFolder.Enabled = textBoxFolder.Enabled = checkBox1.Enabled = false;
            progressBar1.Visible = true;

            // clear message
            listView1.Items.Clear();
            showlabelmsg("");

            bg.RunWorkerAsync();
            timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (TotalBytes == 0)
            {
                progressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progressBar1.Style = ProgressBarStyle.Blocks;
                progressBar1.Value = (int)Math.Max(Math.Min(100, (BytePassed * 100) / TotalBytes), 0);
            }
        }

        private void bg_DoWork(object sender, DoWorkEventArgs e)
        {
            // Clear old value (May be take time to get new value)
            TotalBytes = 0;
            BytePassed = 0;

            showlabelmsg(string.Format("Calculating total size..."));

            TotalBytes = GetDirectorySize(textBoxFolder.Text);

            ScanDirectory(textBoxFolder.Text);
        }

        private void bg_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            buttonOK.Enabled = buttonBrowseFolder.Enabled = textBoxFolder.Enabled = checkBox1.Enabled = true;
            progressBar1.Value = 100;
            showlabelmsg("");

            BackgroundWorker bg = sender as BackgroundWorker;
            bg.Dispose();

            timer.Stop();
            timer.Dispose();
        }

        private void bg_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
        }

        private void labelMundhaSoft_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Show my projects
            System.Diagnostics.Process.Start("https://www.facebook.com/mundhaSoft");
        }

        private void listView1_SizeChanged(object sender, EventArgs e)
        {
            listView1.Columns[1].Width = listView1.Width - 160;
        }
        #endregion

        #region Functions
        /// <summary>
        /// Get total size of directory
        /// </summary>
        /// <param name="dir">Directory Path</param>
        /// <returns>Size in bytes</returns>
        private long GetDirectorySize(string dir)
        {
            try
            {
                long size = 0;

                // Make sure that dir ended with backslash
                if (!dir.EndsWith("\\"))
                    dir += "\\";

                // Make sure that directory exists in system
                if (!Directory.Exists(dir))
                    throw new DirectoryNotFoundException(string.Format("{0} not found", dir));

                // Get list of directories and files
                string[] directories = Directory.GetDirectories(dir);
                string[] files = Directory.GetFiles(dir);

                foreach (string fileName in files)
                {
                    FileInfo fInfo = new FileInfo(fileName);
                    size += fInfo.Length;
                }

                foreach (string dirName in directories)
                {
                    size += GetDirectorySize(dirName);
                }

                return size;
            }
            catch
            {
                return 0;
            }
        }

        private void ScanDirectory(string dir)
        {
            try
            {
                // show status dir
                showlabelmsg(dir);

                // Make sure that dir ended with backslash
                if (!dir.EndsWith("\\"))
                    dir += "\\";

                // Make sure that directory exists in system
                if (!Directory.Exists(dir))
                    throw new DirectoryNotFoundException(string.Format("{0} not found", dir));

                // Get list of directories and files
                string[] directories = Directory.GetDirectories(dir);
                string[] files = Directory.GetFiles(dir);

                // No directory or file found
                if (directories.Length + files.Length == 0)
                    return;

                #region File checksum
                // Local variable for old md5 checksum and new md5 checksum
                Dictionary<string, string> md5old = new Dictionary<string, string>();
                Dictionary<string, string> md5new = new Dictionary<string, string>();

                // Read old md5 checksum
                // and store in local variable
                if (File.Exists(dir + "checksum.md5"))
                {
                    foreach (string checksum_old in File.ReadAllLines(dir + "checksum.md5"))
                    {
                        string[] s = checksum_old.Split('\t');
                        if (s.Length == 2)
                            md5old.Add(s[0], s[1]);
                    }

                }

                foreach (String fileName in files)
                {
                    FileInfo fInfo = new FileInfo(fileName);

                    // dont make checksum of old checksum info file
                    if (fInfo.Extension == ".md5")
                    {
                        continue;
                    }

                    showlabelmsg(string.Format("{0} (size: {1})", fileName, getFileSizeString(fInfo.Length)));

                    string md5hash = md5File(fileName);
                    md5new.Add(fInfo.Name, md5hash);

                    // update progress
                    BytePassed += fInfo.Length;
                }

                // only update checksum file if need
                bool IsUpdateChecksumFile = false;

                // old file and new file count not match
                // so files are added or deleted
                // must be update checksum
                if (md5old.Count != md5new.Count)
                    IsUpdateChecksumFile = true;

                foreach (KeyValuePair<string, string> md5Value in md5new)
                {
                    // old md5 checksum not found
                    // may be new file created
                    if (!md5old.ContainsKey(md5Value.Key))
                    {
                        addListItem("New file", dir + md5Value.Key);
                        IsUpdateChecksumFile = true;
                        continue;
                    }

                    // old md5 checksum and new md5 checksum not match
                    if (md5old[md5Value.Key] != md5Value.Value)
                    {
                        addListItem("Missmatch", dir + md5Value.Key);
                        IsUpdateChecksumFile = true;
                        continue;
                    }
                }

                // update checksum file if required
                if (!checkBox1.Checked && IsUpdateChecksumFile)
                {
                    StreamWriter md5checksumFile = File.CreateText(dir + "checksum.md5");
                    foreach (KeyValuePair<string, string> md5Value in md5new)
                    {
                        md5checksumFile.WriteLine(string.Format("{0}\t{1}", md5Value.Key, md5Value.Value));
                    }
                    md5checksumFile.Close();
                }

                #endregion

                // Scan Sub Direcotries
                foreach (string directoryName in directories)
                {
                    ScanDirectory(directoryName);
                }
            }
            catch (Exception ex)
            {
                addListItem("Error", ex.Message, Color.Red);
            }
        }

        /// <summary>
        /// Get MD5 value of file
        /// </summary>
        /// <param name="fileName">File Path</param>
        /// <returns></returns>
        private string md5File(string fileName)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException(string.Format("{0} not found", fileName));

            FileStream fs = File.OpenRead(fileName);

            MD5 md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(fs);

            fs.Close();

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        /// <summary>
        /// Convert long to KB,MB
        /// </summary>
        /// <param name="size">size in Bytes</param>
        /// <returns>String format of size</returns>
        private string getFileSizeString(long size)
        {
            string[] units = new string[] { "Bytes", "KB", "MB", "GB", "TB" };

            int pos = 0;

            //int intSize = (int)size;

            while (size > 1024 && pos < units.Length)
            {
                pos++;
                size /= 1024;
            }

            return string.Format("{0} {1}", (int)size, units[pos]);
        }

        private void addListItem(string msg, string path)
        {
            addListItem(msg, path, Color.Black);
        }

        private void addListItem(string msg, string path, Color color)
        {
            ListViewItem liv = new ListViewItem(msg);
            liv.SubItems.Add(path);
            liv.ForeColor = color;

            if (!listView1.InvokeRequired)
            {
                listView1.Items.Add(liv);
            }
            else
            {
                listView1.Invoke(new MethodInvoker(delegate()
                {
                    listView1.Items.Add(liv);
                }));
            }
        }

        // Show current message
        private void showlabelmsg(string message)
        {
            if (!labelStatus.InvokeRequired)
            {
                labelStatus.Text = message;
            }
            else
            {
                labelStatus.Invoke(new MethodInvoker(delegate()
                {
                    labelStatus.Text = message;
                }));
            }
        }

        #endregion
    }
}
