using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

//for spech;
using SpeechLib;
using System.Media;
//for Aforge.NET
using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Video.DirectShow;
//for thread
using System.Threading;
//for neural lib
using AirWriter.Lib;
//for BitmapData
using System.Drawing.Imaging;

//for NameValueCollection
using System.Collections.Specialized;

//for path in xml
using System.Configuration;
using System.IO;

//for hangesture
using Emgu.CV;
using Emgu.CV.Structure;
using HandGestureRecognition.SkinDetector;

//for rotation
//using System.Drawing;
using System.Drawing.Drawing2D;




namespace AirWriter
{
    public partial class SignLanguageRecognizer : Form
    {

      
        //************************handgesture

        IColorSkinDetector skinDetector;

        Image<Bgr, Byte> currentFrame;
        Image<Bgr, Byte> currentFrameCopy;

        Capture grabber;

        AdaptiveSkinDetector detector;

        Hsv hsv_min;
        Hsv hsv_max;
        Ycc YCrCb_min;
        Ycc YCrCb_max;

        //**********************handgesture 


        
        int objectX;
        int objectY;
        int ax;
        int by;
        int axp;
        int ayp;

        //*************for camera input
        private FilterInfoCollection videodevices;      
        GrayscaleBT709 grayscaleFilter = new GrayscaleBT709();
        //for rectangle boxes in picture
        BlobCounter blobCounter = new BlobCounter();
        //*******************************************

        //Neural Network Object With Output Type String
        private NeuralNetwork<string> neuralNetwork = null;

        //Data Members Required For Neural Network

        private Dictionary<string, double[]> TrainingSet = null;
        private int av_ImageHeight = 0;
        private int av_ImageWidth = 0;
        private int NumOfPatterns = 0;

        //For Asynchronized Programming Instead of Handling Threads
        //**************************************************************
        private delegate bool TrainingCallBack();// TrainingCallBack 1 or 0 return karar katha

        private AsyncCallback asyCallBack = null;
        //Reference a method to be called when a corresponding asynchronous operation complets               
        private IAsyncResult res = null;//Represents the status of an asynchronous operation
        private ManualResetEvent ManualReset = null;
        //Notifies one or more waiting threads that an event has occurred. This class cannot be inherited


        private DateTime DTStart;       

        public SignLanguageRecognizer()
        {
            InitializeComponent();

            //for camera initia
            StartMonitoringCamera();
            //for neural network
            InitializeSettings();
            //ভুএ();              
            GenerateTrainingSet();
            CreateNeuralNetwork();          

            asyCallBack = new AsyncCallback(TraningCompleted);
            ManualReset = new ManualResetEvent(false);
            
            
        }
       
        private void ভুএ()
        {
            MessageBox.Show("Bikash");
        }

        private void CreateNeuralNetwork()
        {
            if (TrainingSet == null)
                throw new Exception("Unable to Create Neural Network As There is No Data to Train..");

            //****************************bks******************
            if (comboBoxLayers.SelectedIndex == 0)
            {

                neuralNetwork = new NeuralNetwork<string>
                    (new BP1Layer<string>(av_ImageHeight * av_ImageWidth, NumOfPatterns), TrainingSet);

            }
            //else if (comboBoxLayers.SelectedIndex == 1)
            //{
            //    int InputNum = Int16.Parse(textBoxInputUnit.Text);

            //    neuralNetwork = new NeuralNetwork<string>
            //        (new BP2Layer<string>(av_ImageHeight * av_ImageWidth, InputNum, NumOfPatterns), TrainingSet);

            //}

            //else if (comboBoxLayers.SelectedIndex == 2)
            //{
            //    int InputNum = Int16.Parse(textBoxInputUnit.Text);
            //    int HiddenNum = Int16.Parse(textBoxHiddenUnit.Text);

            //    neuralNetwork = new NeuralNetwork<string>
            //        (new BP3Layer<string>(av_ImageHeight * av_ImageWidth, InputNum, HiddenNum, NumOfPatterns), TrainingSet);

            //}

            //tinta layer er jonno tin rokom coz go there note ache
            //**********************************************************************
           // MessageBox.Show("এই পর্যন্ত সব ঠিক আছে।");            
            neuralNetwork.IterationChanged += new NeuralNetwork<string>.IterationChangedCallBack(neuralNetwork_IterationChanged);

            neuralNetwork.MaximumError = Double.Parse(textBoxMaxError.Text); // settings page er maximum error textbox
        }


