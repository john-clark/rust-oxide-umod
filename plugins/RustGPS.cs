using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpPcap;
using System.Net;
using System.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using WMPLib;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Collections;
using System.Globalization;

//https://github.com/neilopet/RustGPS/blob/master/RustGPS.cs

namespace RustGPS
{
    public partial class frmRGPS : Form
    {
        public string version = "2.4";
        public CaptureDeviceList devices;
        public ICaptureDevice device;
        public string[] bootstrap;
        public float _x, _y, _z;
        public Double _direction;
        public int min_update_time = 3;
        public bool updated = false;
        public bool died = false;
        public bool shooting = false;
        public WebClient httpClient;
        public Thread updateQMAP;
        public Thread dropThread;
        public string key = ""; /* api key*/
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        public double last_updated = 0;
        public string latest_version = "http://url.domain.com/latest_version_check.txt";
        public string latest_download = "http://url.domain.com/latest_version.zip";
        public Form splashScreen;
        public Form self;
        public dynamic waypoint_types;
        public string waypoint_name = "";
        public int waypoint_id = 0;
        public Single _fx, _fy, _fz;
        public Double _fd;
        public string entity;
        public bool updateEntity = false;
        public OrderedDictionary players = new OrderedDictionary();
        public Object player_lock = new Object();
        public OrderedDictionary drops = new OrderedDictionary();
        public Object drop_lock = new Object();
        public string my_entity = "";
        public Thread sendForeignEntityThread;
        public string[] doorPasswords = {
            ""
        };

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();


        public frmRGPS()
        {
            InitializeComponent();
        }

        public static bool winPcapIsInstalled()
        {
            RegistryKey winPcapKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\NPF", true);
            if (winPcapKey == null)
            {
                return false;
            }
            string currentKey = winPcapKey.GetValue("DisplayName").ToString();
            return (currentKey != null && currentKey != "");
        }

