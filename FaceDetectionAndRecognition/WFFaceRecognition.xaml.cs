using System;
using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Timers;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace FaceDetectionAndRecognition
{

    public partial class WFFaceRecognition : Window, INotifyPropertyChanged
    {
        #region Properties
        public event PropertyChangedEventHandler PropertyChanged;
        private Capture videoCapture;
        private HaarCascade haarCascade;
        private Image<Bgr, Byte> bgrFrame = null;
        private Image<Gray, Byte> detectedFace = null;
        private List<FaceData> faceList = new List<FaceData>();
        private List<Image<Gray, Byte>> imageList = new List<Image<Gray, byte>>();
        private List<string> lList = new List<string>();
        private Timer captureTimer;
        #region FaceName
        private string faceName;
        public string FaceName
        {
            get { return faceName; }
            set
            {
                faceName = value.ToUpper();
                lblFaceName.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { lblFaceName.Content = faceName; }));
                NotifyPropertyChanged();
            }
        }
        #endregion
        #region CameraCaptureImage
        private Bitmap cameraCapture;
        public Bitmap CameraCapture
        {
            get { return cameraCapture; }
            set
            {
                cameraCapture = value;
                imgCamera.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { imgCamera.Source = BitmapToImageSource(cameraCapture); }));
                NotifyPropertyChanged();
            }
        }
        #endregion
        #region CameraCaptureFaceImage
        private Bitmap cameraCaptureFace;
        public Bitmap CameraCaptureFace
        {
            get { return cameraCaptureFace; }
            set
            {
                cameraCaptureFace = value;
                imgDetectFace.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => { imgDetectFace.Source = BitmapToImageSource(cameraCaptureFace); }));
                // imgCamera.Source = BitmapToImageSource(cameraCapture);
                NotifyPropertyChanged();
            }
        }
        #endregion
        #endregion

        #region Constructor
        public WFFaceRecognition()
        {
            InitializeComponent();
            captureTimer = new Timer()
            {
                Interval = Config.TimerResponseValue

            };
            captureTimer.Elapsed += CaptureTimer_Elapsed;
        }
        #endregion

        #region Event
        protected virtual void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GetFacesList();
            videoCapture = new Capture(Config.ActiveCameraIndex);
            videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FPS, 30);
            videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FRAME_HEIGHT, 450);
            videoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FRAME_WIDTH, 370);
            captureTimer.Start();
        }
        private void CaptureTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessFrame();
        }
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            WFAbout wfAbout = new WFAbout();
            wfAbout.ShowDialog();
        }
        private void NewFaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (detectedFace == null)
            {
                MessageBox.Show("No face detected.");
                return;
            }
            //Save detected face
            detectedFace = detectedFace.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
            detectedFace.Save(Config.FacePhotosPath +"face"+ (faceList.Count + 1) + Config.ImageFileExtension);
            StreamWriter writer = new StreamWriter(Config.FaceListTextFile, true);
            string personName = Microsoft.VisualBasic.Interaction.InputBox("Your Name");
            writer.WriteLine(String.Format("face{0}:{1}", (faceList.Count + 1), personName));
            writer.Close();
            GetFacesList();
            MessageBox.Show("Succesfull.");
        }
        private void OpenVideoFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            if (openDialog.ShowDialog().Value == true)
            {
                captureTimer.Stop();
                videoCapture.Dispose();

                videoCapture = new Capture(openDialog.FileName);
                captureTimer.Start();
                this.Title = openDialog.FileName;
                return;
            }



        }
        #endregion

        #region Method
        public void GetFacesList()
        {
            //haar cascade classifier
            haarCascade = new HaarCascade(Config.HaarCascadePath);
            faceList.Clear();
            string line;
            FaceData faceInstance = null;
            //split face text file
            StreamReader reader = new StreamReader(Config.FaceListTextFile);
            while ((line = reader.ReadLine()) != null)
            {
                string[] lineParts = line.Split(':');
                faceInstance = new FaceData();
                faceInstance.FaceImage = new Image<Gray, byte>(Config.FacePhotosPath + lineParts[0] + Config.ImageFileExtension);
                faceInstance.PersonName = lineParts[1];
                faceList.Add(faceInstance);
            }
            foreach (var face in faceList)
            {
                imageList.Add(face.FaceImage);
                lList.Add(face.PersonName);
            }
            reader.Close();
        }

        private void ProcessFrame()
        {
            bgrFrame = videoCapture.QueryFrame();
                  
            if (bgrFrame != null)
            {
                try
                {//for emgu cv bug
                    Image<Gray, byte> grayframe = bgrFrame.Convert<Gray, byte>();
                    //detect face
                    MCvAvgComp[][] faces = grayframe.DetectHaarCascade(haarCascade, 1.2, 10, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new System.Drawing.Size(20, 20));

                    FaceName = "No face detected";
                    foreach (var face in faces[0])
                    {
                        bgrFrame.Draw(face.rect, new Bgr(255, 255, 0), 2);
                        detectedFace = bgrFrame.Copy(face.rect).Convert<Gray, byte>();
                        FaceRecognition();
                        break;
                    }
                    CameraCapture = bgrFrame.ToBitmap();
                }
                catch (Exception ex)
                {

                    //todo log
                }

            }
        }

        private void FaceRecognition()
        {
            if (imageList.ToArray().Length != 0)
            {
                MCvTermCriteria termCrit = new MCvTermCriteria(lList.Count, 0.001);
                //Eigen Face Algorithm
                EigenObjectRecognizer recognizer = new EigenObjectRecognizer(imageList.ToArray(), lList.ToArray(), 3000, ref termCrit);
                string faceName = recognizer.Recognize(detectedFace.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC));
                FaceName = faceName;
                CameraCaptureFace = detectedFace.ToBitmap();
            }
            else
            {
                FaceName = "Please Add Face";
            }
        }
        /// <summary>
        /// Convert bitmap to bitmap image for image control
        /// </summary>
        /// <param name="bitmap">Bitmap image</param>
        /// <returns>Image Source</returns>
        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        #endregion


    }
}
