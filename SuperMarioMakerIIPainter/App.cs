using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.Imaging.ColorReduction;
using System.IO.Ports;
using System.Threading;

namespace SuperMarioMakerIIPainter
{
    public class App
    {
        #region singletion
        private static App instance = null;
        public static App GetInstance()
        {
            if (instance == null)
            {
                instance = new App();
            }
            return instance;
        }
        #endregion

        public const int CANVAS_WIDTH = 320;
        public const int CANVAS_HEIGHT = 180;

        public const int BTN_HOLD_TIME = 10;
        public const int BTN_RELEASE_WAIT_TIME = 10;

        private Color[] palette = null;
        private Dictionary<Color, int> colorMap = null;
        private int selectedPaletteIndex = 0;
        private SerialPort serialPort = null;

        // event
        public delegate void FalseRespondHandler(byte respond);
        public event FalseRespondHandler OnRespondError;

        public bool InSync { get; set; } = false;
        public bool CanUse { get; set; } = true;

        private App()
        {
            palette = new string[]
            {
                "fe0000", // left - red
                "bc0000",
                "fff5d1",
                "ad8047",
                "ffff00",
                "fec200",
                "00ff01",
                "00bc00",
                "00ffff",
                "0000fe",
                "bc62ff",
                "8800bc",
                "ffc2fe",
                "bc008a",
                "bcbcbc",
                "010000", //black initial selected
                "ffffff"
            }.Select(str => ColorTranslator.FromHtml($"#{str}")).ToArray();
            selectedPaletteIndex = palette.Length - 2;

            colorMap = new Dictionary<Color, int>();
            for (int i = 0; i < palette.Length; i++)
            {
                colorMap[palette[i]] = i;
            }

            serialPort = new SerialPort
            {
                PortName = "COM3",
                Parity = Parity.None,
                BaudRate = 19200, // Use by this firmware
                DataBits = 8,
                StopBits = StopBits.One
            };
            serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);
        }

        public Bitmap Convert(Bitmap source, Size size)
        {
            if (source == null)
            {
                return null;
            }

            Bitmap result = source.Clone(System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            if (result.Size != size)
            {
                if (size.Width >= result.Width && size.Height >= result.Height)
                {
                    size = result.Size; // no change
                }
                else
                {
                    var ratioWidth = (double)size.Width / result.Width;
                    var ratioHeight = (double)size.Height / result.Height;

                    var ratio = Math.Min(ratioWidth, ratioHeight);
                    size = new Size((int)(result.Width * ratio), (int)(result.Height * ratio));

                    ResizeBicubic filter = new ResizeBicubic(size.Width, size.Height);
                    result = filter.Apply(result);
                }
            }

            ColorImageQuantizer colorImageQuantizer = new ColorImageQuantizer(new MedianCutQuantizer());
            BurkesColorDithering dither = new BurkesColorDithering
            {
                ColorTable = palette
            };
            result = dither.Apply(result);

            return result;
        }

        public bool OpenPort()
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort.Open();
                }
                if (!serialPort.IsOpen)
                {
                    throw new Exception($"Could not open {serialPort.PortName}");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return true;
        }

        public Task Sync()
        {
            if (!OpenPort())
            {
                return Task.CompletedTask;
            }

            if (InSync)
            {
                return Task.CompletedTask;
            }

            CanUse = false;

            q.Clear();

            return Task.Run(() =>
            {
                while (!InSync)
                {
                    // So-called sync...
                    // Don't know why send 9 x SYNC_START, from their own example
                    byte[] packet = new byte[9];
                    for (int i = 0; i < packet.Length; i += 1)
                    {
                        packet[i] = (byte)SyncCommand.SYNC_START;
                    }
                    byte respond = WriteBytesToSerial(packet);
                    if (respond != (byte)RespondCode.RESP_SYNC_START)
                    {
                        continue;
                    }

                    respond = WriteBytesToSerial(new byte[] { (byte)SyncCommand.SYNC_1 });
                    if (respond != (byte)RespondCode.RESP_SYNC_1)
                    {
                        continue;
                    }

                    respond = WriteBytesToSerial(new byte[] { (byte)SyncCommand.SYNC_2 });
                    if (respond != (byte)RespondCode.RESP_SYNC_OK)
                    {
                        continue;
                    }

                    InSync = true;
                }
            });
        }

        public void Match()
        {
            CanUse = false;
            Task.Run(() =>
            {
                Sync().Wait();

                Thread.Sleep(200);
                SendButtonCommand(Command.BTN_LR);
                Thread.Sleep(200);
                SendButtonCommand(Command.None);
                Thread.Sleep(200);
                SendButtonCommand(Command.BTN_A);
                Thread.Sleep(200);
                SendButtonCommand(Command.None);
                Thread.Sleep(200);

                CanUse = true;
            });
        }

