/*
 * Application: GaussianBlur
 * Solution:    GaussianBlur
 * Copyright:   Nk185. 2015 - 2016
 * startDate:   01.01.2016
 * endDate:     --.02.2016
 * Version:     00.42.16
 * Modification date: 15.02.16
 * Last modification:
 *  Highlighting function and dialogue to choose it.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Diagnostics;
using System.Threading;

namespace GaussianBlur
{
    public struct ColorStructure
    {
        public byte[,] Red;
        public byte[,] Green;
        public byte[,] Blue;
        public byte[,] GenericPadding;
    }

    public class GaussianDistribution
    {
        private readonly int blurRadius;
        private readonly sbyte highlightingCoef;
        private readonly bool useTwoDemensions;

        private double Div = 0.0;
        private double[] Kernel_SingleDemention;
        private double[,] Kernel_TwoDemention;

        private const int SigmaMultiply = 4; // Minimal value should be 3

        public double[] GetKernel { get { return Kernel_SingleDemention; } }
        public double[,] GetKernel2D { get { return Kernel_TwoDemention; } }
        public double GetDiv { get { return Div; } }
        public sbyte GetOffset { get { return highlightingCoef; } }
        public int GetRadius { get { return blurRadius * SigmaMultiply; } }

        public GaussianDistribution(int blurRadius, sbyte highlightingCoef, bool useTwoDemensions)
        {
            this.blurRadius = blurRadius;
            this.highlightingCoef = highlightingCoef;
            this.useTwoDemensions = useTwoDemensions;

            if (useTwoDemensions)
                CalculateKernel(out this.Kernel_TwoDemention);
            else
                CalculateKernel(out this.Kernel_SingleDemention);
        }

        private void CalculateKernel(out double[] result)
        {

            double[] resKernel = new double[(this.blurRadius * SigmaMultiply) * 2 + 1];
            int iOffset = 0;

            for (int i = -blurRadius * SigmaMultiply; i <= blurRadius * SigmaMultiply; i++)
            {
                resKernel[iOffset] = (1 / (Math.Sqrt(2 * Math.PI * blurRadius))) * Math.Exp(-(Math.Pow(i, 2)) / (2 * Math.Pow(blurRadius, 2)));
                Div += resKernel[iOffset];
                iOffset++;
            }

            result = resKernel;
        }
        private void CalculateKernel(out double[,] result)
        {

            double[,] resKernel = new double[(this.blurRadius * SigmaMultiply) * 2 + 1, (this.blurRadius * SigmaMultiply) * 2 + 1];
            int iOffset = 0;
            int jOffset = 0;

            for (int i = -blurRadius * SigmaMultiply; i <= blurRadius * SigmaMultiply; i++)
            {
                for (int j = -blurRadius * SigmaMultiply; j <= blurRadius * SigmaMultiply; j++)
                {
                    resKernel[iOffset, jOffset] = (1 / (Math.Sqrt(2 * Math.PI * Math.Pow(blurRadius, 2)))) * Math.Exp(-(i * i + j * j) / (2 * Math.Pow(blurRadius, 2)));

                    jOffset++;
                }

                iOffset++;
                jOffset = 0;
            }

            result = resKernel;
        }
    }
    public class GaussianBlurProcessing
    {
        public event Action<Bitmap, BitmapData, ColorStructure> Done;

        private readonly Bitmap _Bitmap;
        private readonly BitmapData _BitmapData;

        private GaussianDistribution _GaussianKernel;
        private ColorStructure _RGBValues;
        private Rectangle _BlurRect;
        private Thread _RChanelProcessing;
        private Thread _GChanelProcessing;
        private Thread _BChanelProcessing;

        private byte _FinishedChanelsCounter = 0; // This common variable should be used only for thread-control function which is: private void _ChanelDone()

        public GaussianBlurProcessing(GaussianDistribution gaussianKernel, ref ColorStructure rgbValues, Rectangle blurRectangle, Bitmap bmp, BitmapData bmpData)
        {
            this._GaussianKernel = gaussianKernel;
            this._RGBValues = rgbValues;
            this._BlurRect = blurRectangle;
            this._Bitmap = bmp;
            this._BitmapData = bmpData;
        }

        public void Start()
        {
            _RChanelProcessing = new Thread(() => _ProcessRedChanel());
            _GChanelProcessing = new Thread(() => _ProcessGreenChanel());
            _BChanelProcessing = new Thread(() => _ProcessBlueChanel());

            _RChanelProcessing.Start();
            _GChanelProcessing.Start();
            _BChanelProcessing.Start();
        }

        private void _ChanelDone()
        {
            if (_FinishedChanelsCounter == 3)
                if (Done != null)
                    Done(_Bitmap, _BitmapData, _RGBValues);
        }


        private void _ProcessRedChanel()
        {
            __ProcessRedH(this._RGBValues, this._GaussianKernel, this._BlurRect);
            __ProcessRedV(this._RGBValues, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _RChanelProcessing.Abort();
        }
        private void _ProcessGreenChanel()
        {
            __ProcessGreenH(this._RGBValues, this._GaussianKernel, this._BlurRect);
            __ProcessGreenV(this._RGBValues, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _GChanelProcessing.Abort();
        }
        private void _ProcessBlueChanel()
        {
            __ProcessBlueH(this._RGBValues, this._GaussianKernel, this._BlurRect);
            __ProcessBlueV(this._RGBValues, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _BChanelProcessing.Abort();
        }

        private void __ProcessRedH(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Left -> Right
            for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
            {
                for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;
                        if (j + rad >= blurRect.X && j + rad <= blurRect.X + blurRect.Width)
                        {
                            newValue += rgbValues.Red[i, j + rad] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Red[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;

                    if (rgbValues.Red[i, j] + gd.GetOffset <= 255 && rgbValues.Red[i, j] + gd.GetOffset >= 0)
                        rgbValues.Red[i, j] = (byte)(rgbValues.Red[i, j] + gd.GetOffset);
                    else if (rgbValues.Red[i, j] + gd.GetOffset <= 0)
                        rgbValues.Red[i, j] = 0;
                    else if (rgbValues.Red[i, j] + gd.GetOffset >= 255)
                        rgbValues.Red[i, j] = 255;
                }
            }
            #endregion
        }
        private void __ProcessRedV(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Up -> Down
            for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
            {
                for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr = 0;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;

                        if ((i + rad >= blurRect.Y) && (i + rad <= blurRect.Y + blurRect.Height))
                        {
                            newValue += rgbValues.Red[i + rad, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Red[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        private void __ProcessGreenH(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Left -> Right
            for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
            {
                for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;
                        if (j + rad >= blurRect.X && j + rad <= blurRect.X + blurRect.Width)
                        {
                            newValue += rgbValues.Green[i, j + rad] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Green[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;

                    if (rgbValues.Green[i, j] + gd.GetOffset <= 255 && rgbValues.Green[i, j] + gd.GetOffset >= 0)
                        rgbValues.Green[i, j] = (byte)(rgbValues.Green[i, j] + gd.GetOffset);
                    else if (rgbValues.Green[i, j] + gd.GetOffset <= 0)
                        rgbValues.Green[i, j] = 0;
                    else if (rgbValues.Green[i, j] + gd.GetOffset >= 255)
                        rgbValues.Green[i, j] = 255;
                }
            }
            #endregion
        }
        private void __ProcessGreenV(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Up -> Down
            for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
            {
                for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr = 0;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;

                        if ((i + rad >= blurRect.Y) && (i + rad <= blurRect.Y + blurRect.Height))
                        {
                            newValue += rgbValues.Green[i + rad, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Green[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        private void __ProcessBlueH(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Left -> Right
            for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
            {
                for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;
                        if (j + rad >= blurRect.X && j + rad <= blurRect.X + blurRect.Width)
                        {
                            newValue += rgbValues.Blue[i, j + rad] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Blue[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;

                    if (rgbValues.Blue[i, j] + gd.GetOffset <= 255 && rgbValues.Blue[i, j] + gd.GetOffset >= 0)
                        rgbValues.Blue[i, j] = (byte)(rgbValues.Blue[i, j] + gd.GetOffset);
                    else if (rgbValues.Blue[i, j] + gd.GetOffset <= 0)
                        rgbValues.Blue[i, j] = 0;
                    else if (rgbValues.Blue[i, j] + gd.GetOffset >= 255)
                        rgbValues.Blue[i, j] = 255;
                }
            }
            #endregion*/
        }
        private void __ProcessBlueV(ColorStructure rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Up -> Down
            for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
            {
                for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddr = 0;

                    for (int rad = -gd.GetRadius; rad <= gd.GetRadius; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;

                        if ((i + rad >= blurRect.Y) && (i + rad <= blurRect.Y + blurRect.Height))
                        {
                            newValue += rgbValues.Blue[i + rad, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    rgbValues.Blue[i, j] = Convert.ToByte(newValue / divisor);
                    newValue = 0.0;
                }
            }
            #endregion
        }
    }

    class GaussianBlur
    {
        /* This common variable should be used only for saving method */
        public static string outputFileAddr;

        private static Stopwatch timer1;

        [STAThread]
        static void Main(string[] Args)
        {
        Again:
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "JPEG (*.jpg)|*.jpg";
            openDlg.Title = "Select your image to blur: ";

            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                Bitmap bmp;
                BitmapData bmpData;
                ColorStructure rgbValues;
                GaussianDistribution gaussianDistribution;
                GaussianBlurProcessing gBlurProcessing;
                Rectangle rect;
                Rectangle blurRect;

                int[] userBlurRect = new int[4];
                int userBlurRadius;
                sbyte highlightingCoef;
                string userRectParams;
                string inputFileAddr;

                inputFileAddr = openDlg.FileName;
                outputFileAddr = Path.GetDirectoryName(inputFileAddr) + @"\[Result] " + (new FileInfo(inputFileAddr).Name);
                bmp = new Bitmap(inputFileAddr);
                userBlurRadius = Convert.ToInt32(Interaction.InputBox("Enter your blur radius.", "Blur radius selection", "1"));
                highlightingCoef = Convert.ToSByte(Interaction.InputBox("Enter your highlighting coefficient. It's should be in [-127 ; 127] \n\nTo save the same light level, leave default value: 0.",
                    "Highlighting coefficient selection", "0"));
                userRectParams = Interaction.InputBox("Enter your blur rectangle using spaces between values (x  y  width  height).\nTo blur all image, enter \"$gb_blurAll\". \nOr \"$gb_blurCentre\" to blur whole centre line" +
                "\n\nPlease, notice that sum of (x + width) must be lower or equal to " + Convert.ToString(bmp.Width) + ", and sum of (y + height) must be lower or equal to "
                + Convert.ToString(bmp.Height) + ".", "Blur rectangle selection", "$gb_blurAll");

                #region BlurRectangleSettingUp
                if (userRectParams != "$gb_blurAll" && userRectParams != "$gb_blurCentre" && userRectParams != "$gd_calcSingle" && userRectParams != "$gd_calc2D")
                {
                    int i = 0;
                    foreach (string value in userRectParams.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        userBlurRect[i] = Convert.ToInt32(value);
                        i++;
                    }

                    if ((userBlurRect[0] + userBlurRect[2] <= bmp.Width) && (userBlurRect[1] + userBlurRect[3] <= bmp.Width) && (userBlurRect[0] >= 0) & (userBlurRect[1] >= 0))
                        blurRect = new Rectangle(userBlurRect[0], userBlurRect[1], userBlurRect[2] - 1, userBlurRect[3] - 1);
                    else
                        throw new Exception("Uncorrect rectangle value(s). Please, restart application.");
                }
                else if (userRectParams == "$gb_blurAll")
                    blurRect = new Rectangle(0, 0, bmp.Width - 1, bmp.Height - 1);
                else if (userRectParams == "$gb_blurCentre")
                    blurRect = new Rectangle(0, (bmp.Height / 2) - (bmp.Height / 4), bmp.Width - 1, (bmp.Height / 2) - 1);
                else if (userRectParams == "$gd_calcSingle")
                {
                    GaussianDistribution gd = new GaussianDistribution(userBlurRadius, 0, false);
                    double[] gdk = gd.GetKernel;

                    foreach (double koef in gdk)
                        Console.WriteLine(koef);

                    using (Stream Str = new FileStream(Path.GetDirectoryName(inputFileAddr) + @"\GaussKernel.txt", FileMode.Create))
                    {
                        using (StreamWriter StrWriter = new StreamWriter(Str))
                        {
                            foreach (double koef in gdk)
                                StrWriter.WriteLine(koef);
                        }
                    }

                    gd = null;
                    gdk = null;

                    Console.Clear();

                    goto Again;
                }
                else if (userRectParams == "$gd_calc2D")
                {
                    GaussianDistribution gd = new GaussianDistribution(userBlurRadius, 0, true);
                    double[,] gdk = gd.GetKernel2D;

                    foreach (double koef in gdk)
                        Console.WriteLine(koef);

                    for (int i = 0; i < gdk.GetLength(0); i++)
                        using (Stream Str = new FileStream(Path.GetDirectoryName(inputFileAddr) + @"\GaussKernel2D[" + i + "].txt", FileMode.Create))
                        {
                            using (StreamWriter StrWriter = new StreamWriter(Str))
                            {
                                for (int j = 0; j < gdk.GetLength(1); j++)
                                    StrWriter.WriteLine(gdk[i, j]);
                            }
                        }

                    goto Again;
                }
                else
                    throw new Exception("Uncorrect rectangle value(s). Please, restart application.");
                #endregion

                Console.WriteLine("--------------------------------- [Summary] ---------------------------------");
                Console.WriteLine("> Choosed image: {0} ({1} x {2})", inputFileAddr, bmp.Width, bmp.Height);
                Console.WriteLine("> Choosed blur radius: {0}", userBlurRadius);
                Console.WriteLine("> Choosed highlighting coefficient: {0}", highlightingCoef);
                Console.WriteLine("> Choosed blur rectangle: x = {0} | y = {1} | width = {2} | height = {3}", blurRect.X, blurRect.Y, blurRect.Width + 1, blurRect.Height + 1);
                Console.WriteLine("\n> Output file name: {0}", outputFileAddr);
                Console.WriteLine("-----------------------------------------------------------------------------");
                Console.Write("> If it's correct press '1', otherwise press another key: ");

                if (Console.ReadKey().KeyChar != '1')
                {
                    Console.Clear();
                    goto Again;
                }

                gaussianDistribution = new GaussianDistribution(userBlurRadius, highlightingCoef, false);
                rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
                rgbValues = ConvertTo2D(ref bmpData, ref bmp);
                gBlurProcessing = new GaussianBlurProcessing(gaussianDistribution, ref rgbValues, blurRect, bmp, bmpData);
                gBlurProcessing.Done += gbp_Done;

                Console.WriteLine("\n\n> Processing image. It could take awhile...");
                timer1 = new Stopwatch();
                timer1.Start();
                gBlurProcessing.Start();

                Console.ReadLine();
            }
        }

        static void gbp_Done(Bitmap Bmp, BitmapData BmpData, ColorStructure rgbValues)
        {
            timer1.Stop();
            Console.WriteLine("> Processing completed in {0}.", timer1.Elapsed);

            Marshal.Copy(ConvertToSingle(rgbValues, Bmp.Height, Bmp.Width, BmpData.Stride), 0, BmpData.Scan0, Math.Abs(BmpData.Height * BmpData.Stride));
            Bmp.UnlockBits(BmpData);
            Bmp.Save(outputFileAddr);
        }

        public static ColorStructure ConvertTo2D(ref BitmapData bmpData, ref Bitmap bmp)
        {
            ColorStructure colors = new ColorStructure();
            byte[] commonRGB = new byte[bmpData.Stride];
            colors.Red = new byte[bmpData.Height, bmpData.Width];
            colors.Green = new byte[bmpData.Height, bmpData.Width];
            colors.Blue = new byte[bmpData.Height, bmpData.Width];
            colors.GenericPadding = new byte[bmpData.Height, Math.Abs(bmpData.Stride - (bmpData.Width * 3))];
            int commonOffset = 0;

            for (int i = 0; i < bmpData.Height; i++)
            {
                Marshal.Copy(bmpData.Scan0 + (i * Math.Abs(bmpData.Stride)), commonRGB, 0, Math.Abs(bmpData.Stride));

                commonOffset = 2;
                for (int j = 0; j < bmpData.Width; j++)
                {
                    colors.Red[i, j] = commonRGB[commonOffset];
                    commonOffset += 3;
                }

                commonOffset = 1;
                for (int j = 0; j < bmpData.Width; j++)
                {
                    colors.Green[i, j] = commonRGB[commonOffset];
                    commonOffset += 3;
                }

                commonOffset = 0;
                for (int j = 0; j < bmpData.Width; j++)
                {
                    colors.Blue[i, j] = commonRGB[commonOffset];
                    commonOffset += 3;
                }

                commonOffset = 0;
                for (int j = bmpData.Stride - (bmpData.Stride - (bmpData.Width * 3)); j < bmpData.Stride; j++)
                {
                    colors.GenericPadding[i, commonOffset] = commonRGB[j];
                    commonOffset++;
                }
            }

            return colors;
        }
        public static byte[] ConvertToSingle(ColorStructure colors, int height, int width, int stride)
        {
            byte[] Res = new byte[Math.Abs(stride) * height];
            int redOffset = 2;
            int greenOffset = 1;
            int blueOffset = 0;
            int paddingOffset;

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Res[redOffset] = colors.Red[i, j];
                    redOffset += 3;
                }

                for (int j = 0; j < width; j++)
                {
                    Res[greenOffset] = colors.Green[i, j];
                    greenOffset += 3;
                }

                for (int j = 0; j < width; j++)
                {
                    Res[blueOffset] = colors.Blue[i, j];
                    blueOffset += 3;
                }

                paddingOffset = (stride * i) + width * 3;
                for (int j = 0; j < colors.GenericPadding.GetLength(1); j++)
                {
                    Res[paddingOffset] = colors.GenericPadding[i, j];
                    paddingOffset++;
                }

                redOffset += colors.GenericPadding.GetLength(1);
                greenOffset += colors.GenericPadding.GetLength(1);
                blueOffset += colors.GenericPadding.GetLength(1);
            }

            return Res;
        }
    }
}