        private void frmRGPS_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void frmRGPS_Load(object sender, EventArgs e)
        {
            self = this;
            splashScreen = new Splash();
            splashScreen.Show();
            new Thread(checkForClientUpdates).Start();

            lblVersion.Text = String.Format("v{0}", version);
            if (!winPcapIsInstalled())
            {
                if (MessageBox.Show("WinPCAP is not installed.  You must install WinPCAP in order for QMAP to work.  Would you like to install WinPCAP now?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start(String.Format("{0}\\Resources\\WinPcap_4_1_3.exe", AppDomain.CurrentDomain.BaseDirectory));
                }
                Application.Exit();
                return;
            }

            txtBootstrap.Text = readTokenFile();
            if (txtBootstrap.Text.Length > 0)
            {
                saveBootstrapToken.Checked = true;
            }

            devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
            {
                MessageBox.Show("No devices were found on this machine");
                return;
            }

            foreach (var dev in devices)
            {
                SharpPcap.WinPcap.WinPcapDevice WinPcapDev = (SharpPcap.WinPcap.WinPcapDevice)dev;
                cboInterfaces.Items.Add(WinPcapDev.Interface.FriendlyName.ToString());
                if (WinPcapDev.Interface.GatewayAddress != null)
                {
                    cboInterfaces.Text = WinPcapDev.Interface.FriendlyName.ToString();
                }
            }

            string netif = getSavedNetworkInterface();
            if (netif != "")
            {
                cboInterfaces.Text = netif;
            }

            waypoint_types = JsonConvert.DeserializeObject(new WebClient().DownloadString("http://mysite.com/gps/?wptypes"));
            foreach (var waypoint_type in waypoint_types)
            {
                if (waypoint_type != null)
                {
                    if (waypoint_type.name != null)
                    {
                        cboType.Items.Add(waypoint_type.name);
                    }
                }
            }
        }

        public void checkForClientUpdates()
        {
            WebClient Client = new WebClient();
            Client.Headers.Add("Cache-Control", "no-cache");
            //double latest_v = Convert.ToDouble(Client.DownloadString(latest_version).ToString().Trim());
            double latest_v = Double.Parse(Client.DownloadString(latest_version).ToString().Trim(), CultureInfo.InvariantCulture);
            //double current_v = Convert.ToDouble(version);
            double current_v = Double.Parse(version, CultureInfo.InvariantCulture);

            if (latest_v > current_v)
            {
                Thread.Sleep(1000);
                MessageBox.Show("An update has been downloaded.  Press OK to download and install the latest version.", "Update Notice", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                string savepath = String.Format("{0}\\gps_latest.zip", AppDomain.CurrentDomain.BaseDirectory);
                System.IO.File.Delete(savepath);
                Client.DownloadFile(latest_download, savepath);
                Process.Start(String.Format("{0}\\GPSUpdater.exe", AppDomain.CurrentDomain.BaseDirectory));
            }
            try
            {
                self.Opacity = 1.0;
                splashScreen.Close();
            }
            catch (Exception ex)
            {
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (txtBootstrap.Text == "" || !txtBootstrap.Text.Contains(";"))
            {
                MessageBox.Show("Please enter your bootstrap token.  This can be found at http://www.mysite.com/gps/", "Required Field Notification", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtBootstrap.Focus();
                return;
            }
            bootstrap = txtBootstrap.Text.Split(';');
            bootstrap[0] = bootstrap[0].Trim();
            bootstrap[1] = bootstrap[1].Trim();
            try
            {
                IPAddress.Parse(bootstrap[1]);
            }
            catch (Exception ex2)
            {
                MessageBox.Show("Invalid bootstrap token.  The IP Address was not found.  This can be found at http://www.mysite.com/gps/", "Required Field Notification", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtBootstrap.Focus();
                return;
            }
            if (cboInterfaces.SelectedIndex < 0 || cboInterfaces.SelectedItem.ToString() == "")
            {
                MessageBox.Show("Please select a valid Network Interface.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                cboInterfaces.Focus();
                return;
            }
            httpClient = new WebClient();
            httpClient.Headers.Add("Connection: keep-alive");
            device = this.devices[int.Parse(cboInterfaces.SelectedIndex.ToString())];
            saveNetworkInterface();
            device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);
            device.Open(DeviceMode.Promiscuous, 1000);
            //device.Filter = String.Format("host {0} and less 84 and greater 82", bootstrap[1]);
            device.Filter = String.Format("host {0} and greater 28", bootstrap[1]);
            device.StartCapture();
            lblStatus.Text = "Running";
            lblStatus.ForeColor = System.Drawing.Color.ForestGreen;
            updateQMAP = new Thread(checkUpdate);
            updateQMAP.Start();
            sendForeignEntityThread = new Thread(sendForeignEntities);
            sendForeignEntityThread.Start();
            dropThread = new Thread(sendDropCoordinates);
            dropThread.Start();
        }

        public void sendDropCoordinates()
        {
            while (true)
            {
                lock (drop_lock)
                {
                    if (drops.Count > 0)
                    {
                        foreach(DictionaryEntry dropEntry in drops)
                        {
                            string drop_name = dropEntry.Key.ToString();
                            Drop drop_data = (Drop)dropEntry.Value;
                            using (WebClient wc = new WebClient())
                            {
                                wc.DownloadString(String.Format("http://mysite.com/gps/?drop={0}&ip={1}&name={2}&x={3}&y={4}&z={5}",
                                    bootstrap[0],
                                    bootstrap[1],
                                    drop_name,
                                    drop_data.x.ToString("R"),
                                    drop_data.y.ToString("R"),
                                    drop_data.z.ToString("R")
                                ));
                                Console.WriteLine("Sending Drop Coords: {0}, {1}, {2}", drop_data.x.ToString("N2"), drop_data.y.ToString("N2"), drop_data.z.ToString("N2"));
                            }
                        }
                        drops.Clear();
                    }
                }
                Thread.Sleep(20);
            }
        }

        public void sendForeignEntities()
        {
            while (true)
            {
                /*
                * Always update enemies
                */
                lock (player_lock)
                {
                    if (players.Count > 0)
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                            string req = String.Format("http://mysite.com/gps/?ghost={0}&ip={1}",
                                bootstrap[0],
                                bootstrap[1]
                            );
                            try
                            {
                                wc.UploadString(req, String.Format("data={0}", JsonConvert.SerializeObject(players)));
                            }
                            catch (Exception ex1)
                            {
                            }
                            changeText(myPID, my_entity);
                        }
                        /*
                        foreach (DictionaryEntry d1 in players)
                        {
                            Player plyr = (Player)d1.Value;
                            Console.WriteLine("{0} = ({1}, {2}, {3}) {4}", d1.Key.ToString(), plyr.x, plyr.y, plyr.z, plyr.d);
                        }
                         */
                        players.Clear();
                    }
                }
                Thread.Sleep(20);
            }
        }

        public void checkUpdate()
        {
            while (true)
            {
                /*
                 * Always update enemies
                 */
                if (updated && my_entity != "")
                {
                    updated = false;
                    changeText(lblCoords, String.Format("x: {0}, z: {1}, d: {2}", _x.ToString("N2"), _z.ToString("N2"), _direction.ToString("N2")));
                    try
                    {
                        httpClient.DownloadString(String.Format(
                            "http://mysite.com/gps/?me={0}&x={1}&z={2}&ip={3}&d={4}&death={5}&shooting={6}&v={7}&mark={8}&labelmark={9}&iam={10}",
                            bootstrap[0].ToString(),
                            _x.ToString("R"),
                            _z.ToString("R"),
                            bootstrap[1].ToString(),
                            _direction.ToString(),
                            (died ? "1" : "0"),
                            (shooting ? "1" : "0"),
                            version,
                            waypoint_id,
                            waypoint_name,
                            my_entity
                        ));
                        died = false;
                        shooting = false;
                        waypoint_id = 0;
                        waypoint_name = "";
                    }
                    catch (Exception ex)
                    {
                        /* into the abyss... */
                    }
                }
                else
                {
                    Thread.Sleep(20);
                }
            }
        }

        private void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            /*
             * Set i = 28 because all UDP headers
             * for IPv4 protocol will be 28 bytes in length.
             * IPv4 Header = 20 bytes
             * Datagram = 8 bytes
             */

            double timenow = ConvertToTimestamp(DateTime.Now);

            int j = 0, i = 0, s = 0, t = 0, c = 0, drop = 0;

            int[] dropSignal = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("GetNetworkUpdate"), 28);
            if (dropSignal.Length > 0)
            {
                drop = dropSignal[0];

            }
            if (drop > 28 && e.Packet.Data.Count() >= (drop + 30))
            {
                Double _dx = BitConverter.ToSingle(e.Packet.Data, drop + 18);
                Double _dy = BitConverter.ToSingle(e.Packet.Data, drop + 18 + 4);
                Double _dz = BitConverter.ToSingle(e.Packet.Data, drop + 18 + 8);

                string hexpacket = ByteArrayToString(e.Packet.Data);
                string drop_entity = hexpacket.Substring((drop - 5) * 2, 8);

                Drop _drop = new Drop
                {
                    x = _dx,
                    y = _dy,
                    z = _dz,
                    entity = drop_entity
                };

                if (e.Packet.Data[0x27] != 0x3E)
                {
                    return;
                }

                lock (drop_lock)
                {
                    if (drops.Contains(drop_entity))
                    {
                        drops[drop_entity] = _drop;
                    }
                    else
                    {
                        drops.Add(drop_entity, _drop);
                    }
                }

                return;
            }


            /* yes, the dev is an idiot and misspelled receive */
            int[] recvNet = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("RecieveNetwork"), 28);
            if (recvNet.Length > 0)
            {
                c = recvNet[0];
            }

            if (c > 28)
            {
                string hexpacket = ByteArrayToString(e.Packet.Data);
                my_entity = hexpacket.Substring((c - 5) * 2, 8);
                return;
            }

            int[] playerMove = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("ReadClientMove"), 28);
            if (playerMove.Length > 0)
            {
                t = playerMove[0];
            }

