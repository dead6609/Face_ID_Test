using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Face;
using Emgu.CV.CvEnum;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Xml.Linq;

namespace Face_ID_Test
{
    public partial class Form1 : Form
    {
        #region Variables
        private VideoCapture videoCapture = null;
        private Image<Bgr, byte> currentFrame = null;
        Mat frame = new Mat();
        private bool facesDetectionEnabled = false;
        CascadeClassifier faceCascadeClassfier = new CascadeClassifier("haarcascade_frontalface_alt.xml");
        List<Mat> TrainFaces = new List<Mat>();
        List<int> PersonsLabes = new List<int>();
        bool EnableSaveImage = false;
        private bool isTrained = false;
        EigenFaceRecognizer recognizer;
        List<string> PersonsNames = new List<string>();
        #endregion


        public Form1()
        {
            InitializeComponent();
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            //Захват камеры
            if (videoCapture != null) videoCapture.Dispose();
            videoCapture = new VideoCapture();
            Application.Idle += ProcessFrame;

        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            
            if (videoCapture != null && videoCapture.Ptr != IntPtr.Zero)
            {
                videoCapture.Read(frame);
                currentFrame = frame.ToImage<Bgr, byte>().Resize(picCapture.Width, picCapture.Height, Inter.Cubic);

                //Нахождение лица
                if (facesDetectionEnabled)
                {

                    //Для Машинного глаза
                    Mat grayImage = new Mat();
                    CvInvoke.CvtColor(currentFrame, grayImage, ColorConversion.Bgr2Gray);
                    //Увеличение изо
                    CvInvoke.EqualizeHist(grayImage, grayImage);

                    Rectangle[] faces = faceCascadeClassfier.DetectMultiScale(grayImage, 1.1, 3, Size.Empty, Size.Empty);
                    
                    if (faces.Length > 0)
                    {

                        foreach (var face in faces)
                        {
                            CvInvoke.Rectangle(currentFrame, face, new Bgr(Color.Red).MCvScalar, 2);
                            //Добавление пользователя
                            //Поместить в лицевую картину
                            Image<Bgr, Byte> resultImage = currentFrame.Convert<Bgr, Byte>();
                            resultImage.ROI = face;
                            picDetected.SizeMode = PictureBoxSizeMode.StretchImage;
                            picDetected.Image = resultImage.ToBitmap();
                            

                            if (EnableSaveImage)
                            {
                                //Задача папки с фотографиями
                                string path = Directory.GetCurrentDirectory() + @"\DetectFace";
                                if (!Directory.Exists(path))
                                    Directory.CreateDirectory(path);
                                 /*
                                //Таск с фотками для обучения (попробовать сделать resultImage динамически, заставить в конце кнопку включиться)
                                Task.Factory.StartNew(() => {
                                    for (int i = 0; i < 10; i++)
                                    {
                                        //resize the image then saving it
                                        resultImage.Resize(300, 300, Inter.Cubic).Save(path + @"\" + txtPersonName.Text + "_" + DateTime.Now.ToString("dd-mm-yyyy-hh-mm-ss") + ".bmp");
                                        Thread.Sleep(1000);
                                    }
                                    //btnAddPerson.Enabled = true;
                                });
                                 */
                                resultImage.Resize(300, 300, Inter.Cubic).Save(path + @"\" + txtPersonName.Text + "_" + DateTime.Now.ToString("dd-mm-yyyy-hh-mm-ss") + ".bmp");
                                btnAddPerson.Enabled = true;

                            }
                            EnableSaveImage = false;
                           

                            if (btnAddPerson.InvokeRequired)
                            {
                                btnAddPerson.Invoke(new ThreadStart(delegate {
                                    btnAddPerson.Enabled = true;
                                }));
                            }

                            // Распознование лица СМОТРЕТЬ СЮДА!!!
                            if (isTrained)
                            {
                                Image<Gray, Byte> grayFaceResult = resultImage.Convert<Gray, Byte>().Resize(300, 300, Inter.Cubic);
                                CvInvoke.EqualizeHist(grayFaceResult, grayFaceResult);
                                var result = recognizer.Predict(grayFaceResult);
                                pictureBox1.Image = grayFaceResult.Resize(pictureBox1.Width,pictureBox1.Height, Inter.Cubic).ToBitmap();
                                Debug.WriteLine(result.Label + ". " + result.Distance);
                                
                                
                                //Известные лица
                                if (result.Label != -1 && result.Distance < 3000)
                                {
                                    CvInvoke.PutText(currentFrame, PersonsNames[result.Label], new Point(face.X - 2, face.Y - 2),
                                        FontFace.HersheyComplex, 1.0, new Bgr(Color.Orange).MCvScalar);
                                    CvInvoke.Rectangle(currentFrame, face, new Bgr(Color.Green).MCvScalar, 2);
                                    pictureBox2.Image = TrainFaces[result.Label].ToBitmap();
                                }
                                //Неизвестные
                                else
                                {
                                    CvInvoke.PutText(currentFrame, "Unknown", new Point(face.X - 2, face.Y - 2),
                                        FontFace.HersheyComplex, 1.0, new Bgr(Color.Orange).MCvScalar);
                                    CvInvoke.Rectangle(currentFrame, face, new Bgr(Color.Red).MCvScalar, 2);

                                }
                            }
                        }
                    }
                }

                //Изображение с камеры в большой рисунок
                picCapture.Image = currentFrame.ToBitmap();
            }

            //Удаляем лишний файл
            if (currentFrame != null)
                currentFrame.Dispose();
        }


        private void btnAddPerson_Click(object sender, EventArgs e)
        {
            btnAddPerson.Enabled = false;
            EnableSaveImage = true;
        }

        //Тренеровка ИИ (нужно больше изображений!!!)
        private bool TrainImagesFromDir()
        {
            int ImagesCount = 0;
            double Threshold = 3000;
            
            TrainFaces.Clear();
            PersonsLabes.Clear();
            PersonsNames.Clear();
            try
            {
                string path = Directory.GetCurrentDirectory() + @"\DetectFace";
                string[] files = Directory.GetFiles(path, "*.bmp", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    Image<Gray, byte> trainedImage = new Image<Gray, byte>(file).Resize(300, 300, Inter.Cubic);
                    CvInvoke.EqualizeHist(trainedImage, trainedImage);
                    Mat mat = new Mat();
                    TrainFaces.Add(trainedImage.Mat);
                    PersonsLabes.Add(ImagesCount);
                    string name = file.Split('\\').Last().Split('_')[0];
                    PersonsNames.Add(name);
                    ImagesCount++;
                    Debug.WriteLine(ImagesCount + ". " + name);

                }

                if (TrainFaces.Count() > 0)
                {
                    MCvTermCriteria termCrit = new MCvTermCriteria(ImagesCount, 0.001);
                    recognizer = new EigenFaceRecognizer(ImagesCount, 3000);
                    
                    recognizer.Train(TrainFaces.ToArray(), PersonsLabes.ToArray());

                    isTrained = true;
                    return true;
                }
                else
                {
                    isTrained = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                isTrained = false;
                MessageBox.Show("Error in Train Images: " + ex.Message);
                return false;
            }

        }

        private void btnDetectFace_Click(object sender, EventArgs e)
        {
            facesDetectionEnabled = true;
        }

        private void btnTrain_Click_1(object sender, EventArgs e)
        {
            TrainImagesFromDir();
        }
    }
}
