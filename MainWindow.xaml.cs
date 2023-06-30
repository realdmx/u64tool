using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace u64tool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string lastFile = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.wset)
            {
                Left = Properties.Settings.Default.wleft;
                Top = Properties.Settings.Default.wtop;
                Width = Properties.Settings.Default.wwidth;
                Height = Properties.Settings.Default.wheight;
            }
            else
            {
                Rect desktopWorkingArea = SystemParameters.WorkArea;
                Left = desktopWorkingArea.Right - Width;
                Top = desktopWorkingArea.Bottom - Height;
            }

            tbFile.Text = Properties.Settings.Default.LastLoadedFile;
        }

        private async Task HandleDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                string file = droppedFiles.First();
                _ = await RunFile(file);
            }
        }

        private async Task<bool> RunFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                lastFile = filePath;

                tbFile.Text = lastFile;

                byte[] binary = await System.IO.File.ReadAllBytesAsync(filePath);
                _ = await SendCommand(SocketCommand.DMARUN, binary, WaitReply: false);

                Properties.Settings.Default.LastLoadedFile = filePath;

                return true;
            }
            lastFile = string.Empty;
            return false;
        }

        private async Task Reset()
        {
            _ = await SendCommand(SocketCommand.RESET, null, WaitReply: false);
        }

        public enum SocketCommand
        {
            DMA = 65281,
            DMARUN = 65282,
            KEYB = 65283,
            RESET = 65284,
            WAIT = 65285,
            DMAWRITE = 65286,
            REUWRITE = 65287,
            KERNALWRITE = 65288,
            DMAJUMP = 65289,
            MOUNTIMG = 65290,
            RUNIMG = 65291,
            LOADSIDCRT = 65393,
            LOADBOOTCRT = 65394,
            READMEM = 65396,
            READFLASH = 65397,
            DEBUGREG = 65398
        }


        private static int SOCKET_RECEIVE_TIMEOUT = 1000;

        private static async Task<byte[]> ReceiveMessage(Socket socket, int messageSize)
        {
            int num = 1024;
            byte[] array = new byte[messageSize];
            int num2;
            int num3 = 0;
            do
            {
                byte[] array2 = new byte[num];
                int size = Math.Min(messageSize - num3, num);
                num2 = await socket.ReceiveAsync(array2, SocketFlags.None);
                Buffer.BlockCopy(array2, 0, array, num3, num2);
                num3 += num2;
                if (num3 == messageSize)
                {
                    break;
                }
                Console.WriteLine("Received: " + num3);
            }
            while (num2 > 0);

            if (num3 < messageSize)
            {
                throw new Exception("Server closed connection prematurely");
            }
            return array;
        }


        private static async Task<byte[]> GetReply(Socket sock)
        {
            byte[] array = new byte[8388608];
            try
            {
                sock.ReceiveTimeout = SOCKET_RECEIVE_TIMEOUT;
                return await ReceiveMessage(sock, array.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ultimate64: Cannot receive reply: " + ex.Message);
                return Array.Empty<byte>();
            }
        }

        private static async Task<byte[]> SendCommand(SocketCommand Command, byte[] data, bool WaitReply)
        {
            string hostname = Properties.Settings.Default.U64IP;

            if (string.IsNullOrEmpty(hostname))
            {
                return Array.Empty<byte>();
            }

            int port = 64;
            using Socket socket = new(SocketType.Stream, ProtocolType.Tcp);
            if (data == null)
            {
                data = Array.Empty<byte>();
            }
            byte[] result = null;
            try
            {
                int length = data.Length;
                ushort cmd = (ushort)Command;

                await socket.ConnectAsync(hostname, port);

                if (socket.Connected)
                {
                    byte[] array = new byte[4 + data.Length];
                    array[0] = (byte)(cmd & 0xff);
                    array[1] = (byte)((cmd & 0xff00) >> 8);
                    array[2] = (byte)(length & 0xff);
                    array[3] = (byte)((length & 0xff00) >> 8);
                    Array.Copy(data, 0, array, 4, data.Length);
                    _ = await socket.SendAsync(array, SocketFlags.None);
                    if (WaitReply)
                    {
                        result = await GetReply(socket);
                    }
                    return result;
                }
                socket.Close();
                throw new ApplicationException("Failed to connect - Check IP Address.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ultimate64: Cannot send command: " + ex.Message);
                return Array.Empty<byte>();
            }
        }

        private void bExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void bReset_Click(object sender, RoutedEventArgs e)
        {
            await Reset();
        }

        private async void bLoadRun_Click(object sender, RoutedEventArgs e)
        {
            _ = await RunFile(tbFile.Text);
        }

        private void bConfig_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            await HandleDrop(sender, e);
        }

        private void tbFile_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.wleft = Left;
            Properties.Settings.Default.wtop = Top;
            Properties.Settings.Default.wwidth = Width;
            Properties.Settings.Default.wheight = Height;
            Properties.Settings.Default.wset = true;
            Properties.Settings.Default.Save();
        }
    }


}
