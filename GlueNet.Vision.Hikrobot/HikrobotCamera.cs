using GlueNet.Vision.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MvCamCtrl.NET;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Diagnostics.Eventing.Reader;
using System.Windows.Media.Media3D;
using System.Data.SqlClient;
using static MvCamCtrl.NET.MyCamera;
using System.IO.Packaging;

namespace GlueNet.Vision.Hikrobot
{
    public class HikrobotCamera : ICamera, IDisposable
    {
        private uint myNConvertDstBufLen = 0;
        private IntPtr myConvertDstBuf = IntPtr.Zero;
        private PixelFormat myBitmapPixelFormat = PixelFormat.DontCare;
        private CancellationTokenSource myCancelTokenSource = new CancellationTokenSource();
        private IntPtr myBufForDriver = IntPtr.Zero;
        private uint myNBufSizeForDriver = 0;
        private static Object myBufForDriverLock = new Object();

        public Bitmap Bitmap = null;

        public MyCamera.MV_FRAME_OUT_INFO_EX FrameInfo = new MyCamera.MV_FRAME_OUT_INFO_EX();
        public CameraInfo CameraInfo { get; private set; }
        public TriggerModes TriggerMode { get; set; }
        public MyCamera MyCamera { get; set; }
        public bool IsPlaying { get; set; }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        public event EventHandler<ICaptureCompletedArgs> CaptureCompleted;

        public void InitCamera(MyCamera myCamera, CameraInfo cameraInfo)
        {
            CameraInfo = cameraInfo;
            MyCamera = myCamera;
            OpenCamera((IntPtr)cameraInfo.Raw).Wait();    
            SetSoftTriggerMode();
            //SetContinuousMode();

            //int nRet = MyCamera.MV_CC_SetBoolValue_NET("ReverseX", false);
        }


        public void Dispose()
        {
            myCancelTokenSource.Cancel();
            MyCamera.MV_CC_CloseDevice_NET();
            MyCamera.MV_CC_DestroyDevice_NET();        
        }

        public float GetGain()
        {
            MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();
            MyCamera.MV_CC_GetGain_NET(ref stParam);
            return (float)Math.Round(stParam.fCurValue);
        }

        public void SetGain(float gain)
        {
            MyCamera.MV_CC_SetGain_NET(gain);
        }

        public void SetSoftTriggerMode()
        {
            TriggerMode = TriggerModes.SoftTrigger;
            MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
            int nRet = MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);
            if (nRet != MyCamera.MV_OK)
            {
                MessageBox.Show("Set SoftTrigger Mode Fail!");
            }
        }

        public void SetContinuousMode()
        {
            TriggerMode = TriggerModes.Continues;
            MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
        }

        public async void SoftTrigger()
        {
            int nRet = MyCamera.MV_CC_SetCommandValue_NET("TriggerSoftware");
            if (nRet != MyCamera.MV_OK)
            {
                MessageBox.Show("Capture Fail!");
            }
        }

        public void StartPlay()
        {
            int nRet = NecessaryOperBeforeGrab();
            if (nRet != MyCamera.MV_OK)
            {
                return;
            }

            FrameInfo.nFrameLen = 0; //清除帧长度
            FrameInfo.enPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_Undefined;

            myCancelTokenSource.Dispose();
            myCancelTokenSource = new CancellationTokenSource();
            Task Task1 = Task.Run(ReceiveThreadProcess2, myCancelTokenSource.Token);

            // ch:开始采集 | en:Start Grabbing
            nRet = MyCamera.MV_CC_StartGrabbing_NET();
            if (MyCamera.MV_OK != nRet)
            {
                myCancelTokenSource.Cancel();
                MessageBox.Show("Start Grabbing Fail!");
                return;
            }
        }

        public void StopPlay()
        {
            myCancelTokenSource.Cancel();
            MyCamera.MV_CC_StopGrabbing_NET();
        }

