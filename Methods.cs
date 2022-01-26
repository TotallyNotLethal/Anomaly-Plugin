using IronOcr;
using RuriLib.Attributes;
using RuriLib.Logging;
using RuriLib.Models.Bots;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;

namespace Anomaly.Blocks.OCR
{
    [BlockCategory("Captchas", "Block that solves image/gif captchas", "#9acd32")]
    public class Methods
    {
        [Block("Solves image based captcha using IronOCR", name = "OCR")]
        public static List<string> Solve(BotData data, bool imageIsBase64, bool isGif, bool readBarCodes, bool pdfAndHOCR, string customTrainedData, string captchaUrl, string ocrCharBlacklist, string ocrCharWhitelist, /*bool test_AutoSolve, */bool greyScale, bool binarize, bool enhanceResolution, bool sharpen, bool contrast, bool invert, bool deskew, int rotation, bool houghStraighten, bool removeNoise, bool noiseDeepClean, bool dilate, bool erode, float CUSTOMNOISE, bool CUSTOMDILATE, float CUSTOMCONTRAST, float CUSTOMGAMMA, float CUSTOMBRIGHTNESS, int CUSTOMSATURATION, string setColorTransparent, int transparentTolerance = 10)
        {
            List<string> ocr = new List<string>();
            string pluginDir = Directory.GetCurrentDirectory() + @"\UserData\Plugins\AnomalyPlugin";
            Bitmap appliedCaptcha;
            Image captcha;
            Bitmap bCaptcha;
            Bitmap[] framedCaptcha;
            double gifConfidence = 0;

            if (imageIsBase64)
            {
                byte[] imageBytes = Convert.FromBase64String(captchaUrl);
                using (var ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
                {
                    captcha = (Image)Bitmap.FromStream(ms);
                    bCaptcha = (Bitmap)Bitmap.FromStream(ms);
                    appliedCaptcha = CreateNonIndexedImage(bCaptcha);
                }
            }
            else
            {
                Uri requestUri = new Uri(captchaUrl);
                HttpWebRequest request = HttpWebRequest.CreateHttp(requestUri);
                request.CookieContainer = new CookieContainer();
                foreach (var h in data.HEADERS)
                {
                    if(!data.HEADERS.ContainsValue(h.Key))
                        request.Headers.Add(h.Key, h.Value);
                }
                foreach (var c in data.COOKIES)
                {
                    if (!data.COOKIES.ContainsValue(c.Key))
                        request.CookieContainer.Add(requestUri, new Cookie(c.Key, c.Value));
                }
                var response = request.GetResponse();
                captcha = Image.FromStream(response.GetResponseStream());
                appliedCaptcha = CreateNonIndexedImage((Bitmap)captcha);
            }


            var output = new List<string>();
            IronTesseract tesseract = new IronTesseract();
            tesseract.Configuration.TesseractVersion = TesseractVersion.Tesseract5;
            tesseract.Configuration.EngineMode = TesseractEngineMode.TesseractAndLstm;
            tesseract.Configuration.PageSegmentationMode = TesseractPageSegmentationMode.AutoOsd;
            tesseract.Configuration.WhiteListCharacters = ocrCharWhitelist;
            tesseract.Configuration.BlackListCharacters = ocrCharBlacklist;
            tesseract.Configuration.ReadBarCodes = readBarCodes;
            tesseract.Configuration.RenderSearchablePdfsAndHocr = pdfAndHOCR;
            if (customTrainedData.Length > 0)
                tesseract.UseCustomTesseractLanguageFile($"pluginDir\\tessdata\\{customTrainedData}.traineddata");
            
            tesseract.MultiThreaded = true;

            OcrInput Input;
            OcrResult solvedString;

            if (isGif)
            {
                framedCaptcha = getFrames((Bitmap)captcha);
                int frame = 1;

                foreach (Bitmap image in framedCaptcha)
                {
                    data.Logger.Log($"Attempting frame: #{frame}", LogColors.Buff);
                    using (Input = new OcrInput(image))
                    {
                        /*if (test_AutoSolve)
                        {
                            List<string> bestConfidence = new List<string>();
                            Input.Sharpen();
                            Input.Contrast();
                            Input.DeNoise();
                            Input.Dilate(true);
                        }
                        else
                        {*/
                        //if(scale)
                        if (greyScale)
                        {
                            Input.ToGrayScale();
                            data.Logger.Log($"Image set to greyscale", LogColors.Orange);
                        }
                        if (binarize)
                        {
                            Input.Binarize();
                            data.Logger.Log($"Image Binarized", LogColors.Orange);
                        }
                        if (enhanceResolution)
                        {
                            Input.EnhanceResolution();
                            data.Logger.Log($"Image Enhanced", LogColors.Orange);
                        }
                        if (sharpen)
                        {
                            Input.Sharpen();
                            data.Logger.Log($"Image Sharpened", LogColors.Orange);
                        }
                        if (contrast)
                        {
                            Input.Contrast();
                            data.Logger.Log($"Image Contrast set", LogColors.Orange);
                        }
                        if (invert)
                        {
                            Input.Invert();
                            data.Logger.Log($"Image Inverted", LogColors.Orange);
                        }
                        if (deskew)
                        {
                            Input.Deskew();
                            data.Logger.Log($"Image Deskewed", LogColors.Orange);
                        }
                        if (rotation > 0)
                        {
                            Input.Rotate(rotation);
                            data.Logger.Log($"Image Rotated by: " + rotation + " degrees", LogColors.Orange);
                        }
                        if (houghStraighten)
                        {
                            Input.HoughTransformStraighten();
                            data.Logger.Log($"Image set to nearest 90 degree rotation of straightness", LogColors.Orange);
                        }
                        if (removeNoise)
                        {
                            Input.DeNoise();
                            data.Logger.Log($"Image Denoised", LogColors.Orange);
                        }
                        if (noiseDeepClean)
                        {
                            Input.DeepCleanBackgroundNoise();
                            data.Logger.Log($"Image Denoised like.. a lot", LogColors.Orange);
                        }
                        if (dilate)
                        {
                            Input.Dilate(false);
                            data.Logger.Log($"Image Dilated", LogColors.Orange);
                        }
                        if (erode)
                        {
                            Input.Erode();
                            data.Logger.Log($"Image Eroded", LogColors.Orange);
                        }
                        if (setColorTransparent.Length > 0)
                        {
                            setColorTransparent = string.Concat(setColorTransparent[0].ToString().ToUpper(), setColorTransparent.AsSpan(1));
                            Input.ReplaceColor(ColorTranslator.FromHtml(setColorTransparent), Color.Transparent, transparentTolerance);
                            data.Logger.Log($"Color {setColorTransparent} magically disappeared", LogColors.Orange);
                        }

                        foreach (var p in Input.Pages)
                        {
                            appliedCaptcha = p.ToBitmap();
                        }

                        if (CUSTOMCONTRAST > 0 || CUSTOMGAMMA > 0 || CUSTOMBRIGHTNESS > 0)
                            appliedCaptcha = SetConGamBri(appliedCaptcha, CUSTOMBRIGHTNESS, CUSTOMCONTRAST, CUSTOMGAMMA);
                        if (CUSTOMSATURATION > 0)
                            appliedCaptcha = SetSaturation(appliedCaptcha, CUSTOMSATURATION);
                        if (CUSTOMNOISE > 0)
                            appliedCaptcha = RemoveNoiseThreshold(appliedCaptcha, CUSTOMNOISE);
                        if (CUSTOMDILATE)
                            appliedCaptcha = DilateImage(appliedCaptcha);
                        //}

                        Input.Dispose();
                    }
                    solvedString = tesseract.Read(appliedCaptcha);

                    if (solvedString.Confidence > gifConfidence)
                    {
                        output.Clear();
                        ocr.Add(solvedString.Text);
                        ocr.Add(solvedString.Confidence.ToString());
                        data.Logger.Log($"Best chance frame set to: #{frame}", LogColors.Jasper);
                    }
                    frame++;
                }
            }
            else
            {
                using (Input = new OcrInput(appliedCaptcha))
                {
                    /*if (test_AutoSolve)
                    {
                        List<string> bestConfidence = new List<string>();
                        Input.Sharpen();
                        Input.Contrast();
                        Input.DeNoise();
                        Input.Dilate(true);
                    }
                    else
                    {*/
                    //if(scale)
                    if (greyScale)
                    {
                        Input.ToGrayScale();
                        data.Logger.Log($"Image set to greyscale", LogColors.Orange);
                    }
                    if (binarize)
                    {
                        Input.Binarize();
                        data.Logger.Log($"Image Binarized", LogColors.Orange);
                    }
                    if (enhanceResolution)
                    {
                        Input.EnhanceResolution();
                        data.Logger.Log($"Image Enhanced", LogColors.Orange);
                    }
                    if (sharpen)
                    {
                        Input.Sharpen();
                        data.Logger.Log($"Image Sharpened", LogColors.Orange);
                    }
                    if (contrast)
                    {
                        Input.Contrast();
                        data.Logger.Log($"Image Contrast set", LogColors.Orange);
                    }
                    if (invert)
                    {
                        Input.Invert();
                        data.Logger.Log($"Image Inverted", LogColors.Orange);
                    }
                    if (deskew)
                    {
                        Input.Deskew();
                        data.Logger.Log($"Image Deskewed", LogColors.Orange);
                    }
                    if (rotation > 0)
                    {
                        Input.Rotate(rotation);
                        data.Logger.Log($"Image Rotated by: " + rotation + " degrees", LogColors.Orange);
                    }
                    if (houghStraighten)
                    {
                        Input.HoughTransformStraighten();
                        data.Logger.Log($"Image set to nearest 90 degree rotation of straightness", LogColors.Orange);
                    }
                    if (removeNoise)
                    {
                        Input.DeNoise();
                        data.Logger.Log($"Image Denoised", LogColors.Orange);
                    }
                    if (noiseDeepClean)
                    {
                        Input.DeepCleanBackgroundNoise();
                        data.Logger.Log($"Image Denoised like.. a lot", LogColors.Orange);
                    }
                    if (dilate)
                    {
                        Input.Dilate(false);
                        data.Logger.Log($"Image Dilated", LogColors.Orange);
                    }
                    if (erode)
                    {
                        Input.Erode();
                        data.Logger.Log($"Image Eroded", LogColors.Orange);
                    }
                    if (setColorTransparent.Length > 0)
                    {
                        setColorTransparent = string.Concat(setColorTransparent[0].ToString().ToUpper(), setColorTransparent.AsSpan(1));
                        Input.ReplaceColor(ColorTranslator.FromHtml(setColorTransparent), Color.Transparent, transparentTolerance);
                        data.Logger.Log($"Color {setColorTransparent} magically disappeared", LogColors.Orange);
                    }

                    foreach (var p in Input.Pages)
                    {
                        appliedCaptcha = p.ToBitmap();
                    }

                    if (CUSTOMCONTRAST > 0 || CUSTOMGAMMA > 0 || CUSTOMBRIGHTNESS > 0)
                        appliedCaptcha = SetConGamBri(appliedCaptcha, CUSTOMBRIGHTNESS, CUSTOMCONTRAST, CUSTOMGAMMA);
                    if (CUSTOMSATURATION > 0)
                        appliedCaptcha = SetSaturation(appliedCaptcha, CUSTOMSATURATION);
                    if (CUSTOMNOISE > 0)
                        appliedCaptcha = RemoveNoiseThreshold(appliedCaptcha, CUSTOMNOISE);
                    if (CUSTOMDILATE)
                        appliedCaptcha = DilateImage(appliedCaptcha);

                    solvedString = tesseract.Read(appliedCaptcha);
                    ocr.Add(solvedString.Text);
                    ocr.Add(solvedString.Confidence.ToString());

                    Input.Dispose();
                }
            }
        return ocr;
        }

        //public Bitmap RunAllFrames(Bitmap[] frameCaptcha)
        //{
        //    Dictionary<string, int> dictionary = new Dictionary<string, int>();

        //    int choose = 0;
        //    string output = "";
        //    string[] words = new string[frameCaptcha.Length];
        //    string autoChoose = "";
        //    foreach (Bitmap image in frameCaptcha)
        //    {
        //        Bitmap settingsImage = ApplySettingsToFrame(image);
        //        var OCRTess = new TesseractEngine(@".\tessdata", OcrLang, EngineMode.Default);
        //        Pix OCRimage = PixConverter.ToPix(settingsImage);
        //        string chosenText = OCRTess.Process(OCRimage).GetText().Trim();
        //        words[choose] = chosenText;
        //        output += choose + ": " + chosenText + Environment.NewLine;
        //        if (chosenText.Length == 4 && chosenText.All(Char.IsLetter))
        //            autoChoose = chosenText;
        //        OCRTess.Dispose();
        //        choose++;
        //    }

        //    foreach (string word in words)
        //    {
        //        if (dictionary.ContainsKey(word))
        //        {
        //            dictionary[word] += 1;
        //        }
        //        else
        //        {
        //            dictionary.Add(word, 1);
        //        }
        //    }
        //    var keyAndValue = dictionary.OrderBy(kvp => kvp.Value).First();
        //    //MessageBox.Show(output);

        //    //MessageBox.Show("Best automatic choice: " + keyAndValue.Key);

        //    Bitmap chosenFrame = frameCaptcha[choose];
        //    return chosenFrame;
        //}

        public static Bitmap CreateNonIndexedImage(Bitmap src)
        {
            Bitmap newBmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            using (Graphics gfx = Graphics.FromImage(newBmp))
            {
                gfx.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
                gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                gfx.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
            }

            return newBmp;
        }

        public static Bitmap SetConGamBri(Bitmap original, float brightness, float contrast, float gamma)
        {
            brightness = brightness + 1;
            contrast = contrast + 1;
            gamma = gamma + 1;

            Bitmap adjustedImage = original;

            float adjustedBrightness = brightness - 1.0f;
            // create matrix that will brighten and contrast the image
            float[][] ptsArray ={
        new float[] { contrast, 0, 0, 0, 0}, // scale red
        new float[] {0, contrast, 0, 0, 0}, // scale green
        new float[] {0, 0, contrast, 0, 0}, // scale blue
        new float[] {0, 0, 0, 1.0f, 0}, // don't scale alpha
        new float[] {adjustedBrightness, adjustedBrightness, adjustedBrightness, 0, 1}};

            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.ClearColorMatrix();
            imageAttributes.SetColorMatrix(new ColorMatrix(ptsArray), ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            imageAttributes.SetGamma(gamma, ColorAdjustType.Bitmap);
            Graphics g = Graphics.FromImage(adjustedImage);
            g.DrawImage(original, new Rectangle(0, 0, adjustedImage.Width, adjustedImage.Height)
                , 0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel, imageAttributes);
            return adjustedImage;
        }

        public static Bitmap SetSaturation(Bitmap original, int saturation)
        {
            int plusSat = saturation;
            System.Drawing.Color newColor;

            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    int red = (original.GetPixel(x, y).R + plusSat > 255 ? 255 : (original.GetPixel(x, y).R + plusSat)); //Int32.Parse(original.GetPixel(x, y).R*0.5);
                    int green = (original.GetPixel(x, y).G + plusSat > 255 ? 255 : (original.GetPixel(x, y).G + plusSat)); ;
                    int blue = (original.GetPixel(x, y).B + plusSat > 255 ? 255 : (original.GetPixel(x, y).B + plusSat)); ;
                    newColor = System.Drawing.Color.FromArgb(red, green, blue);
                    original.SetPixel(x, y, newColor);
                }
            }
            return original;
        }