        void neuralNetwork_IterationChanged(object o, NeuralEventArgs args)
        {
            UpdateError(args.CurrentError);
            UpdateIteration(args.CurrentIteration);

            if (ManualReset.WaitOne(0, true))
                args.Stop = true;
        }   

        private void GenerateTrainingSet() // এইটা প্রত্যেক টা ইমেজ এর ডাটা training set এর মধ্যে রাখবে।
        {
            // textBoxState.AppendText("Generating Training Set..");

            string[] Patterns = Directory.GetFiles(textBoxTrainingBrowse.Text, "*.bmp");

            TrainingSet = new Dictionary<string, double[]>(Patterns.Length);
            foreach (string s in Patterns)
            {
                //private Dictionary<string, double[]> TrainingSet = null
                // eita upor theke copy kore nie aschi  ----bks
                //training set ekta array er moto jar indexing 0 theke suru hoi see (2008book-file dictionary bks)
                //এই খানে training set er moddhe দুই টাই থাকবে imagename,double result(4m imagerossin.tomatrix)

                Bitmap Temp = new Bitmap(s);
                TrainingSet.Add(Path.GetFileNameWithoutExtension(s), ImageProcessing.ToMatrix(Temp, av_ImageHeight, av_ImageWidth));
                //ImageProcessing class er Tomatrix function
                Temp.Dispose();
            }

            // textBoxState.AppendText("Done!\r\n");
           // MessageBox.Show("Generate traning set thik ache");
        }