        private Int32 NecessaryOperBeforeGrab()
        {
            // ch:取图像宽 | en:Get Iamge Width
            MyCamera.MVCC_INTVALUE stWidth = new MyCamera.MVCC_INTVALUE();
            //int nRet = MyCamera.MV_CC_GetIntValueEx_NET("Width", ref stWidth);
            int nRet = MyCamera.MV_CC_GetWidth_NET(ref stWidth);
            if (MyCamera.MV_OK != nRet)
            {
                MessageBox.Show("Get Width Info Fail!");
                return nRet;
            }

            // ch:取图像高 | en:Get Iamge Height
            MyCamera.MVCC_INTVALUE_EX stHeight = new MyCamera.MVCC_INTVALUE_EX();
            nRet = MyCamera.MV_CC_GetIntValueEx_NET("Height", ref stHeight);
            if (MyCamera.MV_OK != nRet)
            {
                MessageBox.Show("Get Height Info Fail!");
                return nRet;
            }

            // ch:取像素格式 | en:Get Pixel Format
            MyCamera.MVCC_ENUMVALUE stPixelFormat = new MyCamera.MVCC_ENUMVALUE();
            nRet = MyCamera.MV_CC_GetEnumValue_NET("PixelFormat", ref stPixelFormat);
            if (MyCamera.MV_OK != nRet)
            {
                MessageBox.Show("Get Pixel Format Fail!");
                return nRet;
            }

            // ch:设置bitmap像素格式，申请相应大小内存 | en:Set Bitmap Pixel Format, alloc memory
            if ((Int32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Undefined == stPixelFormat.nCurValue)
            {
                MessageBox.Show("Unknown Pixel Format!");
                return MyCamera.MV_E_UNKNOW;
            }
            else if (IsMono(stPixelFormat.nCurValue))
            {
                myBitmapPixelFormat = PixelFormat.Format8bppIndexed;

                if (IntPtr.Zero != myConvertDstBuf)
                {
                    Marshal.Release(myConvertDstBuf);
                    myConvertDstBuf = IntPtr.Zero;
                }

                // Mono8为单通道
                myNConvertDstBufLen = (UInt32)(stWidth.nCurValue * stHeight.nCurValue);
                myConvertDstBuf = Marshal.AllocHGlobal((Int32)myNConvertDstBufLen);
                if (IntPtr.Zero == myConvertDstBuf)
                {
                    MessageBox.Show("Malloc Memory Fail!");
                    return MyCamera.MV_E_RESOURCE;
                }
            }
            else
            {
                myBitmapPixelFormat = PixelFormat.Format24bppRgb;

                if (IntPtr.Zero != myConvertDstBuf)
                {
                    Marshal.FreeHGlobal(myConvertDstBuf);
                    myConvertDstBuf = IntPtr.Zero;
                }

                // RGB为三通道
                myNConvertDstBufLen = (UInt32)(3 * stWidth.nCurValue * stHeight.nCurValue);
                myConvertDstBuf = Marshal.AllocHGlobal((Int32)myNConvertDstBufLen);
                if (IntPtr.Zero == myConvertDstBuf)
                {
                    MessageBox.Show("Malloc Memory Fail!");
                    return MyCamera.MV_E_RESOURCE;
                }
            }

            // 确保释放保存了旧图像数据的bitmap实例，用新图像宽高等信息new一个新的bitmap实例
            if (null != Bitmap)
            {
                Bitmap.Dispose();
                Bitmap = null;
            }
            Bitmap = new Bitmap((Int32)stWidth.nCurValue, (Int32)stHeight.nCurValue, myBitmapPixelFormat);

            // ch:Mono8格式，设置为标准调色板 | en:Set Standard Palette in Mono8 Format
            if (PixelFormat.Format8bppIndexed == myBitmapPixelFormat)
            {
                ColorPalette palette = Bitmap.Palette;
                for (int i = 0; i < palette.Entries.Length; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                Bitmap.Palette = palette;
            }

            
            return MyCamera.MV_OK;
        }

        private Boolean IsMono(UInt32 enPixelType)
        {
            switch (enPixelType)
            {
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono1p:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono2p:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono4p:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8_Signed:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono14:
                case (UInt32)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono16:
                    return true;
                default:
                    return false;
            }
        }

        public void RotateImage(MyCamera.MV_FRAME_OUT stFrameInfo, MV_IMG_ROTATION_ANGLE rotateAngle)
        {
            MyCamera.MV_CC_ROTATE_IMAGE_PARAM rotateImageInfo = new MyCamera.MV_CC_ROTATE_IMAGE_PARAM();

            rotateImageInfo.enPixelType = stFrameInfo.stFrameInfo.enPixelType;
            rotateImageInfo.nWidth = stFrameInfo.stFrameInfo.nWidth;
            rotateImageInfo.nHeight = stFrameInfo.stFrameInfo.nHeight;
            rotateImageInfo.pSrcData = stFrameInfo.pBufAddr;
            rotateImageInfo.nSrcDataLen = stFrameInfo.stFrameInfo.nFrameLen;
            rotateImageInfo.pDstBuf = myConvertDstBuf;
            rotateImageInfo.nDstBufLen = 0;
            rotateImageInfo.nDstBufSize = myNConvertDstBufLen;
            rotateImageInfo.enRotationAngle = rotateAngle;

            int success = MyCamera.MV_CC_RotateImage_NET(ref rotateImageInfo);
            //Console.WriteLine(success);          
        }

        public void ReceiveThreadProcess2()
        {
            MyCamera.MV_FRAME_OUT stFrameInfo = new MyCamera.MV_FRAME_OUT();
            MyCamera.MV_DISPLAY_FRAME_INFO stDisplayInfo = new MyCamera.MV_DISPLAY_FRAME_INFO();
            MyCamera.MV_PIXEL_CONVERT_PARAM stConvertInfo = new MyCamera.MV_PIXEL_CONVERT_PARAM();

            int nRet = MyCamera.MV_OK;

            while (myCancelTokenSource.Token.IsCancellationRequested == false)
            {
                //Console.WriteLine($"ThreadId:{Thread.CurrentThread.ManagedThreadId} , {DateTime.Now.ToString("hh:mm:ss.fff")}");

                nRet = MyCamera.MV_CC_GetImageBuffer_NET(ref stFrameInfo, 2500);
                if (nRet == MyCamera.MV_OK)
                {

                    lock (myBufForDriverLock)
                    {
                        //Console.WriteLine("MV_CC_GetImageBuffer_NET");
                        if (myBufForDriver == IntPtr.Zero || stFrameInfo.stFrameInfo.nFrameLen > myNBufSizeForDriver)
                        {
                            if (myBufForDriver != IntPtr.Zero)
                            {
                                Marshal.Release(myBufForDriver);
                                myBufForDriver = IntPtr.Zero;
                            }

                            myBufForDriver = Marshal.AllocHGlobal((Int32)stFrameInfo.stFrameInfo.nFrameLen);
                            if (myBufForDriver == IntPtr.Zero)
                            {
                                return;
                            }
                            myNBufSizeForDriver = stFrameInfo.stFrameInfo.nFrameLen;
                        }

                        FrameInfo = stFrameInfo.stFrameInfo;
                        CopyMemory(myBufForDriver, stFrameInfo.pBufAddr, stFrameInfo.stFrameInfo.nFrameLen);

                        // ch:转换像素格式 | en:Convert Pixel Format
                        stConvertInfo.nWidth = stFrameInfo.stFrameInfo.nWidth;
                        stConvertInfo.nHeight = stFrameInfo.stFrameInfo.nHeight;
                        stConvertInfo.enSrcPixelType = stFrameInfo.stFrameInfo.enPixelType;
                        stConvertInfo.pSrcData = stFrameInfo.pBufAddr;
                        stConvertInfo.nSrcDataLen = stFrameInfo.stFrameInfo.nFrameLen;
                        stConvertInfo.pDstBuffer = myConvertDstBuf;
                        stConvertInfo.nDstBufferSize = myNConvertDstBufLen;
                        if (PixelFormat.Format8bppIndexed == Bitmap.PixelFormat)
                        {
                            stConvertInfo.enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8;
                            int a = MyCamera.MV_CC_ConvertPixelType_NET(ref stConvertInfo);
                        }
                        else
                        {
                            stConvertInfo.enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
                            int a = MyCamera.MV_CC_ConvertPixelType_NET(ref stConvertInfo);
                        }

                        RotateImage(stFrameInfo, MV_IMG_ROTATION_ANGLE.MV_IMAGE_ROTATE_180);

                        try
                        {
                            // Save Bitmap Data
                            BitmapData bitmapData = Bitmap.LockBits(new Rectangle(0, 0, stConvertInfo.nWidth, stConvertInfo.nHeight), ImageLockMode.ReadWrite, Bitmap.PixelFormat);
                            
                            CopyMemory(bitmapData.Scan0, stConvertInfo.pDstBuffer, (UInt32)(bitmapData.Stride * Bitmap.Height));

                            Bitmap.UnlockBits(bitmapData);
                            
                            var args = new HikrobotCaptureCompletedArgs(new Bitmap(Bitmap));
                            
                            CaptureCompleted?.Invoke(this, args);
                          
                            //Bitmap.Dispose();

                            //if (TriggerMode == TriggerModes.SoftTrigger)
                            //{
                            //    var filename = $"{DateTime.Now:MMddyy_HHmmss}_test.bmp";
                                //Bitmap.Save(filename, ImageFormat.Bmp);
                            //}
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }

                    }
                    MyCamera.MV_CC_DisplayOneFrame_NET(ref stDisplayInfo);
                    MyCamera.MV_CC_FreeImageBuffer_NET(ref stFrameInfo);
                }
            }
        }

        public Task OpenCamera(IntPtr raw)
        {
            //for (int i = 0; i < deviceList.nDeviceNum; i++)
            //{
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(raw, typeof(MyCamera.MV_CC_DEVICE_INFO));
                MyCamera.MV_CC_FLIP_IMAGE_PARAM flip = (MyCamera.MV_CC_FLIP_IMAGE_PARAM)Marshal.PtrToStructure(raw, typeof(MyCamera.MV_CC_FLIP_IMAGE_PARAM));
                MyCamera = new MyCamera();

                int nRet = MyCamera.MV_CC_CreateDevice_NET(ref device);
                if (nRet != MyCamera.MV_OK)
                {
                    return Task.CompletedTask;
                }

                var connected = MyCamera.MV_CC_IsDeviceConnected_NET();
                if (!connected)
                {
                    nRet = MyCamera.MV_CC_OpenDevice_NET();
                    if (nRet != MyCamera.MV_OK)
                    {
                        myCancelTokenSource.Cancel();
                        MyCamera.MV_CC_DestroyDevice_NET();
                        MessageBox.Show("Device open failed!");
                        return Task.CompletedTask;
                }

                    var timeout = 500;
                    nRet = MyCamera.MV_CC_SetIntValueEx_NET("GevHeartbeatTimeout", timeout);
                    if (nRet != MyCamera.MV_OK)
                    {
                        MessageBox.Show("Set HeartbeatTimeout failed!");
                        return Task.CompletedTask;
                    }
                }

                nRet = MyCamera.MV_CC_FeatureLoad_NET("..\\CameraFile.mfs");
                if (MyCamera.MV_OK != nRet)
                {
                    Console.WriteLine("FeatureLoad failed!");
                }


                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    int nPacketSize = MyCamera.MV_CC_GetOptimalPacketSize_NET();
                    if (nPacketSize > 0)
                    {
                        nRet = MyCamera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", nPacketSize);
                        if (nRet != MyCamera.MV_OK)
                        {
                            MessageBox.Show("Set Packet Size failed!");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Get Packet Size failed!");
                    }
                }

                // ch:设置采集连续模式 | en:Set Continues Aquisition Mode
                MyCamera.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                MyCamera.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON);
                MyCamera.MV_CC_SetEnumValue_NET("TriggerSource", (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE);

                return Task.CompletedTask;
            //}
        }
    }
}
