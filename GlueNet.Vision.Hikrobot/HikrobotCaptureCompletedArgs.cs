using Emgu.CV;
using Emgu.CV.Structure;
using GlueNet.Vision.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MvCamCtrl.NET;
using Emgu.CV.CvEnum;
using System.Windows.Controls;

namespace GlueNet.Vision.Hikrobot
{
    public class HikrobotCaptureCompletedArgs : ICaptureCompletedArgs
    {
        public MyCamera.MV_FRAME_OUT_INFO_EX FrameInfo;
        public Bitmap Bitmap { get; set; }
        public object Raw { get; }

        public HikrobotCaptureCompletedArgs(Bitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public Mat GetMat()
        {
            var bitmap = (Bitmap)Bitmap.Clone();
            var image = new Image<Bgr, byte>(bitmap);
            var mat = image.Mat;
            var width = FrameInfo.nWidth;
            var height = FrameInfo.nHeight;
            var isGray = false;
            int channels = isGray ? 1 : 3;
            int stride = isGray ? width : width * 3;
            //return new Mat(height, width, DepthType.Cv8U, channels, ptrImageData, stride);
            return mat;
        }
    }
}