        static Bitmap[] getFrames(Bitmap originalImg)
        {
            int numberOfFrames = originalImg.GetFrameCount(FrameDimension.Time);
            Bitmap[] frames = new Bitmap[numberOfFrames];

            for (int i = 0; i < numberOfFrames; i++)
            {
                originalImg.SelectActiveFrame(FrameDimension.Time, i);
                frames[i] = ((Bitmap)originalImg.Clone());
            }

            //MessageBox.Show("Number of frames on image: " + numberOfFrames);
            return frames;
        }

        public static Bitmap RemoveNoiseThreshold(Bitmap Bmp, float CUSTOMNOISE)
        {
            // Make the result bitmap.
            Bitmap bm = new Bitmap(Bmp.Width, Bmp.Height);

            // Make the ImageAttributes object and set the threshold.
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetThreshold(CUSTOMNOISE);



            // Draw the image onto the new bitmap
            // while applying the new ColorMatrix.
            Point[] points =
            {
                new Point(0, 0),
                new Point(Bmp.Width, 0),
                new Point(0, Bmp.Height),
            };
            Rectangle rect =
                new Rectangle(0, 0, Bmp.Width, Bmp.Height);
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.DrawImage(Bmp, points, rect,
                    GraphicsUnit.Pixel, attributes);
            }