        private void StartMonitoringCamera()
        {
            //**********************block for bringing camera dievice in combo box.......start
            try
            {
                videodevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (videodevices.Count == 0)
                    throw new ApplicationException();

                foreach (FilterInfo device in videodevices)
                {
                    comboBox_camera.Items.Add(device.Name);
                }
                comboBox_camera.SelectedIndex = 0; //onno pc te jodi camera akta thake then 0 hobe
            }
            catch (ApplicationException)
            {
                //comboBox_camera.Items.Add("No Device Found");
                MessageBox.Show("No Device Is Found");
                videodevices = null;
            }
            //*********************for bringing camera dievice in combo box.......end


            //// pictureBox1 making white
            //Bitmap b = new Bitmap(320, 240);
            //// Rectangle a = (Rectangle)r;
            //Pen pen1 = new Pen(Color.FromArgb(160, 255, 160), 3);
            //Graphics g2 = Graphics.FromImage(b);
            //pen1 = new Pen(Color.FromArgb(255, 0, 0), 3);
            //g2.Clear(Color.White);
            ////g2.DrawLine(pen1, b.Width / 2, 0, b.Width / 2, b.Width);
            ////g2.DrawLine(pen1, b.Width, b.Height / 2, 0, b.Height / 2);
            //pictureBox1.Image = (System.Drawing.Image)b;
        }
        private void InitializeSettings()
        {
            try
            {
            //thisworks for getting the current file path of pattern
            //for this have .IO and .configuration is needed and reference

            textBoxTrainingBrowse.Text = Path.GetFullPath(ConfigurationSettings.AppSettings["PatternsDirectory"]);
            //first index -1, 2nd +0/-0 (just 0 error dekhabe) , third +1,4th +2
            comboBoxLayers.SelectedIndex = (Int16.Parse(ConfigurationSettings.AppSettings["NumOfLayers"]) - 1);// ei khane 0 dea chilo tai object reference
                //er problem hochilo coz amar akahen just 1 ta layer enable kora ache.
            textBoxMaxError.Text = ConfigurationSettings.AppSettings["MaxError"];
            textBox_voiceDirectory.Text = Path.GetFullPath(ConfigurationSettings.AppSettings["VoiceDirectory"]);

            //***********অন্য ঊপায়******************
            //xml file থেকে ফল্ডার এর ফুল পথ নিয়ে আসার জন্য
            //forNameValueCollection needed using System.Collections.Specialized; and 
            //for ConfigurationManager needed add reference to .NET>system.configuration

            //NameValueCollection AppSettings = ConfigurationManager.AppSettings;
            //comboBoxLayers.SelectedIndex = (Int16.Parse(AppSettings["NumOfLayers"]) - 1);
            //textBoxTrainingBrowse.Text = Path.GetFullPath(AppSettings["PatternsDirectory"]);
            //textBoxMaxError.Text = AppSettings["MaxError"];
            //**********

            
                string[] Images = Directory.GetFiles(textBoxTrainingBrowse.Text, "*.bmp");
                NumOfPatterns = Images.Length;
                //patterns a je koi ta pic ta ache tar sonkha 
                //********************

                av_ImageHeight = 0;
                av_ImageWidth = 0;

                foreach (string s in Images)
                {
                    Bitmap Temp = new Bitmap(s);
                    av_ImageHeight += Temp.Height;
                    av_ImageWidth += Temp.Width;
                    Temp.Dispose();
                }
                av_ImageHeight /= NumOfPatterns;
                av_ImageWidth /= NumOfPatterns;

                //avarage image height ber kora hoise pattern folder er image er upor vitti kore
                //******************************************************************************

                int networkInput = av_ImageHeight * av_ImageWidth;

                //textBoxInputUnit.Text = ((int)((double)(networkInput + NumOfPatterns) * .33)).ToString();
                //textBoxHiddenUnit.Text = ((int)((double)(networkInput + NumOfPatterns) * .11)).ToString();
                textBoxOutputUnit.Text = NumOfPatterns.ToString();

                /// .33/.11 dara keno gun kora holo?
                //tab settings page er text box gulote valu insert kora holo
                //**********************************************


            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Initializing Settings: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            //************************************for smooth opening
            // Set Form's Transperancy 100 %
            this.Opacity = 0;            
            // Start the Timer To Animate Form
            timer2.Enabled = true;
            //***************************************************
            
            ////for camera starting
            //VideoCaptureDevice vs = new VideoCaptureDevice(videodevices[comboBox_camera.SelectedIndex].MonikerString);
            ////VideoCaptureDevice vs = new VideoCaptureDevice(videodivices[1].MonikerString);
            //vs.DesiredFrameSize = new Size(320, 240);
            //vs.DesiredFrameRate = 12;
            //videoSourcePlayer1.VideoSource = vs;
            ////videoSourcePlayer1.Start();
            ////videoSourcePlayer2.VideoSource = vs;
            ////videoSourcePlayer2.Start();
        }
        
        

        private void button6_Click(object sender, EventArgs e)
        {            
            NetworkControl form = new NetworkControl();                
            form.Show();                                      
        }


        

        private void videoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            detector = new AdaptiveSkinDetector(1, AdaptiveSkinDetector.MorphingMethod.NONE);
            
            hsv_min = new Hsv(0, 45, 0);
            hsv_max = new Hsv(20, 255, 255);
            YCrCb_min = new Ycc(0, 131, 80);
            YCrCb_max = new Ycc(255, 185, 135);

            Bitmap mImage = null;
            mImage = (Bitmap)image.Clone();

            ParameterizedThreadStart thd = new ParameterizedThreadStart(writer);
            Thread aa = new Thread(thd);
            aa.Start(mImage);
            Thread.Sleep(50); 

            //Bitmap objectsImage = null;
            //Bitmap mImage = null;
            //mImage = (Bitmap)image.Clone();

            //image = mImage;


            //EuclideanColorFiltering filter = new EuclideanColorFiltering();
            //// set center colol and radius
            ////filter.CenterColor = Color.FromArgb(0, 0, 0);
            ////filter.CenterColor = Color.White;
            //filter.CenterColor = Color.Red;
            //filter.Radius = 120;
            //// apply the filter
            //objectsImage = image; // bitmap er akta object a; image ta asceh parametter theke
            ////new_frame er image objectImage a prottek bar asche;

            //filter.ApplyInPlace(objectsImage);
            ////filter.ApplyInPlace(image);

            //BitmapData objectsData = objectsImage.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, image.PixelFormat);

            ////grayscaling
            //UnmanagedImage grayImage = grayscaleFilter.Apply(new UnmanagedImage(objectsData));

            ////unlock image
            //objectsImage.UnlockBits(objectsData);

            ////locate blobs 
            //blobCounter.ProcessImage(grayImage);
            //Rectangle[] rects = blobCounter.GetObjectRectangles();//rectangle rects er moddhe joto rectangle thakbe ta store kora thakbe
            
        

            //if (rects.Length > 0)
            //{

            //    foreach (Rectangle objectRect in rects)
            //    {
            //        // Rectangle objectRect = rects[0];

            //        // draw rectangle around derected object
            //        Graphics g = Graphics.FromImage(mImage);

            //        // using (Pen pen = new Pen(Color.FromArgb(160, 255, 160), 5))
            //        using (Pen pen = new Pen(Color.Red))
            //        {
            //            g.DrawRectangle(pen, objectRect);
            //        }

            //        g.Dispose();

            //        //int objectX = objectRect.X + objectRect.Width / 2 - image.Width / 2;
            //        //int objectY = image.Height / 2 - (objectRect.Y + objectRect.Height / 2);


            //        ParameterizedThreadStart thd = new ParameterizedThreadStart(writer);
            //        Thread aa = new Thread(thd);
            //        aa.Start();
            //        //aa.Start(rects[0]);
            //        Thread.Sleep(50);
            //        //Application.DoEvents();
            //    }
            //}

           // image = mImage;  // ei ta video source e image show kore, na thakle video dekha jabe na
               
            

                 
                                 
                        
        }        


