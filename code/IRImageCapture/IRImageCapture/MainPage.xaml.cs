/*
  IR Image Capture

  Simple near infrared image capture tool (UWP app) using Windows Perception API

  based on publicly availabe source code by Mike Taulty (https://mtaulty.com/2015/12/03/m_15988/)
  and kaorun55 (https://github.com/kaorun55/PerceptionSamples)

  MIT License

  Copyright (c) 2017, 2018 Matthias Deeg, SySS GmbH

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Perception;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace IRCapture
{
    [ComImport]
    [Guid("905a0fef-bc53-11df-8c49-001e4fc686da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IBufferByteAccess
    {
        unsafe void Buffer(out byte* pByte);
    }
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed partial class MainPage : Page
    {
        PerceptionInfraredFrameReader reader;
        WriteableBitmap bitmap1;
        WriteableBitmap bitmap2;
        WriteableBitmap bitmap_fg;
        WriteableBitmap bitmap_bg;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        async void OnLoaded(object sender, RoutedEventArgs args)
        {
            await InitIrFrame();
        }

        private async System.Threading.Tasks.Task InitIrFrame()
        {
            var access = await PerceptionInfraredFrameSource.RequestAccessAsync();
            if (access == PerceptionFrameSourceAccessStatus.Allowed)
            {
                var possibleSources = await PerceptionInfraredFrameSource.FindAllAsync();
                var firstSource = possibleSources.First();
                var videoProfile = firstSource.AvailableVideoProfiles.First();
                
                this.bitmap1 = new WriteableBitmap(videoProfile.Width, videoProfile.Height);
                this.bitmap2 = new WriteableBitmap(videoProfile.Width, videoProfile.Height);

                this.bitmap_fg = bitmap1;
                this.bitmap_bg = bitmap2;

                this.setcolor();

                this.myImage.Source = this.bitmap_fg;
                this.reader = firstSource.OpenReader();             
                this.reader.FrameArrived += HandleFrameArrivedAsync;
            }
        }

        async void HandleFrameArrivedAsync(PerceptionInfraredFrameReader sender,
          PerceptionInfraredFrameArrivedEventArgs args)
        {          
            // We move the whole thing to the dispatcher thread for now because we need to  
            // get back to the writeable bitmap and that's got affinity. We could probably  
            // do a lot better here.  
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, 
                () => {
                    this.HandleFrameArrivedDispatcherThread(args);
                }
            );
        }

        public async System.Threading.Tasks.Task saveImageAsync()
        {
            Stream pixelStream = this.bitmap1.PixelBuffer.AsStream();
            byte[] pixels = new byte[pixelStream.Length];
            await pixelStream.ReadAsync(pixels, 0, pixels.Length);

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            
            // dropdown menu of file types the user can save the file as
            savePicker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
            
            // default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "IR_Capture";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmap1.PixelWidth, (uint)bitmap1.PixelHeight, 96.0, 96.0, pixels);
                await encoder.FlushAsync();
            }
        }

        unsafe void setcolor()
        {
            unsafe
            {
                var destByteAccess = bitmap_bg.PixelBuffer as IBufferByteAccess;
                var pDestBits = (byte*)null;
                destByteAccess.Buffer(out pDestBits);

                for (int i = 0; i < 340 * 340; i++)
                {
                    *pDestBits++ = 0xff;
                    *pDestBits++ = 0x00;
                    *pDestBits++ = 0x00;
                    *pDestBits++ = 0xFF;
                }

                destByteAccess = bitmap_fg.PixelBuffer as IBufferByteAccess;
                destByteAccess.Buffer(out pDestBits);

                for (int i = 0; i < 340 * 340; i++)
                {

                    *pDestBits++ = 0x00;
                    *pDestBits++ = 0xff;
                    *pDestBits++ = 0x00;
                    *pDestBits++ = 0xFF;
                }
            }
        }

        unsafe void HandleFrameArrivedDispatcherThread(PerceptionInfraredFrameArrivedEventArgs args)
        {
            using (var frame = args.TryOpenFrame())
            {
                if (frame != null)
                {
                    unsafe
                    {
                        using (var bufferSource = frame.VideoFrame.SoftwareBitmap.LockBuffer(BitmapBufferAccessMode.Read))
                        using (var sourceReference = bufferSource.CreateReference())
                        {
                            var sourceByteAccess = sourceReference as IMemoryBufferByteAccess;
                            var pSourceBits = (byte*)null;
                            uint capacity = 0;
                            sourceByteAccess.GetBuffer(out pSourceBits, out capacity);

                            var destByteAccess = bitmap_bg.PixelBuffer as IBufferByteAccess;
                            var pDestBits = (byte*)null;
                            destByteAccess.Buffer(out pDestBits);

                            var bufferStart = pSourceBits;

                            for (int i = 0; i < (capacity); i++)
                            {
                                byte val = (byte)(*pSourceBits);

                                *pDestBits++ = val;
                                *pDestBits++ = val;
                                *pDestBits++ = val;
                                *pDestBits++ = 0xFF;

                                pSourceBits += 1;               // sizeof(Uint16)  
                            }
                        }
                    }

                    bitmap_bg.Invalidate();

                    if (bitmap_bg == bitmap1)
                    {
                        bitmap_bg = bitmap2;
                        bitmap_fg = bitmap1;
                    }
                    else
                    {
                        bitmap_bg = bitmap1;
                        bitmap_fg = bitmap2;
                    }
                }
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            await saveImageAsync();
        }
    }
}
