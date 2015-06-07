using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;


namespace Emgu.CV.StickyTetris
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        private const int drawShift = 10;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        private readonly Brush contourBrush = new SolidColorBrush(Color.FromRgb(0, 228, 255));

        private readonly Pen stickyPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 142, 0, 200)), 20);

        private readonly Brush stickyBrush = new SolidColorBrush(Color.FromArgb(255, 142, 0, 200));

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        private System.Drawing.Point[] po;
        private Emgu.CV.Contour<System.Drawing.Point> contour;
        private Emgu.CV.Contour<System.Drawing.Point> result;
        private StreamGeometry geometry;
        private DispatcherTimer _timer;
        private TimeSpan _time;
        private DateTimeOffset startTime;
        private KinectSensorChooser sensorChooser;
        private int levelTime = 15;
        private List<Image<Bgr, Byte>> imageList;

        private enum Level { Normal, Hard };

        private bool started = false;
        private int level = 1;
        private bool blocked = false;

        private bool[] drawed;

        public Window1()
        {
            InitializeComponent();
            Loaded += onLoaded;
        }

        ~Window1()
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
            sensorChooser.Stop();
        }

        private List<Point> LinesToPoints(LineSegment2D[] lines)
        {
            List<Point> list = new List<Point>();
            foreach (LineSegment2D line in lines)
            {
                list.Add(new Point(line.P1.X, line.P1.Y));
                list.Add(new Point(line.P2.X, line.P2.Y));
            }
            return list;
        }

        private void onLoaded(object sender, RoutedEventArgs e)
        {
            startScreenMusic.Play();
            level = 1;
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooser.KinectChanged += SensorChooserOnKinectChanged;
            this.sensorChooserUi.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.Start();
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            human.Source = this.imageSource;

            imageList = new List<Image<Bgr, byte>>();
            string[] filePaths = Directory.GetFiles(@"../Images/Shapes")
                .Select(path => Path.GetFullPath(path))
                                     .ToArray();
            foreach (string item in filePaths)
            {
                imageList.Add((new Image<Bgr, byte>(item)).Resize(640, 480, INTER.CV_INTER_LANCZOS4));
            }
            drawed = new bool[imageList.Count];
            getNewContour();
        }

        private void getNewContour()
        {
            blocked = false;
            Random random = new Random();
            int get = random.Next() % drawed.Length;

            while (drawed[get])
                get = (get + 1) % drawed.Length;

            Image<Gray, Byte> gray = imageList[get].Convert<Gray, Byte>().PyrDown().PyrUp();

            contour = gray.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_TC89_KCOS, RETR_TYPE.CV_RETR_LIST);

            if (contour != null)
            {
                result = contour.ApproxPoly(contour.Perimeter * 0.00001);
            }

            po = result.ToArray();
            geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(po[0].X, po[0].Y + drawShift), true, false);
                for (int i = 1; i < po.Length; i++)
                {
                    ctx.LineTo(new Point(po[i].X, po[i].Y + drawShift), true, false);
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
            sensorChooser.Stop();
        }

        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if (started)
            {
                Skeleton[] skeletons = new Skeleton[0];

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }

                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                    dc.DrawGeometry(contourBrush, null, geometry);

                    if (skeletons.Length != 0)
                    {
                        foreach (Skeleton skel in skeletons)
                        {
                            RenderClippedEdges(skel, dc);

                            if (skel.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                this.DrawBonesAndJoints(skel, dc);
                                this.checkHumanPosition(skel, dc);
                            }
                            else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                            {
                                dc.DrawEllipse(
                                this.centerPointBrush,
                                null,
                                this.SkeletonPointToScreen(skel.Position),
                                BodyCenterThickness,
                                BodyCenterThickness);
                            }
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                }
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {

            #region rightHand
            List<Joint> rightHand = new List<Joint>();
            rightHand.Add(skeleton.Joints[JointType.ShoulderCenter]);
            rightHand.Add(skeleton.Joints[JointType.ShoulderRight]);
            rightHand.Add(skeleton.Joints[JointType.ElbowRight]);
            rightHand.Add(skeleton.Joints[JointType.WristRight]);
            rightHand.Add(skeleton.Joints[JointType.HandRight]);

            this.drawManyBones(skeleton, drawingContext, rightHand);
            #endregion

            #region leftHand
            List<Joint> leftHand = new List<Joint>();
            leftHand.Add(skeleton.Joints[JointType.ShoulderCenter]);
            leftHand.Add(skeleton.Joints[JointType.ShoulderLeft]);
            leftHand.Add(skeleton.Joints[JointType.ElbowLeft]);
            leftHand.Add(skeleton.Joints[JointType.WristLeft]);
            leftHand.Add(skeleton.Joints[JointType.HandLeft]);

            this.drawManyBones(skeleton, drawingContext, leftHand);
            #endregion

            #region spine
            List<Joint> spine = new List<Joint>();
            spine.Add(skeleton.Joints[JointType.Head]);
            spine.Add(skeleton.Joints[JointType.ShoulderCenter]);
            spine.Add(skeleton.Joints[JointType.Spine]);
            spine.Add(skeleton.Joints[JointType.HipCenter]);

            this.drawManyBones(skeleton, drawingContext, spine);
            #endregion

            #region leftleg
            List<Joint> leftLeg = new List<Joint>();
            leftLeg.Add(skeleton.Joints[JointType.HipCenter]);
            leftLeg.Add(skeleton.Joints[JointType.HipLeft]);
            leftLeg.Add(skeleton.Joints[JointType.KneeLeft]);
            leftLeg.Add(skeleton.Joints[JointType.AnkleLeft]);
            leftLeg.Add(skeleton.Joints[JointType.FootLeft]);

            this.drawManyBones(skeleton, drawingContext, leftLeg);
            #endregion

            #region rightleg
            List<Joint> rightLeg = new List<Joint>();
            rightLeg.Add(skeleton.Joints[JointType.HipCenter]);
            rightLeg.Add(skeleton.Joints[JointType.HipRight]);
            rightLeg.Add(skeleton.Joints[JointType.KneeRight]);
            rightLeg.Add(skeleton.Joints[JointType.AnkleRight]);
            rightLeg.Add(skeleton.Joints[JointType.FootRight]);

            this.drawManyBones(skeleton, drawingContext, rightLeg);
            #endregion

            this.DrawHead(skeleton, drawingContext);

        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawHead(Skeleton skeleton, DrawingContext drawingContext)
        {
            Joint joint0 = skeleton.Joints[JointType.Head];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            Brush myBrush = new SolidColorBrush(Color.FromArgb(250, 250, 250, 250));
            drawingContext.DrawEllipse(myBrush, stickyPen, this.SkeletonPointToScreen(joint0.Position), 20, 20);
            testContour(drawingContext, this.SkeletonPointToScreen(joint0.Position));
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Aqua,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Aqua,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Aqua,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Aqua,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        private Boolean testContour(DrawingContext drawingContext, Point point)
        {
            if (
                CvInvoke.cvPointPolygonTest((IntPtr)result, pt: new PointF((float)point.X, (float)point.Y),
                    measureDist: 2) >= 0)
            {
                return true;
            }
            else return false;

        }

        private void checkHumanPosition(Skeleton skeleton, DrawingContext drawingContext)
        {
            var values = Enum.GetValues(typeof(JointType));
            List<Joint> list = new List<Joint>();
            Boolean draw = true;
            foreach (JointType val in values)
            {
                list.Add(skeleton.Joints[val]);
            }
            foreach (Joint joint in list)
            {
                if (this.testContour(drawingContext, this.SkeletonPointToScreen(joint.Position)) == false)
                {
                    draw = false;
                    break;
                }
            }
            if (draw)
            {
                nextLevel();
                drawingContext.DrawRectangle(
                        Brushes.Red,
                        null,
                        new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        private void nextLevel()
        {
            stopTimer();
            if (level + 1 == drawed.Length)
            {
                winnerTb.Visibility = Visibility.Visible;
            }
            else
            {
                if (!blocked)
                {
                    levelTB.Text = "Level" + ++level;
                    optionSound.Stop();
                    optionSound.Play();
                    blocked = true;
                    if (imageList.Count - level > 0)
                    {
                        getNewContour();
                        startTimer();
                    }
                }
            }



        }

        private void StartOnClick(object sender, RoutedEventArgs e)
        {
            level = 1;
            getNewContour();
            game_over.Visibility = Visibility.Hidden;
            startTimer();
            startScreenMusic.Stop();
            levelSound.Stop();
            levelSound.Play();
            bgMusic.Play();
            kinectRegion.Visibility = Visibility.Hidden;
            Game.Visibility = Visibility.Visible;
            started = true;
            KinectTileButton send = (KinectTileButton)sender;
            send.IsEnabled = false;
            send.Visibility = Visibility.Hidden;
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs args)
        {
            if (args.OldSensor != null)
            {
                try
                {
                    args.OldSensor.DepthStream.Disable();
                    args.OldSensor.SkeletonStream.Disable();
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }

            if (args.NewSensor != null)
            {
                try
                {
                    TransformSmoothParameters smoothingParam = new TransformSmoothParameters();
                    {
                        smoothingParam.Smoothing = 0.5f;
                        smoothingParam.Correction = 0.1f;
                        smoothingParam.Prediction = 0.5f;
                        smoothingParam.JitterRadius = 0.1f;
                        smoothingParam.MaxDeviationRadius = 0.1f;
                    };
                    args.NewSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    args.NewSensor.SkeletonStream.Enable(smoothingParam);
                    args.NewSensor.SkeletonFrameReady += SensorSkeletonFrameReady;
                    this.sensor = args.NewSensor;

                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }

            kinectRegion.KinectSensor = args.NewSensor;
        }

        private void MediaFailedHandler(object sender, ExceptionRoutedEventArgs e)
        {
            throw new System.Exception("Mediaelement error", e.ErrorException);
        }

        private void startTimer()
        {
            _time = TimeSpan.FromSeconds(levelTime);

            _timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                tbTime.Text = _time.ToString("c");
                if (_time == TimeSpan.Zero) _timer.Stop();
                _time = _time.Add(TimeSpan.FromSeconds(-1));
            }, Application.Current.Dispatcher);

            _timer.Tick += _timer_Tick;

            _timer.Start();

            startTime = DateTimeOffset.Now;
        }

        private void stopTimer()
        {
            _timer.Stop();
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            DateTimeOffset time = DateTimeOffset.Now;
            TimeSpan span = time - startTime;
            if (span.Seconds >= levelTime)
            {
                _timer.Stop();
                bgMusic.Stop();
                kinectRegion.Visibility = Visibility.Visible;
                Game.Visibility = Visibility.Hidden;
                kinectButton.Visibility = Visibility.Visible;
                kinectButton.IsEnabled = true;
                game_over.Visibility = Visibility.Visible;
                startScreenMusic.Play();
            }
        }

        private void drawManyBones(Skeleton skeleton, DrawingContext drawingContext, List<Joint> joints)
        {
            int counter = 0;
            foreach (Joint joint in joints)
            {
                if (joint.TrackingState == JointTrackingState.NotTracked)
                    return;
                if (joint.TrackingState == JointTrackingState.Inferred)
                    counter++;
            }
            if (counter >= joints.Count)
                return;

            PathSegmentCollection psc = new PathSegmentCollection();

            foreach (Joint joint in joints)
            {
                PathSegment ps = new LineSegment(this.SkeletonPointToScreen(joint.Position), true);
                psc.Add(ps);
            }
            PathFigure pf = new PathFigure(this.SkeletonPointToScreen(joints[0].Position), psc, false);

            Geometry g = new PathGeometry(new[] { pf });
            drawingContext.DrawGeometry(null, stickyPen, g);
        }

        private void Normal_Click(object sender, RoutedEventArgs e)
        {
            optionSound.Stop();
            optionSound.Play();
            levelTime = 15;
            Hard.Foreground = new SolidColorBrush(Color.FromRgb(195, 195, 195));
            Normal.Foreground = new SolidColorBrush(Color.FromRgb(142, 0, 200));
        }

        private void Hard_Click(object sender, RoutedEventArgs e)
        {
            optionSound.Stop();
            optionSound.Play();
            levelTime = 7;
            Normal.Foreground = new SolidColorBrush(Color.FromRgb(195, 195, 195));
            Hard.Foreground = new SolidColorBrush(Color.FromRgb(142, 0, 200));
        }
    }
}