        void writer(object curImg)
        {
            

            try
            {
               // imageBoxSkin.Image = (Bitmap)curImg; 
                //Bitmap skinImage = (Bitmap)curImg; 
                
                ////************************************************
                //currentFrame = grabber.QueryFrame();
                //if (currentFrame != null)
                //{
                //    currentFrameCopy = currentFrame.Copy();                            

                //    skinDetector = new YCrCbSkinDetector();

                //    Image<Gray, Byte> skin = skinDetector.DetectSkin(currentFrameCopy, YCrCb_min, YCrCb_max);

                //    imageBoxSkin.Image = skin;
                //    imageBoxFrameGrabber.Image = currentFrame;
                //}
                ////************************************************


                //skinImage = Image <skinImage,>; 
                //Emgu.CV.Image = new Emgu.CV.Image<Bgr, Byte>(skinImage);

                skinDetector = new YCrCbSkinDetector();
                //Image<Gray, Byte> skin = skinDetector.DetectSkin(bks, YCrCb_min, YCrCb_max);
                
                //imageBoxSkin.Image = skin;

            }
            catch (Exception faa)
            {
                Thread.CurrentThread.Abort();
            }


            Thread.CurrentThread.Abort();

        }//void writer ses



        private void AirWriter_FormClosed(object sender, FormClosedEventArgs e)
        {
            //videoSourcePlayer1.Stop();
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            //// pictureBox1 making white
            ////*************************************
            //Bitmap b = new Bitmap(320, 240);
            //// Rectangle a = (Rectangle)r;
            //Pen pen1 = new Pen(Color.FromArgb(160, 255, 160), 3);
            //Graphics g2 = Graphics.FromImage(b);
            //pen1 = new Pen(Color.FromArgb(255, 0, 0), 3);
            //g2.Clear(Color.White);
            ////g2.DrawLine(pen1, b.Width / 2, 0, b.Width / 2, b.Width);
            ////g2.DrawLine(pen1, b.Width, b.Height / 2, 0, b.Height / 2);
            //pictureBox1.Image = (System.Drawing.Image)b;
            

            //panel clear korar jonno;
            //**************************
           // writingPlace3.WritigPlaceClear();

            //******************************************emgu er jonno
            imageBox2.Image = imageBoxSkin.Image; 
            
                   
        }

        private void buttonTrain_Click(object sender, EventArgs e)
        {
            ManualReset.Reset();//bool EventWaitHandle.Reset()/>Sets the state of the event to nonsigned, causing threads to block.

            TrainingCallBack TR = new TrainingCallBack(neuralNetwork.Train);
            res = TR.BeginInvoke(asyCallBack, TR);

            DTStart = DateTime.Now;
            timer1.Start();
        }
        private void TraningCompleted(IAsyncResult result)
        {
            if (result.AsyncState is TrainingCallBack)
            {
                TrainingCallBack TR = (TrainingCallBack)result.AsyncState;
                timer1.Stop();
                MessageBox.Show("আমি প্যাটার্ন সম্পর্কে একটা ধারনা নিয়েছি। আপনি লিখা শুরু করতে পারেন।");
            }
            //button_recognize.Enabled = true; এখানে button enable বা disable kora jabe na coz eita thread theke call hoche
           
        }
       
