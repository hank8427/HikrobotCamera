using Emgu.CV.Dnn;
using Emgu.CV.Ocl;
using GlueNet.Vision.Core;
using MvCamCtrl.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace GlueNet.Vision.Hikrobot
{
    public class HikrobotCameraFactory : ICameraFactory
    {
        private MyCamera.MV_CC_DEVICE_INFO_LIST myDeviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
        private List<CameraInfo> myCameraInfoList { get; set; } = new List<CameraInfo>();

        public IEnumerable<CameraInfo> EnumerateCameras()
        {
            var cameraInfoList = EnumCamera();

            return cameraInfoList;
        }

        public ICamera CreateCamera(CameraInfo cameraInfo)
        {
            var camera = new HikrobotCamera();
            camera.InitCamera(new MyCamera(),cameraInfo);
            return camera;
        }

        public ICamera CreateCamera(string serial)
        {
            //var cameraInfos = EnumerateCameras().ToList();
            //var find = cameraInfos.FirstOrDefault(x => x.SerialNumber == serial);

            //if (find != null)
            //{
            //    return CreateCamera(find);
            //}

            throw new Exception($"Serial [{serial}] not found.");
        }

        public List<CameraInfo> EnumCamera()
        {
            GC.Collect();

            myDeviceList.nDeviceNum = 0;
            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref myDeviceList);
            if (nRet!=MyCamera.MV_OK)
            {
                MessageBox.Show("Enumerate devices fail!");
            }

            for (int i = 0; i < myDeviceList.nDeviceNum; i++)
            {
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(myDeviceList.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    MyCamera.MV_GIGE_DEVICE_INFO gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));

                    if (gigeInfo.chUserDefinedName != "")
                    {
                        myCameraInfoList.Add(new CameraInfo(typeof(HikrobotCameraFactory).ToString()
                                                            , gigeInfo.chUserDefinedName
                                                            , gigeInfo.chSerialNumber
                                                            , myDeviceList.pDeviceInfo[i]));
                    }
                }              
            }
            return myCameraInfoList;
        }      
    }
}