        public void StartWrite(Bitmap bitmap)
        {
            OpenPort();

            Task.Run(() =>
            {
                bool running = true;
                bool isIncr = true;
                int deltaX = 0, deltaY = 0;

                int x = 0;
                int y = 0;
                selectedPaletteIndex = palette.Length - 2;

                while(!CanUse)
                {
                    Thread.Sleep(1);
                }

                Sync().Wait();
                q.Clear();

                // Move from center to top left
                int stepsX = bitmap.Width / 2;
                int stepsY = bitmap.Height / 2;

                while (stepsX > 0 && stepsY > 0)
                {
                    SendButtonCommand(Command.DPAD_U);
                    Thread.Sleep(BTN_HOLD_TIME);
                    SendButtonCommand(Command.DPAD_L);
                    Thread.Sleep(BTN_HOLD_TIME);
                    SendButtonCommand(Command.None); // Release
                    Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    stepsX -= 1;
                    stepsY -= 1;
                }
                while (stepsX > 0)
                {
                    SendButtonCommand(Command.DPAD_L);
                    Thread.Sleep(BTN_HOLD_TIME);
                    SendButtonCommand(Command.None); // Release
                    Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    stepsX -= 1;
                }
                while (stepsY > 0)
                {
                    SendButtonCommand(Command.DPAD_U);
                    Thread.Sleep(BTN_HOLD_TIME);
                    SendButtonCommand(Command.None); // Release
                    Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    stepsY -= 1;
                }
                

                while (running)
                {
                    if ((isIncr && x >= bitmap.Width) || (!isIncr && x < 0))
                    {
                        y += 1;
                        deltaY = 1;

                        isIncr = !isIncr;

                        x = isIncr ? 0 : bitmap.Width - 1;
                        deltaX = 0; // no x movement
                    }
                    if (y >= bitmap.Height)
                    {
                        running = false;
                        continue;
                    }

                    // Move to position
                    if (deltaX != 0)
                    {
                        //Console.WriteLine(deltaX < 0 ? "Left" : "Right");
                        SendButtonCommand(deltaX < 0 ? Command.DPAD_L : Command.DPAD_R);
                        Thread.Sleep(BTN_HOLD_TIME);
                        SendButtonCommand(Command.None); // Release
                        Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    }
                    if (deltaY != 0)
                    {
                        //Console.WriteLine(deltaY < 0 ? "Up" : "Down");
                        SendButtonCommand(deltaY < 0 ? Command.DPAD_U : Command.DPAD_D);
                        Thread.Sleep(BTN_HOLD_TIME);
                        SendButtonCommand(Command.None); // Release
                        Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    }

                    Color c = bitmap.GetPixel(x, y);
                    int slot = colorMap[c];

                    // Choose Color
                    int stepsLeft = slot <= selectedPaletteIndex ? selectedPaletteIndex - slot : palette.Length - (slot - selectedPaletteIndex);
                    int stepsRight = slot >= selectedPaletteIndex ? slot - selectedPaletteIndex : palette.Length - (selectedPaletteIndex - slot);

                    bool goLeft = stepsLeft < stepsRight;
                    int steps = Math.Min(stepsLeft, stepsRight);
                    for (int i = 0; i < steps; i += 1)
                    {
                        //Console.WriteLine(goLeft ? "Color Left" : "Color Right");
                        SendButtonCommand(goLeft ? Command.BTN_ZL : Command.BTN_ZR);
                        Thread.Sleep(BTN_HOLD_TIME);
                        SendButtonCommand(Command.None); // Release
                        Thread.Sleep(BTN_RELEASE_WAIT_TIME);
                    }
                    selectedPaletteIndex = slot; // Update current selected

                    // Draw - Press A
                    //Console.WriteLine("Draw!");
                    SendButtonCommand(Command.BTN_A);
                    Thread.Sleep(BTN_HOLD_TIME);
                    SendButtonCommand(Command.None); // Release
                    Thread.Sleep(BTN_RELEASE_WAIT_TIME);

                    deltaX = isIncr ? 1 : -1;
                    x += deltaX;
                    deltaY = 0;

                    Thread.Sleep(1);
                }
                SendButtonCommand(Command.None); // Release
                Thread.Sleep(BTN_RELEASE_WAIT_TIME);

                CanUse = true;
            });
        }

        #region Serial Port
        enum RespondCode
        {
            RESP_SYNC_START = 0xFF,
            RESP_SYNC_1 = 0xCC,
            RESP_SYNC_OK = 0x33,

