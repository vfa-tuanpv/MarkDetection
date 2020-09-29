using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace WeldingSpot_UWP
{
    public class SaveAIImage
    {
        public static SaveAIImage Instance = new SaveAIImage();

        private StorageFile savefile;

        public async void SelectFolder()
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            savePicker.FileTypeChoices.Add("Image", new List<string>() { ".jpg" });
            savePicker.SuggestedFileName = "Card" + DateTime.Now.ToString("yyyyMMddhhmmss");
            savefile = await savePicker.PickSaveFileAsync();
        }

        public async void Save(byte[] imageBytes, int width, int height)
        {
            try
            {
                if (savefile == null)
                {
                    return;
                }

                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(savefile.Path));
                    savefile = await folder.CreateFileAsync("Card" + DateTime.Now.Ticks + ".jpg");
                }

                using (IRandomAccessStream stream = await savefile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // Create an encoder with the desired format
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                    // Set the software bitmap
                    

                    encoder.SetSoftwareBitmap(SoftwareBitmap.CreateCopyFromBuffer(imageBytes.AsBuffer(),BitmapPixelFormat.Bgra8, width, height));

                    // Set additional encoding parameters, if needed
                    //encoder.BitmapTransform.ScaledWidth = 320;
                    //encoder.BitmapTransform.ScaledHeight = 240;
                    //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                    // encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.NearestNeighbor;
                    encoder.IsThumbnailGenerated = true;

                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch (Exception err)
                    {
                        const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                        switch (err.HResult)
                        {
                            case WINCODEC_ERR_UNSUPPORTEDOPERATION:
                                // If the encoder does not support writing a thumbnail, then try again
                                // but disable thumbnail generation.
                                encoder.IsThumbnailGenerated = false;
                                break;
                            default:
                                throw;
                        }
                    }

                    if (encoder.IsThumbnailGenerated == false)
                    {
                        await encoder.FlushAsync();
                    }


                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("log: " + ex.Message);
            }
        }
    }
}
