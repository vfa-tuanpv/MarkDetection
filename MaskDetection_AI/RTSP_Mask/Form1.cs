using Emgu.CV;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RTSP_Mask
{
    public partial class Form1 : Form
    {
        private Capture capture;        //takes images from camera as image frames
        private bool captureInProgress; // checks if capture is executing
        public Capture cap;
        public Form1()
        {
            InitializeComponent();
            try
            {

                cap = new Capture(@"rtsp://<properIP>:554/live1.sdp?svc-t_fps_settings=3svc-header=enable");
                //  cap = new Capture(); //my usb camera
                cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, 640);
                cap.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, 480);
                MessageBox.Show(cap.CaptureSource.ToString());

                Application.Idle += new EventHandler(delegate (object sender, EventArgs e)
                {
                    Image<Gray, byte> obraz = cap.QueryFrame().ToImage<Gray, byte>();
                    // ^^^ here  -  FlipType = Cannot evaluate expression because a native frame is on top of the call stack.

                    pictureBox1.Image = obraz.ToBitmap();


                });
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }
    }
}