            // Return the result.
            return FillWhitespace(bm);
        }

        public static Bitmap FillWhitespace(Bitmap Bmp)
        {
            for (int a = 0; a < 3; a++)
                for (int i = 2; i < Bmp.Width - 2; i++)
                    for (int j = 2; j < Bmp.Height - 2; j++)
                    {
                        Color setColor = Bmp.GetPixel(i, j);
                        if (Bmp.GetPixel(i, j).R + Bmp.GetPixel(i, j).B + Bmp.GetPixel(i, j).G < 760)
                        {
                            //corners
                            Bmp.SetPixel(i - 1, j - 1, setColor);
                            Bmp.SetPixel(i - 1, j + 1, setColor);
                            Bmp.SetPixel(i + 1, j - 1, setColor);
                            Bmp.SetPixel(i + 1, j + 1, setColor);
                            //edges
                            Bmp.SetPixel(i, j - 1, setColor);
                            Bmp.SetPixel(i - 1, j, setColor);
                            Bmp.SetPixel(i + 1, j, setColor);
                            Bmp.SetPixel(i, j + 1, setColor);
                            i++;
                            j++;
                        }
                    }

            return Bmp;
        }



        public static Bitmap DilateImage(Bitmap SrcImage)
        {
            // Create Destination bitmap.
            Bitmap tempbmp = new Bitmap(SrcImage.Width, SrcImage.Height);

            // Take source bitmap data.
            BitmapData SrcData = SrcImage.LockBits(new Rectangle(0, 0,
                SrcImage.Width, SrcImage.Height), ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            // Take destination bitmap data.
            BitmapData DestData = tempbmp.LockBits(new Rectangle(0, 0, tempbmp.Width,
                tempbmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            // Element array to used to dilate.
            byte[,] sElement = new byte[5, 5] {
        {0,0,1,0,0},
        {0,1,1,1,0},
        {1,1,1,1,1},
        {0,1,1,1,0},
        {0,0,1,0,0}
    };

            // Element array size.
            int size = 5;
            byte max, clrValue;
            int radius = size / 2;
            int ir, jr;

            unsafe
            {
                // Loop for Columns.
                for (int colm = radius; colm < DestData.Height - radius; colm++)
                {
                    // Initialise pointers to at row start.
                    byte* ptr = (byte*)SrcData.Scan0 + (colm * SrcData.Stride);
                    byte* dstPtr = (byte*)DestData.Scan0 + (colm * SrcData.Stride);

                    // Loop for Row item.
                    for (int row = radius; row < DestData.Width - radius; row++)
                    {
                        max = 0;
                        clrValue = 0;

                        // Loops for element array.
                        for (int eleColm = 0; eleColm < 5; eleColm++)
                        {
                            ir = eleColm - radius;
                            byte* tempPtr = (byte*)SrcData.Scan0 +
                                ((colm + ir) * SrcData.Stride);

                            for (int eleRow = 0; eleRow < 5; eleRow++)
                            {
                                jr = eleRow - radius;

                                // Get neightbour element color value.
                                clrValue = (byte)((tempPtr[row * 3 + jr] +
                                    tempPtr[row * 3 + jr + 1] + tempPtr[row * 3 + jr + 2]) / 3);

                                if (max < clrValue)
                                {
                                    if (sElement[eleColm, eleRow] != 0)
                                        max = clrValue;
                                }
                            }
                        }

                        dstPtr[0] = dstPtr[1] = dstPtr[2] = max;

                        ptr += 3;
                        dstPtr += 3;
                    }
                }
            }

            // Dispose all Bitmap data.
            SrcImage.UnlockBits(SrcData);
            tempbmp.UnlockBits(DestData);

            // return dilated bitmap.
            return tempbmp;
        }
    }
}