        private void button_stop_Click(object sender, EventArgs e)
        {
            ManualReset.Set();
        }
        private void button_recognize_Click(object sender, EventArgs e)
        {
            string MatchedHigh = "?", MatchedLow = "?";
            double OutputValueHight = 0, OutputValueLow = 0;

            //Bitmap b = new Bitmap(pictureBox1.Image);

            //double[] input = ImageProcessing.ToMatrix(writingPlace3.CharacterOnPanel, av_ImageHeight, av_ImageWidth);

            Bitmap savechar = new Bitmap(imageBox2.Image.Bitmap, 30, 33);// dimensin reduction

            double[] input = ImageProcessing.ToMatrix(savechar, av_ImageHeight, av_ImageWidth);

            //            double[] input = ImageProcessing.ToMatrix(imageBox2.Image.Bitmap, av_ImageHeight, av_ImageWidth);

            neuralNetwork.Recognize(input, ref MatchedHigh, ref OutputValueHight, ref MatchedLow, ref OutputValueLow);//***************
            
            //****for testing air
            //MessageBox.Show(MatchedHigh);

            string firstH = MatchedHigh.Substring(0,1);//ei khan theke first letter ta parse kore nichi
            string firstL = MatchedLow.Substring(0, 1);

            //MessageBox.Show(firstH);
            //MessageBox.Show(firstL);
            //MessageBox.Show(OutputValueLow.ToString());
            //ShowRecognitionResults(MatchedHigh, MatchedLow, OutputValueHight, OutputValueLow);
            ShowRecognitionResults(firstH, firstL, OutputValueHight, OutputValueLow);           
            

        }

        private void ShowRecognitionResults(string MatchedHigh, string MatchedLow, double OutputValueHight, double OutputValueLow)
        {
            labelMatchedHigh.Text = "High: " +  " ( " + ((int)100 * OutputValueHight).ToString("##") + "% )";//bangla letter er karone %value ta dekhache na
            labelMatchedLow.Text = "Low: " +  " ( " + ((int)100 * OutputValueLow).ToString("##") + "% )"; // koto percent milse seta show korbe

            //pictureBoxInput.Image = new Bitmap(drawingPanel1.ImageOnPanel, pictureBoxInput.Width, pictureBoxInput.Height); // jeta draw korbo oi ta show korbe

            if (MatchedHigh != "?")
                pictureBoxMatchedHigh.Image = new Bitmap(new Bitmap(textBoxTrainingBrowse.Text + "\\" + MatchedHigh + ".bmp"),
                    pictureBoxMatchedHigh.Width, pictureBoxMatchedHigh.Height);

            if (MatchedLow != "?")
                pictureBoxMatchedLow.Image = new Bitmap(new Bitmap(textBoxTrainingBrowse.Text + "\\" + MatchedLow + ".bmp"),
                    pictureBoxMatchedLow.Width, pictureBoxMatchedLow.Height);


            //writing in the text box
            //textBox_recognize.Text += MatchedHigh;
            textBox_recognize.Text = MatchedHigh;
            
            Application.DoEvents();// this for giving time to take voice to speak;
            //writeIntoTextBox();
            speakTheletter();
            
           

        }
        private void writeIntoTextBox()
        {
            //writing in the text box
            //textBox_recognize.Text += MatchedHigh;
            //textBox_recognize.Text = MatchedHigh;
        }
        
        private void speakTheletter()
        {
            //speak out what is written in the air
            //SpVoice voice = new SpVoice();
            //voice.Speak("You write" + textBox_recognize.Text + "in the air", SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
            //voice.Speak("Apni", SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
            
            //SoundPlayer sp = new SoundPlayer(@"D:\অ.wav");
            //SoundPlayer sp = new SoundPlayer(textBox_voiceDirectory.Text + "\\" + textBox_recognize.Text + ".wav");
            //sp.Play();

            if (textBox_recognize.Text != null)
            {
                SoundPlayer sp = new SoundPlayer(textBox_voiceDirectory.Text + "\\" + textBox_recognize.Text + ".wav");
                //sp.Play();
            }

            //if (textBox_recognize.Text == "অ")
            //{
            //    SoundPlayer sp = new SoundPlayer(textBox_voiceDirectory.Text + "\\" + textBox_recognize.Text + ".wav");
            //    sp.Play();
            //}
            
            //voice.Speak("Likhechen", SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            textBox_RecentText.Text += textBox_recognize.Text;
        }

       
        #region Methods To Invoke UI Components If Required
        
