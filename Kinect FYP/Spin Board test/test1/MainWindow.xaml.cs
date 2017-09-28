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
using System.Windows.Media.Animation;
using Microsoft.Kinect;
using Microsoft.Speech.Recognition;
using System.Threading;
using System.IO;
using Microsoft.Speech.AudioFormat;
using System.Diagnostics;
using System.Windows.Threading;
using Kinect.Toolbox;


namespace test1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        KinectSensor sensor;
        SpeechRecognitionEngine speechRecognizer;

        DispatcherTimer readyTimer;

        byte[] colorBytes;

        SwipeGestureDetector swipeGestureRecognizer;
        bool isCirclesVisible = true;

        bool isForwardGestureActive = false;
        bool isBackGestureActive = false;
        bool flag = true;
        SolidColorBrush activeBrush = new SolidColorBrush(Colors.Green);
        SolidColorBrush inactiveBrush = new SolidColorBrush(Colors.Red);

        public MainWindow()
        {
            InitializeComponent();

            //Runtime initialization is handled when the window is opened. When the window
            //is closed, the runtime MUST be unitialized.
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            //Handle the content obtained from the video camera, once received.

            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
        }

        Brush brush1 = new SolidColorBrush(Colors.Red);

        void addLine(Joint j1, Joint j2)
        {
            Line boneline = new Line();
            boneline.Stroke = brush1;
            boneline.StrokeThickness = 5;

            ColorImagePoint j1p = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(j1.Position, ColorImageFormat.RgbResolution640x480Fps30);
            boneline.X1 = j1p.X/3;
            boneline.Y1 = j1p.Y/3;

            ColorImagePoint j2p = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(j2.Position, ColorImageFormat.RgbResolution640x480Fps30);
            boneline.X2 = j2p.X/3;
            boneline.Y2 = j2p.Y/3;

            c1.Children.Add(boneline);
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            sensor = KinectSensor.KinectSensors.FirstOrDefault();
            swipeGestureRecognizer = new SwipeGestureDetector();
            swipeGestureRecognizer.OnGestureDetected += swipeGestureRecognizer_OnGestureDetected;

            if (sensor == null)
            {
                MessageBox.Show("This application requires a Kinect sensor.");
                this.Close();
            }

            sensor.Start();

            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            sensor.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(sensor_ColorFrameReady);

            sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
            sensor.SkeletonStream.Enable();
            sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(sensor_SkeletonFrameReady);

            sensor.ElevationAngle = 0;

            Application.Current.Exit += new ExitEventHandler(Current_Exit);

            InitializeSpeechRecognition();
        }

        void swipeGestureRecognizer_OnGestureDetected(string obj)
        {
            throw new NotImplementedException();
        }

        void Current_Exit(object sender, ExitEventArgs e)
        {
            if (speechRecognizer != null)
            {
                speechRecognizer.RecognizeAsyncCancel();
                speechRecognizer.RecognizeAsyncStop();
            }
            if (sensor != null)
            {
                sensor.AudioSource.Stop();
                sensor.Stop();
                sensor.Dispose();
                sensor = null;
            }
        }

        void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C)
            {
                ToggleCircles();
            }
        }

        void sensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var image = e.OpenColorImageFrame())
            {
                if (image == null)
                    return;

                if (colorBytes == null ||
                    colorBytes.Length != image.PixelDataLength)
                {
                    colorBytes = new byte[image.PixelDataLength];
                }

                image.CopyPixelDataTo(colorBytes);

                //You could use PixelFormats.Bgr32 below to ignore the alpha,
                //or if you need to set the alpha you would loop through the bytes 
                //as in this loop below
                int length = colorBytes.Length;
                for (int i = 0; i < length; i += 4)
                {
                    colorBytes[i + 3] = 255;
                }

                BitmapSource source = BitmapSource.Create(image.Width,
                    image.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    colorBytes,
                    image.Width * image.BytesPerPixel);
                    
            }
        }


        void sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            c1.Children.Clear();
            Skeleton[] skeletons = null;

            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null)
                    return;

                if (skeletons == null ||
                    skeletons.Length != skeletonFrame.SkeletonArrayLength)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                skeletonFrame.CopySkeletonDataTo(skeletons);

                Skeleton closestSkeleton = (from s in skeletons
                                            where s.TrackingState == SkeletonTrackingState.Tracked &&
                                                  s.Joints[JointType.Head].TrackingState == JointTrackingState.Tracked
                                            select s).OrderBy(s => s.Joints[JointType.Head].Position.Z)
                                                    .FirstOrDefault();

                if (closestSkeleton == null)
                    return;

                var head = closestSkeleton.Joints[JointType.Head];
                var rightHand = closestSkeleton.Joints[JointType.HandRight];
                var leftHand = closestSkeleton.Joints[JointType.HandLeft];
                var hipLeft = closestSkeleton.Joints[JointType.HipLeft];

                if (head.TrackingState != JointTrackingState.Tracked ||
                    rightHand.TrackingState != JointTrackingState.Tracked ||
                    leftHand.TrackingState != JointTrackingState.Tracked ||
                    hipLeft.TrackingState != JointTrackingState.Tracked)
                {
                    //Don't have a good read on the joints so we cannot process gestures
                    return;
                }

                SetEllipsePosition(ellipseHead, head, false);
                SetEllipsePosition(ellipseLeftHand, leftHand, isBackGestureActive);
                SetEllipsePosition(ellipseRightHand, rightHand, isForwardGestureActive);

                ProcessForwardBackGesture(head, rightHand, leftHand, hipLeft);
            }

            if (skeletons == null)
                return;
            foreach (Skeleton skeleton in skeletons)
            {
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    Joint headJoint = skeleton.Joints[JointType.Head];
                    Joint hipCenter = skeleton.Joints[JointType.HipCenter];

                    // Spine
                    addLine(skeleton.Joints[JointType.Head], skeleton.Joints[JointType.ShoulderCenter]);
                    addLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.Spine]);

                    // Left leg
                    addLine(skeleton.Joints[JointType.Spine], skeleton.Joints[JointType.HipCenter]);
                    addLine(skeleton.Joints[JointType.HipCenter], skeleton.Joints[JointType.HipLeft]);
                    addLine(skeleton.Joints[JointType.HipLeft], skeleton.Joints[JointType.KneeLeft]);
                    addLine(skeleton.Joints[JointType.KneeLeft], skeleton.Joints[JointType.AnkleLeft]);
                    addLine(skeleton.Joints[JointType.AnkleLeft], skeleton.Joints[JointType.FootLeft]);

                    // Right leg
                    addLine(skeleton.Joints[JointType.HipCenter], skeleton.Joints[JointType.HipRight]);
                    addLine(skeleton.Joints[JointType.HipRight], skeleton.Joints[JointType.KneeRight]);
                    addLine(skeleton.Joints[JointType.KneeRight], skeleton.Joints[JointType.AnkleRight]);
                    addLine(skeleton.Joints[JointType.AnkleRight], skeleton.Joints[JointType.FootRight]);

                    // Left arm
                    addLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.ShoulderLeft]);
                    addLine(skeleton.Joints[JointType.ShoulderLeft], skeleton.Joints[JointType.ElbowLeft]);
                    addLine(skeleton.Joints[JointType.ElbowLeft], skeleton.Joints[JointType.WristLeft]);
                    addLine(skeleton.Joints[JointType.WristLeft], skeleton.Joints[JointType.HandLeft]);

                    // Right arm
                    addLine(skeleton.Joints[JointType.ShoulderCenter], skeleton.Joints[JointType.ShoulderRight]);
                    addLine(skeleton.Joints[JointType.ShoulderRight], skeleton.Joints[JointType.ElbowRight]);
                    addLine(skeleton.Joints[JointType.ElbowRight], skeleton.Joints[JointType.WristRight]);
                    addLine(skeleton.Joints[JointType.WristRight], skeleton.Joints[JointType.HandRight]);

                }
            }
        }

        int rotadegree = 0;
        int a = 0;
        int c = 0;
        DispatcherTimer timer = new DispatcherTimer();

        private void ProcessForwardBackGesture(Joint head, Joint rightHand, Joint leftHand, Joint hipLeft)
        {

            if (rightHand.Position.Y < hipLeft.Position.Y && rightHand.Position.X > head.Position.X+0.25 && flag == true )
            {
                if (!isBackGestureActive && !isForwardGestureActive)
                {
                    isForwardGestureActive = true;
                    flag = false;
                    
                    DoubleAnimation dax = new DoubleAnimation();
                    RotateTransform rota = new RotateTransform();
                    Random degree = new Random();

                    c++;

                    rotadegree = degree.Next(a + 1080, a + 1440);

                    tx1.Text = Convert.ToString(rotadegree);
                    lab2.Text = Convert.ToString(a + 1440 - rotadegree);
                    dax.To = rotadegree;

                    int time = (rotadegree - a) * 2;
                    Duration duration = new Duration(TimeSpan.FromMilliseconds(time));
                    dax.Duration = duration;

                    a = rotadegree;

                    this.pp.BeginAnimation(RotateTransform.AngleProperty, dax);

                    timer.Interval = new TimeSpan(0, 0, 3);
                    timer.Tick += new EventHandler(dispatcherTimer_Tick);

                    timer.Start();
                }
            }

            else
            {
                isForwardGestureActive = false;
            }

            //if (leftHand.Position.X < head.Position.X - 0.45)
            //{
            //    if (!isBackGestureActive && !isForwardGestureActive)
            //    {
            //        isBackGestureActive = true;
            //        System.Windows.Forms.SendKeys.SendWait("{Left}");
            //    }
            //}
            //else
            //{
            //    isBackGestureActive = false;
            //}
        }

        //This method is used to position the ellipses on the canvas
        //according to correct movements of the tracked joints.
        private void SetEllipsePosition(Ellipse ellipse, Joint joint, bool isHighlighted)
        {
            var point = sensor.CoordinateMapper.MapSkeletonPointToColorPoint(joint.Position, sensor.ColorStream.Format);

            if (isHighlighted)
            {
                ellipse.Width = 60;
                ellipse.Height = 60;
                ellipse.Fill = activeBrush;
            }
            else
            {
                ellipse.Width = 20;
                ellipse.Height = 20;
                ellipse.Fill = inactiveBrush;
            }
            
            Canvas.SetLeft(ellipse, point.X/3 - ellipse.ActualWidth / 3);
            Canvas.SetTop(ellipse, point.Y/3 - ellipse.ActualHeight / 3);
        }

        void ToggleCircles()
        {
            if (isCirclesVisible)
                HideCircles();
            else
                ShowCircles();
        }

        void HideCircles()
        {
            isCirclesVisible = false;
            ellipseHead.Visibility = System.Windows.Visibility.Collapsed;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Collapsed;
            ellipseRightHand.Visibility = System.Windows.Visibility.Collapsed;
        }

        void ShowCircles()
        {
            isCirclesVisible = true;
            ellipseHead.Visibility = System.Windows.Visibility.Visible;
            ellipseLeftHand.Visibility = System.Windows.Visibility.Visible;
            ellipseRightHand.Visibility = System.Windows.Visibility.Visible;
        }

        #region Speech Recognition Methods

        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        private void InitializeSpeechRecognition()
        {
            RecognizerInfo ri = GetKinectRecognizer();
            if (ri == null)
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                speechRecognizer = new SpeechRecognitionEngine(ri.Id);
            }
            catch
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed and configured.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            var phrases = new Choices();
            phrases.Add("computer show window");
            phrases.Add("computer hide window");
            phrases.Add("computer show circles");
            phrases.Add("computer hide circles");

            var gb = new GrammarBuilder();
            //Specify the culture to match the recognizer in case we are running in a different culture.                                 
            gb.Culture = ri.Culture;
            gb.Append(phrases);

            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);

            speechRecognizer.LoadGrammar(g);
            speechRecognizer.SpeechRecognized += SreSpeechRecognized;
            speechRecognizer.SpeechHypothesized += SreSpeechHypothesized;
            speechRecognizer.SpeechRecognitionRejected += SreSpeechRecognitionRejected;

            this.readyTimer = new DispatcherTimer();
            this.readyTimer.Tick += this.ReadyTimerTick;
            this.readyTimer.Interval = new TimeSpan(0, 0, 4);
            this.readyTimer.Start();

        }

        private void ReadyTimerTick(object sender, EventArgs e)
        {
            this.StartSpeechRecognition();
            this.readyTimer.Stop();
            this.readyTimer = null;
        }

        private void StartSpeechRecognition()
        {
            if (sensor == null || speechRecognizer == null)
                return;

            var audioSource = this.sensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = audioSource.Start();

            speechRecognizer.SetInputToAudioStream(
                    kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);

        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Trace.WriteLine("\nSpeech Rejected, confidence: " + e.Result.Confidence);
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Trace.Write("\rSpeech Hypothesized: \t{0}", e.Result.Text);
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //This first release of the Kinect language pack doesn't have a reliable confidence model, so 
            //we don't use e.Result.Confidence here.
            if (e.Result.Confidence < 0.70)
            {
                Trace.WriteLine("\nSpeech Rejected filtered, confidence: " + e.Result.Confidence);
                return;
            }

            Trace.WriteLine("\nSpeech Recognized, confidence: " + e.Result.Confidence + ": \t{0}", e.Result.Text);

            if (e.Result.Text == "computer show window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.Topmost = true;
                    this.WindowState = System.Windows.WindowState.Normal;
                });
            }
            else if (e.Result.Text == "computer hide window")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.Topmost = false;
                    this.WindowState = System.Windows.WindowState.Minimized;
                });
            }
            else if (e.Result.Text == "computer hide circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.HideCircles();
                });
            }
            else if (e.Result.Text == "computer show circles")
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    this.ShowCircles();
                });
            }
        }

        #endregion


        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            flag = true;
        }
    }
}
