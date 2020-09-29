using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace CustomVision
{
    public class CustomVisonAPI
    {
        public static string _liveDataUrl = "https://test-detection.cognitiveservices.azure.com/customvision/v3.0/Prediction/701b1611-c8be-4227-b09d-3e891b19f82d/detect/iterations/Iteration2/image";
        public static string _predictionKey = "63c7807c87014dd58069b4bd64efdd48";

        public static async void SaveSoftwareBitmapToFile(string url, string key, SoftwareBitmap softwareBitmap)
        {
            var data = await EncodedBytes(softwareBitmap, BitmapEncoder.JpegEncoderId);
            string result = await PredictImageContentsAsync(url, key, data);
        }

        private static async Task<byte[]> EncodedBytes(SoftwareBitmap soft, Guid encoderId)
        {
            byte[] array = null;

            // First: Use an encoder to copy from SoftwareBitmap to an in-mem stream (FlushAsync)
            // Next:  Use ReadAsync on the in-mem stream to get byte[] array

            using (var ms = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, ms);
                encoder.SetSoftwareBitmap(soft);

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception ex) { return new byte[0]; }

                array = new byte[ms.Size];
                await ms.ReadAsync(array.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
            }
            return array;
        }

        public static async Task<string> PredictImageContentsAsync(string url, string key, byte[] trainImages)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Prediction-key", key);
            //client.DefaultRequestHeaders.Add("Content-Type", "application/json");
            HttpResponseMessage response;
            using (var content = new ByteArrayContent(trainImages))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(url, content);
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            return resultJson;
        }
    }
}