        private delegate void UpdateUI(object o);        
        
        private void UpdateError(object o)
        {
            if (label_error.InvokeRequired)
            {
                label_error.Invoke(new UpdateUI(UpdateError), o);
            }
            else
            {
                label_error.Text = "Error: " + ((double)o).ToString(".###");
            }
        }
        private void UpdateIteration(object o)
        {
            if (label_iteration.InvokeRequired)
            {
                label_iteration.Invoke(new UpdateUI(UpdateIteration), o);
            }
            else
            {
                label_iteration.Text = "Iteration: " + ((int)o).ToString();
            }
        }       

        private void UpdateTimer(object o)
        {
            //if (labelTimer.InvokeRequired)
            if (label_timer.InvokeRequired)
            {
                label_timer.Invoke(new UpdateUI(UpdateTimer), o);
            }
            else
            {
                label_timer.Text = (string)o;
            }
        }

        #endregion
        
        #region RadioButton & CheckBox Event Handlers- Not Important
        private void comboBoxLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxLayers.SelectedIndex == 0)
            {
               // textBoxInputUnit.Enabled = false;
               // textBoxHiddenUnit.Enabled = false;
            }           
        }
        #endregion
        
        private void timer1_Tick(object sender, EventArgs e)
        {         
           
                TimeSpan TSElapsed = DateTime.Now.Subtract(DTStart);
                UpdateTimer(TSElapsed.Hours.ToString("D2") + ":" +
                TSElapsed.Minutes.ToString("D2") + ":" +
                TSElapsed.Seconds.ToString("D2"));
            
        }

        private void button_restart_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            // Make Form Visible a Bit more on Every timer Tick
            this.Opacity += 0.07;
        }

        private void button_Browse_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "Bitmap Image(*.bmp)|*.bmp";
            fd.InitialDirectory = textBoxTrainingBrowse.Text;
            if (fd.ShowDialog() == DialogResult.OK)
            {
                string filename = fd.FileName;
                if (Path.GetExtension(filename) == ".bmp")
                {
                    //pictureBox1.Image = new Bitmap(new Bitmap(filename), pictureBox1.Width, pictureBox1.Height);
                    //writingPlace3.CharacterOnPanel = new Bitmap(new Bitmap(filename), writingPlace3.Width, writingPlace3.Height);


                    Bitmap bks = new Bitmap(new Bitmap(filename), imageBox2.Width, imageBox2.Height);
                    Image<Bgr, byte> facePic = new Image<Bgr, byte>(bks);
                    imageBox2.Image = facePic; 

                    //drawingPanel1.ImageOnPanel = new Bitmap(new Bitmap(FileName), drawingPanel1.Width, drawingPanel1.Height);
                }
            }
            fd.Dispose();
        }

       
        private void pictureBox1_MouseHover(object sender, EventArgs e)
        {
            
            //base.MouseUp +=new MouseEventHandler(AirWriter_MouseUp);
            //AirWriter_MouseDown();

        }


      
        

        private void button_Capture_Click(object sender, EventArgs e)
        {
            ////for camera starting
            //VideoCaptureDevice vs = new VideoCaptureDevice(videodevices[comboBox_camera.SelectedIndex].MonikerString);
            ////VideoCaptureDevice vs = new VideoCaptureDevice(videodivices[1].MonikerString);
            //vs.DesiredFrameSize = new Size(320, 240);
            //vs.DesiredFrameRate = 12;
            //videoSourcePlayer1.VideoSource = vs;
            //videoSourcePlayer1.Start();
            ////videoSourcePlayer2.VideoSource = vs;
            ////videoSourcePlayer2.Start();


            //EMGU*******************************************************
            grabber = new Emgu.CV.Capture();
            grabber.QueryFrame();

            detector = new AdaptiveSkinDetector(1, AdaptiveSkinDetector.MorphingMethod.NONE);

            hsv_min = new Hsv(0, 45, 0);
            hsv_max = new Hsv(20, 255, 255);
            YCrCb_min = new Ycc(0, 131, 80);
            YCrCb_max = new Ycc(255, 185, 135);


            Application.Idle += new EventHandler(FrameGrabber);


            //EMGU*******************************************************




        }

        void FrameGrabber(object sender, EventArgs e)
        //void FrameGrabber(object r)
        {
            currentFrame = grabber.QueryFrame();
            if (currentFrame != null)
            {
                currentFrameCopy = currentFrame.Copy();

                skinDetector = new YCrCbSkinDetector();

                Image<Gray, Byte> skin = skinDetector.DetectSkin(currentFrameCopy, YCrCb_min, YCrCb_max);
                                
                
                // bitmap to emgu.CV
                //******************************
                //Bitmap bks = new Bitmap(new Bitmap(filename), imageBox2.Width, imageBox2.Height);
                //Image<Bgr, byte> facePic = new Image<Bgr, byte>(bks);
                //imageBox2.Image = facePic;

                //emgu to bitmap 
                //just emgu image er por .Bitmap add korlei hoi. 


                Bitmap newB = skin.Bitmap;
                newB.RotateFlip(RotateFlipType.RotateNoneFlipX);

                Image<Bgr, byte> newSkin = new Image<Bgr, byte>(newB);

                imageBoxSkin.Image = newSkin; 
;
                imageBoxFrameGrabber.Image = currentFrame;
            }
            //Thread.CurrentThread.Abort();
        }

       
        

        private void button_savechr_Click(object sender, EventArgs e)
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.Filter = "Bitmap Image(*.bmp)|*.bmp";
            fd.InitialDirectory = textBoxTrainingBrowse.Text;

            //Bitmap savechar = new Bitmap(writingPlace3.CharacterOnPanel, 30, 33);
            Bitmap savechar = new Bitmap(imageBox2.Image.Bitmap, 30, 33);

            if (fd.ShowDialog() == DialogResult.OK)
            {
               
                savechar.Save(fd.FileName, ImageFormat.Bmp);

                MessageBox.Show("সংরক্ষণ করা হল");
            }
            fd.Dispose();
        }

        
        private void button_erase_Click(object sender, EventArgs e)
        {
            
        }


        //thisworks for getting the current file path of pattern
        //for this have .IO and .configuration is needed and reference

        string pathdirectory = Path.GetFullPath(ConfigurationSettings.AppSettings["LettersDirectory"]);

        string sentence;
        int len;
        int speed;

        private void button7_Click(object sender, EventArgs e)
        {
            if (radioButton_slow.Checked)
                speed = 1;
            else if (radioButton_medium.Checked)
                speed = 2;
            else if (radioButton_deaf.Checked)
                speed = 3;

            //sentence = textBox_englng.Text;//works just for small letters
            sentence = textBox_englng.Text.ToLower();
            len = sentence.Length;

            //MessageBox.Show(sentence[2].ToString());//it will show first character of sentence
            //MessageBox.Show(sentence.Length.ToString());//show lenth of the sentence


            //<= dile index was out of bound error dekhabe 
            //coz len er = value pabe na, 0 theke suru ei jonno
            // 1 theke suru korle no prob

            //MessageBox.Show(sentence[i].ToString());

            for (int i = 0; i < len; i++)
            {
                if (sentence[i] == 'a')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "a" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'b')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "b" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'c')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "c" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'd')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "d" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'e')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "e" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'f')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "f" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'g')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "g" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'h')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "h" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'i')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "i" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'j')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "j" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'k')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "k" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'l')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "l" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'm')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "m" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'n')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "n" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'o')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "o" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'p')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "p" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'q')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "q" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'r')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "r" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 's')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "s" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 't')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "t" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'u')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "u" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'v')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "v" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'w')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "w" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'x')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "x" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'y')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "y" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == 'z')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "z" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }
                else if (sentence[i] == ' ')
                {
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "space" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }

                else if (sentence[i] == 'ক')
                {                    
                    pictureBox8.Image = new Bitmap(new Bitmap(pathdirectory + "\\" + "ক" + ".gif"),
                     pictureBox8.Width, pictureBox8.Height);
                }

                //making delay for next sign appearing
                if (speed == 1)
                {
                    Thread.Sleep(1000);
                    Application.DoEvents(); // ei ta na dile UI update hobe na, force kore UI update kore      
                }
                else if (speed == 2)
                {
                    Thread.Sleep(500);
                    Application.DoEvents();
                }
                else if (speed == 3)
                {
                    Thread.Sleep(200);
                    Application.DoEvents();
                }
            }
        }
          
                            
    }
}