            if (t > 28)
            {
                _fx = BitConverter.ToSingle(e.Packet.Data, t + 16);
                _fy = BitConverter.ToSingle(e.Packet.Data, t + 16 + 4);
                _fz = BitConverter.ToSingle(e.Packet.Data, t + 16 + 8);
                short tw = BitConverter.ToInt16(e.Packet.Data, t + 16 + 14);

                _fd = tw / 180;

                if (_fd < 0)
                {
                    _fd = -1 * _fd;
                }
                else
                {
                    _fd = 180 + (180 - _fd);
                }

                string hexpacket = ByteArrayToString(e.Packet.Data);
                entity = hexpacket.Substring((t - 5) * 2, 8);
                updateEntity = true;

                Player p = new Player
                {
                    x = _fx,
                    y = _fy,
                    z = _fz,
                    d = _fd
                };

                lock (player_lock)
                {
                    if (players.Contains(entity))
                    {
                        players[entity] = p;
                    }
                    else
                    {
                        players.Add(entity, p);
                    }
                }

                return;
            }

            if (chkActionBlip.Checked)
            {
                int[] isShooting = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("Action1B"), 28);
                if (isShooting.Length > 0)
                {
                    s = isShooting[0];
                }
                if (s > 28)
                {
                    shooting = true;
                    updated = true;
                    return;
                }
            }

