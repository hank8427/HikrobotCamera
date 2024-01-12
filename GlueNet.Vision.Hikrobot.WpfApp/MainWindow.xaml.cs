using GlueNet.Vision.Core;
using MvCamCtrl.NET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
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

namespace GlueNet.Vision.Hikrobot.WpfApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public BitmapSource BitmapSource1 { get; set; }
        public BitmapSource BitmapSource2 { get; set; }
        public ICamera HikrobotCamera1 { get; set; }
        public ICamera HikrobotCamera2 { get; set; }
        public bool IsContinuous { get; set; } = true;
        public bool IsSoftTrigger { get; set; } = false;
        public float Gain { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            var factory = new HikrobotCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();
            if (cameraInfos.Count != 0)
            {
                HikrobotCamera1 = factory.CreateCamera(cameraInfos[0]);
                //HikrobotCamera2 = factory.CreateCamera(cameraInfos[1]);

                HikrobotCamera1.CaptureCompleted += HikrobotCamera1_CaptureCompleted;
                //HikrobotCamera2.CaptureCompleted += HikrobotCamera2_CaptureCompleted;
                this.Closing += Window_Closing;
                InitializeComponent();
                Gain = HikrobotCamera1.GetGain();
            }
        }

        private void OpenCameraOnClick(object sender, RoutedEventArgs e)
        {
            var factory = new HikrobotCameraFactory();
            var cameraInfos = factory.EnumerateCameras().ToList();
            if (cameraInfos.Count != 0)
            {
                HikrobotCamera1 = factory.CreateCamera(cameraInfos[0]);
                HikrobotCamera1.CaptureCompleted += HikrobotCamera1_CaptureCompleted;
                HikrobotCamera2 = factory.CreateCamera(cameraInfos[1]);
                HikrobotCamera2.CaptureCompleted += HikrobotCamera2_CaptureCompleted;
            }
        }

        private void HikrobotCamera1_CaptureCompleted(object sender, ICaptureCompletedArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e is HikrobotCaptureCompletedArgs args)
                {
                    var bmp = (Bitmap)args.Bitmap.Clone();
                    BitmapSource1 = ConvertToBitmapSource(bmp);                
                }                
            });
        }

        private void HikrobotCamera2_CaptureCompleted(object sender, ICaptureCompletedArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e is HikrobotCaptureCompletedArgs args)
                {
                    var bmp = (Bitmap)args.Bitmap.Clone();
                    BitmapSource2 = ConvertToBitmapSource(bmp);
                }
            });
        }

        public static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            BitmapSource result = BitmapSource.Create(bitmapData.Width, bitmapData.Height, bitmap.HorizontalResolution, bitmap.VerticalResolution, PixelFormats.Gray8, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);
            return result;
        }

        private void StopPlayOnClick(object sender, RoutedEventArgs e)
        {
            HikrobotCamera1.StopPlay();
            HikrobotCamera2.StopPlay();
        }

        private void CaptureOnClick(object sender, RoutedEventArgs e)
        {
            HikrobotCamera1.SoftTrigger();
            //Task.Delay(500).Wait();
            //HikrobotCamera2.SoftTrigger();
        }

        private void GetParmOnClick(object sender, RoutedEventArgs e)
        {
            Gain = HikrobotCamera1.GetGain();
        }

        private void SetParmOnClick(object sender, RoutedEventArgs e)
        {
            HikrobotCamera1.SetGain(Gain);
        }

        private void CloseCameraOnClick(object sender, RoutedEventArgs e)
        {
            HikrobotCamera1.Dispose();
            HikrobotCamera2.Dispose();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            HikrobotCamera1.Dispose();
            HikrobotCamera2.Dispose();
        }

        private void StartPlayOnClick(object sender, RoutedEventArgs e)
        {
            //HikrobotCamera2.StartPlay();
            HikrobotCamera1.StartPlay();         
        }

        private void TriggerMode_Checked(object sender, RoutedEventArgs e)
        {
            //IsSoftTrigger = true;
            //IsContinuous = false;
            
            //if (HikrobotCamera1 is HikrobotCamera camera)
            //{
            //    camera.SetSoftTriggerMode();
            //}
            //if (HikrobotCamera2 is HikrobotCamera camera2)
            //{
            //    camera2.SetSoftTriggerMode();
            //}
        }

        private void ContinuousMode_Checked(object sender, RoutedEventArgs e)
        {
            //IsSoftTrigger = false;
            //IsContinuous = true;

            //if (HikrobotCamera1 is HikrobotCamera camera)
            //{
            //    camera.SetContinuousMode();
            //}
            //if (HikrobotCamera2 is HikrobotCamera camera2)
            //{
            //    camera2.SetContinuousMode();
            //}
        }
    }
}
