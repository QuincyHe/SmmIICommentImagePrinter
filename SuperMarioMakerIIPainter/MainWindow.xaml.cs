using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Drawing;
using System.IO;

namespace SuperMarioMakerIIPainter
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Bitmap bitmap = null;

        public bool CanUse
        {
            //get { return App.GetInstance().CanUse; }
            get { return true; }
        }

        public MainWindow()
        {
            InitializeComponent();

            App.GetInstance().OnRespondError += MainWindow_OnRespondError;
        }

        private void MainWindow_OnRespondError(byte respond)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                Console.WriteLine($"Wrong response: {respond}");
            }));
        }

        private void ProcessImage(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }

            using (Bitmap bmp = new Bitmap(filename))
            {
                App app = App.GetInstance();

                bitmap = app.Convert(bmp, new System.Drawing.Size(App.CANVAS_WIDTH, App.CANVAS_HEIGHT));

                BitmapImage image = null;
                // Display Bitmap, Oh man...
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Position = 0;
                    image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = ms;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                }
                imgBr.ImageSource = image;
            }
        }

        private void BtnOpenPort_Click(object sender, RoutedEventArgs e)
        {
            //App.GetInstance().OpenPort();
        }

        private void CmdLoad_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = App.GetInstance().CanUse;
        }

        private void CmdLoad_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "All files (*.*)|*.*|JPG Files (*.jpg)|*.jpg|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|GIF Files (*.gif)|*.gif" };
            if (ofd.ShowDialog() != false)
            {
                ProcessImage(ofd.FileName);
            }
        }

        private void CmdMatch_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            App.GetInstance().Match();
        }

        private void CmdExecute_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (bitmap == null)
                return;

            App.GetInstance().StartWrite(bitmap);
        }
    }

    public static class Command
    {
        public static RoutedUICommand CmdLoad = new RoutedUICommand("load", "load", typeof(MainWindow));
        public static RoutedUICommand CmdMatch = new RoutedUICommand("match", "match", typeof(MainWindow));
        public static RoutedUICommand CmdExecute = new RoutedUICommand("execute", "execute", typeof(MainWindow));
    }
}
