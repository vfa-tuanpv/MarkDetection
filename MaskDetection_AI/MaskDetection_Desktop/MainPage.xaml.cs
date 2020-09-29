using CustomVision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using WeldingSpot_UWP;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MaskDetection_Desktop
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture mediaCapture;
        bool isPreviewing = true;

        //for detection by AI
        private ObjectDetection objectDetection;
        private VideoFrame videoFrame;
        SoftwareBitmap bitmap;

        IBuffer buffer;
        IBuffer AIbuffer;
        private string ModelFilename = "model.onnx";
        private int frameWidth = 640;
        private int frameHeight = 480;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            WarningImage.Visibility = Visibility.Collapsed;
            InitTextBox();
            StartPreviewAsync();
            InitONNX();
            StartPullCameraFrames();
        }

        private async void StartPreviewAsync()
        {
            try
            {
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                DeviceInformation cameraDevice = allVideoDevices.FirstOrDefault();
                var mediaInitSettings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                mediaCapture = new MediaCapture();

                mediaCapture.CameraStreamStateChanged += MediaCapture_CameraStreamStateChanged;
                // mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;

                await mediaCapture.InitializeAsync(mediaInitSettings);
//                mediaCapture.VideoDeviceController.FocusControl.Configure(
//new FocusSettings { Mode = FocusMode.Auto });
//                await mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            try
            {
                mediaCapture.Failed -= _mediaCapture_Failed;
                mediaCapture.Failed += _mediaCapture_Failed;
                PreviewControl.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;


                videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, frameWidth, frameHeight);
                buffer = new Windows.Storage.Streams.Buffer((uint)(frameWidth * frameHeight * 8));
                AIbuffer = new Windows.Storage.Streams.Buffer((uint)(frameWidth * frameHeight * 8));

                //FacesCanvas.Width = frameHeight;
                //FacesCanvas.Height = frameHeight;

                Debug.WriteLine("Start Preview");
            }
            catch (System.IO.FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }
        }

        private void MediaCapture_CameraStreamStateChanged(MediaCapture sender, object args)
        {
            Debug.WriteLine(mediaCapture.CameraStreamState);
            if (sender.CameraStreamState == Windows.Media.Devices.CameraStreamState.Shutdown)
            {
                RunOnMainThread(() =>
                {
                    try
                    {
                        StartPreviewAsync();
                        StartPullCameraFrames();
                    }
                    catch (System.IO.FileLoadException)
                    {
                        mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
                    }
                });
            }
        }

        private void _mediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("_mediaCapture_Failed");
        }

        private void _mediaCapture_CameraStreamStateChanged(MediaCapture sender, object args)
        {
            Debug.WriteLine("_mediaCapture_CameraStreamStateChanged " + sender.CameraStreamState);
        }

        private void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                Debug.WriteLine("The camera preview can't be displayed because another app has exclusive access");
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                RunOnMainThread(() =>
                {
                    StartPreviewAsync();
                });
            }
        }

        private void RunOnMainThread(Windows.UI.Core.DispatchedHandler handler)
        {
            if (Dispatcher.HasThreadAccess)
            {
                handler.Invoke();
            }
            else
            {
                // Note: use a discard "_" to silence CS4014 warning
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, handler);
            }
        }

        private async void InitONNX()
        {
            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Model/{ModelFilename}"));
            objectDetection = new ObjectDetection(new List<string>() { "Mask", "No Mask" });
            await objectDetection.Init(modelFile);
        }

        Stopwatch stopwatchTake = new Stopwatch();

        public async void StartPullCameraFrames()
        {
            stopwatchTake.Start();
            // The position of the canvas also needs to be adjusted, as the size adjustment affects the centering of the control
            await Task.Run(async () =>
            {
                await Task.Delay(3000);
                for (; ; ) // Forever = While the app runs
                {
                    await Task.Delay(100);
                    try
                    {
                        if (!isPreviewing || mediaCapture == null)
                        {
                            continue;
                        }

                        //if (mediaCapture != null && mediaCapture.CameraStreamState != Windows.Media.Devices.CameraStreamState.Streaming)
                        //{
                        //    isPreviewing = false;
                        //    Debug.WriteLine(mediaCapture.CameraStreamState);
                        //}

                        var previewFrame = await mediaCapture.GetPreviewFrameAsync(videoFrame);
                        previewFrame.SoftwareBitmap.CopyToBuffer(buffer);

                        bitmap = SoftwareBitmap.CreateCopyFromBuffer(buffer, BitmapPixelFormat.Bgra8, frameWidth, frameHeight);
                        var frameAI = VideoFrame.CreateWithSoftwareBitmap(bitmap);
                        bitmap.CopyToBuffer(AIbuffer);
                        var predicts = await objectDetection.PredictImageAsync(frameAI).ConfigureAwait(false);



                        var loss = predicts.OrderBy(x => -(x.Probability));
                        //  var good = loss.Where(x => { return (x.Probability > 0.5f && x.TagName == "No Mask"); });
                        if (loss.Count() > 0)
                        {

                            var first = loss.First();
                            RunOnMainThread(async () =>
                            {
                                //await mediaCapture.VideoDeviceController.RegionsOfInterestControl.ClearRegionsAsync();
                                //// focus in the center of the screen
                                //await mediaCapture.VideoDeviceController.RegionsOfInterestControl.SetRegionsAsync(
                                //    new[]{

                                //    new RegionOfInterest() {Bounds = new Rect(first.BoundingBox.Left,first.BoundingBox.Top,0.02,0.02) }
                                //    });
                            });
                        }
                        else
                        {
                            RunOnMainThread(async () =>
                            {
                                //await mediaCapture.VideoDeviceController.RegionsOfInterestControl.ClearRegionsAsync();
                                //// focus in the center of the screen
                                //await mediaCapture.VideoDeviceController.RegionsOfInterestControl.SetRegionsAsync(
                                //    new[]{

                                //    new RegionOfInterest() {Bounds = new Rect(0.49f,0.49f,0.02,0.02) }
                                //    });
                            });
                        }

                        // var lossStr = string.Join(",  ", loss.Select(l => l.TagName + " " + (l.Probability * 100.0f).ToString("#0.00") + "%" + " Border:" + string.Format("x:{0},y:{1},w:{2},h:{3}", l.BoundingBox.Left, l.BoundingBox.Top, l.BoundingBox.Width, l.BoundingBox.Height)));
                        //string message = $"Evaluation took {TimeRecorder.ElapsedMilliseconds}ms to execute, Predictions: {lossStr}.";
                        //Debug.WriteLine(lossStr);
                        RunOnMainThread(() =>
                        {

                            ClearCanvas();
                            //DrawCanvas(0f, 0f, 1f, 1f);
                            bool noMask = false;
                            bool take = false;
                            foreach (var pre in predicts)
                            {
                
                                if (pre.Probability >= 0.5f && pre.TagName == "No Mask")
                                {
                                    noMask = true;

                                    if (AutoNoMaskCheckBox.IsChecked.Value)
                                    {
                                        take = true;
                                    }

                                    DrawCanvas(pre.BoundingBox.Left, pre.BoundingBox.Top, pre.BoundingBox.Width, pre.BoundingBox.Height, Colors.Red);
                                }

                                if (pre.Probability >= 0.5f && pre.TagName == "Mask")
                                {
                                    if (AutoMaskCheckBox.IsChecked.Value)
                                    {
                                        take = true;
                                    }

                                    DrawCanvas(pre.BoundingBox.Left, pre.BoundingBox.Top, pre.BoundingBox.Width, pre.BoundingBox.Height, Colors.Green);
                                }
                            }

                            if (noMask)
                            {
                                WarningImage.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                WarningImage.Visibility = Visibility.Collapsed;
                            }

                            if (take && stopwatchTake.ElapsedMilliseconds >= 500)
                            {
                                System.Diagnostics.Debug.WriteLine("Auto Take Photo");

                                TakePhoto(AIbuffer.ToArray());
                                stopwatchTake.Restart();
                            }
                        });
                    }

                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("log: " + ex.StackTrace);
                        return;
                    }
                    finally
                    {

                    }

                }
            });
        }

        private void ClearCanvas()
        {
            FacesCanvas.Children.Clear();
        }
        private void DrawCanvas(float left, float top, float width, float height, Color color)
        {
            if (left < 0 || top < 0)
            {
                return;
            }
            Rectangle faceBoundingBox = new Rectangle();
            faceBoundingBox.Width = width * (double)FacesCanvas.Width;
            faceBoundingBox.Height = height * (double)FacesCanvas.Height;
            Canvas.SetLeft(faceBoundingBox, left * (double)FacesCanvas.Width);
            Canvas.SetTop(faceBoundingBox, top * (double)FacesCanvas.Height);
            // Set bounding box stroke properties
            faceBoundingBox.StrokeThickness = 2;

            // Highlight the first face in the set
            faceBoundingBox.Stroke = new SolidColorBrush(color);

            // Add grid to canvas containing all face UI objects
            FacesCanvas.Children.Add(faceBoundingBox);
        }


        SaveAIImage saveAI = new SaveAIImage();
        private void InitTextBox()
        {
            URLTextBox.Text = CustomVisonAPI._liveDataUrl;
            PredictionKeyTextBox.Text = CustomVisonAPI._predictionKey;
            saveAI.SelectFolder();
        }

        private void TakePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            TakePhoto(buffer.ToArray());
        }

        private void TakePhoto(byte[] data)
        {
            saveAI.Save(data, frameWidth, frameHeight);
            if (UploadCheckBox.IsChecked.Value && !string.IsNullOrEmpty(URLTextBox.Text) && !string.IsNullOrEmpty(PredictionKeyTextBox.Text))
            {
                CustomVisonAPI.SaveSoftwareBitmapToFile(URLTextBox.Text, PredictionKeyTextBox.Text, bitmap);
            }
        }
    }
}