            // Check for Death
            int[] isDead = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("deathscreen.reason"), 28);
            if (isDead.Length > 0)
            {
                j = isDead[0];
            }
            if (j > 28)
            {
                died = true;
                updated = true;
                return;
            }
            // Check for Position
            int[] clientMoved = e.Packet.Data.Locate(Encoding.ASCII.GetBytes("GetClientMove"), 28);
            if (clientMoved.Length > 0)
            {
                i = clientMoved[0];
            }

            if (i > 28)
            {
                Single x = BitConverter.ToSingle(e.Packet.Data, i + 15);
                Single y = BitConverter.ToSingle(e.Packet.Data, i + 15 + 4);
                Single z = BitConverter.ToSingle(e.Packet.Data, i + 15 + 8);
                //short f = BitConverter.ToInt16(e.Packet.Data, i + 15 + 12);
                short w = BitConverter.ToInt16(e.Packet.Data, i + 15 + 14);

                //Double deg = (Math.Sqrt(f * f + w * w) * Math.Cos(Math.Atan2(f, w))) / 182;
                Double deg = w / 180;

                if (deg < 0)
                {
                    deg = -1 * deg;
                }
                else
                {
                    deg = 180 + (180 - deg);
                }

                if (x == _x && y == _y && z == _z && deg == _direction && !died)
                {
                    if (last_updated + min_update_time >= timenow)
                    {
                        return;
                    }
                }

                last_updated = timenow;
                updated = true;
                _x = x;
                _y = y;
                _z = z;
                _direction = deg;

                return;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                sendForeignEntityThread.Abort();
                updateQMAP.Abort();
                dropThread.Abort();
                httpClient.CancelAsync();
                httpClient.Dispose();
                device.StopCapture();
                device.Close();
            }
            catch (Exception ex)
            {

            }
            lblStatus.ForeColor = System.Drawing.Color.Red;
            lblStatus.Text = "Not Running";
        }

        private void changeText(Control obj, string value)
        {
            if (obj.InvokeRequired)
            {
                obj.Invoke(new MethodInvoker(delegate
                {
                    obj.Text = value;
                }));
            }
        }

        private void saveBootstrapToken_CheckedChanged(object sender, EventArgs e)
        {

            string data = Crypto.EncryptStringAES(txtBootstrap.Text.Trim(), key);
            System.IO.File.WriteAllText(String.Format("{0}\\token.dat", AppDomain.CurrentDomain.BaseDirectory), data);
        }

        private void saveNetworkInterface()
        {
            System.IO.File.WriteAllText(
                String.Format("{0}\\if.dat", AppDomain.CurrentDomain.BaseDirectory),
                ((SharpPcap.WinPcap.WinPcapDevice)device).Interface.FriendlyName.ToString());
        }

        private string getSavedNetworkInterface()
        {
            try
            {
                string netif = System.IO.File.ReadAllText(String.Format("{0}\\if.dat", AppDomain.CurrentDomain.BaseDirectory));
                if (netif == null)
                {
                    return "";
                }
                return netif.Trim();
            }
            catch (Exception e)
            {
            }
            return "";
        }

        public string readTokenFile()
        {
            try
            {
                string encrypted = System.IO.File.ReadAllText(String.Format("{0}\\token.dat", AppDomain.CurrentDomain.BaseDirectory));
                if (encrypted == null)
                {
                    return "0";
                }
                encrypted = encrypted.Trim();
                string decrypted = Crypto.DecryptStringAES(encrypted, key);
                if (decrypted == null)
                {
                    return "";
                }
                return decrypted.ToString();
            }
            catch (Exception ex)
            {
            }
            return "";
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Application.Exit();
        }

        private void ExitApp(object sender, EventArgs e)
        {
            Process[] runningProcesses = Process.GetProcesses();
            foreach (var p in runningProcesses)
            {
                if (p.ProcessName.ToString().Equals("GPS"))
                {
                    p.Kill();
                }
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private double ConvertToTimestamp(DateTime value)
        {
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
            return (double)span.TotalSeconds;
        }

        private void btnPinit_Click(object sender, EventArgs e)
        {
            if (cboType.Text == "")
            {
                MessageBox.Show("You must select a waypoint type before adding this waypoint to the map.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                cboType.Focus();
                return;
            }

            foreach (var waypoint_type in waypoint_types)
            {
                if (waypoint_type != null)
                {
                    if (waypoint_type.id != null && waypoint_type.name != null)
                    {
                        if (cboType.Text == waypoint_type.name.ToString())
                        {
                            //waypoint_id = Convert.ToInt32(waypoint_type.id.ToString());
                            waypoint_id = int.Parse(waypoint_type.id.ToString(), CultureInfo.InvariantCulture);
                            if (txtName.Text != "")
                            {
                                waypoint_name = txtName.Text;
                            }
                            updated = true;
                            cboType.Text = "";
                            txtName.Text = "";
                        }
                    }
                }
            }
        }

    }
}