            RESP_USB_ACK = 0x90,
            RESP_UPDATE_ACK = 0x91,
            RESP_UPDATE_NACK = 0x92,

        }
        enum SyncCommand
        {
            None = 0,
            SYNC_1 = 0X33,
            SYNC_2 = 0xCC,
            SYNC_START = 0xFF
        }
        enum Command
        {
            None = 0,

            BTN_A = 0x0000000000000004,
            BTN_L = 0x0000000000000010,
            BTN_R = 0x0000000000000020,
            BTN_LR = 0x0000000000000010 | 0x0000000000000020,
            BTN_ZL = 0x0000000000000040,
            BTN_ZR = 0x0000000000000080,

            DPAD_U = 0x0000000000010000,
            DPAD_R = 0x0000000000020000,
            DPAD_D = 0x0000000000040000,
            DPAD_L = 0x0000000000080000,
        }
        enum SwitchDPad
        {
            DPAD_CENTER = 0x08,
            DPAD_L = 0x06,
            DPAD_R = 0x02,
            DPAD_U = 0x00,
            DPAD_D = 0x04
        }
        private AutoResetEvent evt = new AutoResetEvent(false); // Init to unsignaled
        private Queue<byte> q = new Queue<byte>();

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private Point Angle(double angle, int indensity)
        {
            int x = (int)(Math.Cos(DegreeToRadian(angle) * 0x7F) * indensity / 0xFF) + 0x80;
            int y = (int)(Math.Sin(DegreeToRadian(angle) * 0x7F) * indensity / 0xFF) + 0x80;
            return new Point(x, y);
        }

        private int CRC8(int crc, byte b)
        {
            int result = crc ^ b;

            for(int i = 0; i < 8; i += 1)
            {
                if ((result & 0x80) != 0)
                {
                    result <<= 1;
                    result ^= 0x07;
                }
                else
                {
                    result <<= 1;
                }
                result &= 0xFF;
            }
            return result;
        }

        private byte ToSwitchDPad(byte dpad)
        {
            switch (dpad)
            {
                case (byte)((int)Command.DPAD_U >> 16 & 0xFF):
                    return (byte)SwitchDPad.DPAD_U;
                case (byte)((int)Command.DPAD_R >> 16 & 0xFF):
                    return (byte)SwitchDPad.DPAD_R;
                case (byte)((int)Command.DPAD_D >> 16 & 0xFF):
                    return (byte)SwitchDPad.DPAD_D;
                case (byte)((int)Command.DPAD_L >> 16 & 0xFF):
                    return (byte)SwitchDPad.DPAD_L;
                default:
                    return (byte)SwitchDPad.DPAD_CENTER;
            }
        }

        private byte GetByte(bool discardAll = true)
        {
            if (q.Count < 1)
            {
                return 0;
            }

            if (discardAll)
            {
                byte b = 0;
                while (q.Count > 0)
                {
                    b = q.Dequeue();
                }
                return b;
            }
            else
            {
                return q.Dequeue();
            }
        }

        private byte WriteBytesToSerial(byte[] bytes)
        {
            serialPort.Write(bytes, 0, bytes.Length);
            evt.WaitOne();
            byte respond = GetByte();
            return respond;
        }

        private bool SendBytes(byte[] bytes)
        {
            List<byte> byteList = new List<byte>();
            byteList.AddRange(bytes);

            // CRC
            int crc = 0;
            foreach (byte b in bytes)
            {
                crc = CRC8(crc, b);
            }
            byteList.Add((byte)crc);

            byte respond = WriteBytesToSerial(byteList.ToArray());
            if (respond == (byte)RespondCode.RESP_USB_ACK)
            {
                return true;
            }
            else
            {
                OnRespondError?.Invoke(respond);
                return false;
            }
        }

        private bool SendButtonCommand(Command cmd)
        {
            int cmdByte = (int)cmd;

            byte low = (byte)(cmdByte & 0xFF);
            cmdByte >>= 8;
            byte high = (byte)(cmdByte & 0xFF);
            cmdByte >>= 8;
            byte dpad = ToSwitchDPad((byte)(cmdByte & 0xFF));
            cmdByte >>= 8;

            var left = Angle(0, 0);
            var right = Angle(0, 0);

            byte[] bytes = new byte[]
            {
                high, low, dpad, (byte)left.X, (byte)left.Y, (byte)right.X, (byte)right.Y, 0x00 // left_x, left_y, right_x, right_y are all ignored...
            };

            return SendBytes(bytes);
        }
        #endregion

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int cnt = serialPort.BytesToRead;
            for (int i = 0; i < cnt; i += 1)
            {
                q.Enqueue((byte)serialPort.ReadByte());
            }
            evt.Set();
        }
    }
}
