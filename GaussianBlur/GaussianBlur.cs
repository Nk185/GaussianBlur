﻿/*
 * Application: GaussianBlur
 * Solution:    GaussianBlur
 * Copyright:   Nk185. 2015 - 2016
 * Version:     00.42.16
 * Modifications log:
 *  10.03.16 - Calculations (2D) improvements
 *  29.02.16 - Calculations (1D) improvements
 *  24.02.16 - Added 2D matrix support
 *  17.02.16 - Useless code removed
 *  16.02.16 - Pre-processing info 
 * Watching next lines you automatically agree with this: https://goo.gl/M5bjl6
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
        private readonly int   blurRadius;
        private readonly sbyte highlightingCoef;
        private readonly bool  useTwoDemensions;
         
        private double    Div = 0.0;
        private double[]  Kernel_SingleDemention;
        private double[,] Kernel_TwoDemention;

        private const int SigmaMultiply = 4; // Minimal value should be 3
                
        public int       GetRadius { get { return blurRadius * SigmaMultiply; } }
        public sbyte     GetOffset { get { return highlightingCoef; } }
        public double    GetDiv { get { return Div; } }
        public double[]  GetKernel { get { return Kernel_SingleDemention; } }
        public double[,] GetKernel2D { get { return Kernel_TwoDemention; } }

        public GaussianDistribution(int blurRadius, sbyte highlightingCoef, bool useTwoDemensions)
        {
            this.blurRadius       = blurRadius;
            this.highlightingCoef = highlightingCoef;
            this.useTwoDemensions = useTwoDemensions;

            if (useTwoDemensions)
                CalculateKernel(out this.Kernel_TwoDemention);
            else
                CalculateKernel(out this.Kernel_SingleDemention);
        }

        private void CalculateKernel(out double[] result)
        {
            int iOffset = 0;
            double[] resKernel = new double[(this.blurRadius * SigmaMultiply) * 2 + 1];            

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
            int iOffset = 0;
            int jOffset = 0;
            double[,] resKernel = new double[(this.blurRadius * SigmaMultiply) * 2 + 1, (this.blurRadius * SigmaMultiply) * 2 + 1];

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

        protected readonly Bitmap     _Bitmap;
        protected readonly BitmapData _BitmapData;

        protected GaussianDistribution _GaussianKernel;
        protected ColorStructure _RGBValues;
        protected Rectangle _BlurRect;
        protected Thread _RChanelProcessing;
        protected Thread _GChanelProcessing;
        protected Thread _BChanelProcessing;

        protected byte _FinishedChanelsCounter = 0; // This common variable should be used only for thread-control function which is: private void _ChanelDone()

        public GaussianBlurProcessing(GaussianDistribution gaussianKernel, ColorStructure rgbValues, Rectangle blurRectangle, Bitmap bmp, BitmapData bmpData)
        {
            this._GaussianKernel = gaussianKernel;
            this._RGBValues      = rgbValues;
            this._BlurRect       = blurRectangle;
            this._Bitmap         = bmp;
            this._BitmapData     = bmpData;
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

        protected void _ChanelDone()
        {
            if (_FinishedChanelsCounter == 3)
                if (Done != null)
                    Done(_Bitmap, _BitmapData, _RGBValues);                    
        }


        protected virtual void _ProcessRedChanel()
        {
            __ProcessH(ref this._RGBValues.Red, this._GaussianKernel, this._BlurRect);
            __ProcessV(ref this._RGBValues.Red, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _RChanelProcessing.Abort();
        }
        protected virtual void _ProcessGreenChanel()
        {
            __ProcessH(ref this._RGBValues.Green, this._GaussianKernel, this._BlurRect);
            __ProcessV(ref this._RGBValues.Green, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _GChanelProcessing.Abort();
        }
        protected virtual void _ProcessBlueChanel()
        {
            __ProcessH(ref this._RGBValues.Blue, this._GaussianKernel, this._BlurRect);
            __ProcessV(ref this._RGBValues.Blue, this._GaussianKernel, this._BlurRect);

            _FinishedChanelsCounter++;
            _ChanelDone();
            _BChanelProcessing.Abort();
        }

        private void __ProcessH(ref byte[,] rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Left -> Right
            double newValue = 0.0;
            double divisor = 0.0;
            int kernelAddr;
            byte[,] bufferCh = new byte[rgbValues.GetLength(0), rgbValues.GetLength(1)];

            for (int i = 0; i < rgbValues.GetLength(0); i++)
                for (int j = 0; j < rgbValues.GetLength(1); j++)
                    bufferCh[i, j] = rgbValues[i, j];

            for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
            {
                for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
                {              
                    
                    for (int rad = -(gd.GetRadius - 1); rad <= gd.GetRadius - 1; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;

                        if (j + rad >= blurRect.X && j + rad <= blurRect.X + blurRect.Width)
                        {
                            newValue += rgbValues[i, j + rad] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    bufferCh[i, j] = (byte)(newValue / divisor);
                    newValue = 0.0;
                    divisor = 0.0;

                    if (gd.GetOffset != 0)
                    if (bufferCh[i, j] + gd.GetOffset <= 255 && bufferCh[i, j] + gd.GetOffset >= 0)
                        bufferCh[i, j] = (byte)(bufferCh[i, j] + gd.GetOffset);
                    else if (bufferCh[i, j] + gd.GetOffset <= 0)
                        bufferCh[i, j] = 0;
                    else if (bufferCh[i, j] + gd.GetOffset >= 255)
                        bufferCh[i, j] = 255;
                }
            }

            rgbValues = bufferCh;
            #endregion
        }

        private void __ProcessV(ref byte[,] rgbValues, GaussianDistribution gd, Rectangle blurRect)
        {
            #region Up -> Down
            double newValue = 0.0;
            double divisor = 0.0;
            int kernelAddr;
            byte[,] bufferCh = new byte[rgbValues.GetLength(0), rgbValues.GetLength(1)];

            for (int i = 0; i < rgbValues.GetLength(0); i++)
                for (int j = 0; j < rgbValues.GetLength(1); j++)
                    bufferCh[i, j] = rgbValues[i, j];

            for (int j = blurRect.X; j <= blurRect.X + blurRect.Width; j++)
            {
                for (int i = blurRect.Y; i <= blurRect.Y + blurRect.Height; i++)
                {
                    for (int rad = -(gd.GetRadius - 1); rad <= gd.GetRadius - 1; rad++)
                    {
                        kernelAddr = rad + gd.GetRadius;

                        if ((i + rad >= blurRect.Y) && (i + rad <= blurRect.Y + blurRect.Height))
                        {
                            newValue += rgbValues[i + rad, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                        }
                    }

                    bufferCh[i, j] = (byte)(newValue / divisor);
                    newValue = 0.0;
                    divisor = 0.0;
                }
            }

            rgbValues = bufferCh;
            #endregion
        }
    }
    public class GaussianBlurProcessing2D : GaussianBlurProcessing
    {
        public GaussianBlurProcessing2D (GaussianDistribution gaussianKernel, ColorStructure rgbValues, Rectangle blurRectangle, Bitmap bmp, BitmapData bmpData)
            : base(gaussianKernel, rgbValues, blurRectangle, bmp, bmpData)
        {
           
        }

        protected sealed override void _ProcessRedChanel()
        {
            byte[,] bufferCh = new byte[this._RGBValues.Red.GetLength(0), this._RGBValues.Red.GetLength(1)];
            
            for (int i = 0; i < bufferCh.GetLength(0); i++)
                for (int j = 0; j < bufferCh.GetLength(1); j++)
                    bufferCh[i, j] = this._RGBValues.Red[i, j];

            for (int i = this._BlurRect.Y; i <= this._BlurRect.Y + this._BlurRect.Height; i++)
            {
                for (int j = this._BlurRect.X; j <= this._BlurRect.X + this._BlurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddrX;
                    int kernelAddrY;

                    for (int x = -(this._GaussianKernel.GetRadius - 1); x <= this._GaussianKernel.GetRadius - 1; x++)
                    {
                        for (int y = -(this._GaussianKernel.GetRadius - 1); y <= this._GaussianKernel.GetRadius - 1; y++)
                        {
                            kernelAddrX = this._GaussianKernel.GetRadius + x;
                            kernelAddrY = this._GaussianKernel.GetRadius + y;


                            if ((j + x >= this._BlurRect.X) && (j + x <= this._BlurRect.X + this._BlurRect.Width) && 
                                (i + y >= this._BlurRect.Y) && (i + y <= this._BlurRect.Y + this._BlurRect.Height))
                            {
                                newValue += this._RGBValues.Red[i + y, j + x] * this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                                divisor += this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                            }
                        }
                    }

                    bufferCh[i, j] = (byte)(newValue / divisor);
                    newValue = 0.0;

                    if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 255 && bufferCh[i, j] + this._GaussianKernel.GetOffset >= 0)
                        bufferCh[i, j] = (byte)(bufferCh[i, j] + this._GaussianKernel.GetOffset);
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 0)
                        bufferCh[i, j] = 0;
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset >= 255)
                        bufferCh[i, j] = 255;                    
                }

                Console.WriteLine("{0} out {1}", i, this._BlurRect.Y + this._BlurRect.Height);
            }

            this._RGBValues.Red = bufferCh;
            _FinishedChanelsCounter++;
            _ChanelDone();
            _RChanelProcessing.Abort();
        }

        protected sealed override void _ProcessGreenChanel()
        {
            byte[,] bufferCh = new byte[this._RGBValues.Green.GetLength(0), this._RGBValues.Green.GetLength(1)];

            for (int i = 0; i < bufferCh.GetLength(0); i++)
                for (int j = 0; j < bufferCh.GetLength(1); j++)
                    bufferCh[i, j] = this._RGBValues.Green[i, j];

            for (int i = this._BlurRect.Y; i <= this._BlurRect.Y + this._BlurRect.Height; i++)
            {
                for (int j = this._BlurRect.X; j <= this._BlurRect.X + this._BlurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddrX;
                    int kernelAddrY;

                    for (int x = -(this._GaussianKernel.GetRadius - 1); x <= this._GaussianKernel.GetRadius - 1; x++)
                    {
                        for (int y = -(this._GaussianKernel.GetRadius - 1); y <= this._GaussianKernel.GetRadius - 1; y++)
                        {
                            kernelAddrX = this._GaussianKernel.GetRadius + x;
                            kernelAddrY = this._GaussianKernel.GetRadius + y;


                            if ((j + x >= this._BlurRect.X) && (j + x <= this._BlurRect.X + this._BlurRect.Width) &&
                                (i + y >= this._BlurRect.Y) && (i + y <= this._BlurRect.Y + this._BlurRect.Height))
                            {
                                newValue += this._RGBValues.Green[i + y, j + x] * this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                                divisor += this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                            }
                        }
                    }

                    bufferCh[i, j] = (byte)(newValue / divisor);
                    newValue = 0.0;

                    if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 255 && bufferCh[i, j] + this._GaussianKernel.GetOffset >= 0)
                        bufferCh[i, j] = (byte)(bufferCh[i, j] + this._GaussianKernel.GetOffset);
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 0)
                        bufferCh[i, j] = 0;
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset >= 255)
                        bufferCh[i, j] = 255;   
                }
            }

            this._RGBValues.Green = bufferCh;
            _FinishedChanelsCounter++;
            _ChanelDone();
            _GChanelProcessing.Abort();
        }

        protected sealed override void _ProcessBlueChanel()
        {
            byte[,] bufferCh = new byte[this._RGBValues.Blue.GetLength(0), this._RGBValues.Blue.GetLength(1)];

            for (int i = 0; i < bufferCh.GetLength(0); i++)
                for (int j = 0; j < bufferCh.GetLength(1); j++)
                    bufferCh[i, j] = this._RGBValues.Blue[i, j];

            for (int i = this._BlurRect.Y; i <= this._BlurRect.Y + this._BlurRect.Height; i++)
            {
                for (int j = this._BlurRect.X; j <= this._BlurRect.X + this._BlurRect.Width; j++)
                {
                    double newValue = 0.0;
                    double divisor = 0.0;
                    int kernelAddrX;
                    int kernelAddrY;

                    for (int x = -(this._GaussianKernel.GetRadius - 1); x <= this._GaussianKernel.GetRadius - 1; x++)
                    {
                        for (int y = -(this._GaussianKernel.GetRadius - 1); y <= this._GaussianKernel.GetRadius - 1; y++)
                        {
                            kernelAddrX = this._GaussianKernel.GetRadius + x;
                            kernelAddrY = this._GaussianKernel.GetRadius + y;


                            if ((j + x >= this._BlurRect.X) && (j + x <= this._BlurRect.X + this._BlurRect.Width) &&
                                (i + y >= this._BlurRect.Y) && (i + y <= this._BlurRect.Y + this._BlurRect.Height))
                            {
                                newValue += this._RGBValues.Blue[i + y, j + x] * this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                                divisor += this._GaussianKernel.GetKernel2D[kernelAddrY, kernelAddrX];
                            }
                        }
                    }

                    bufferCh[i, j] = (byte)(newValue / divisor);
                    newValue = 0.0;

                    if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 255 && bufferCh[i, j] + this._GaussianKernel.GetOffset >= 0)
                        bufferCh[i, j] = (byte)(bufferCh[i, j] + this._GaussianKernel.GetOffset);
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset <= 0)
                        bufferCh[i, j] = 0;
                    else if (bufferCh[i, j] + this._GaussianKernel.GetOffset >= 255)
                        bufferCh[i, j] = 255;   
                }
            }

            this._RGBValues.Blue = bufferCh;
            _FinishedChanelsCounter++;
            _ChanelDone();
            _BChanelProcessing.Abort();
        }
    }

    class GaussianBlur
    {
         
        public static string outputFileAddr; // This common variable should be used only for saving method 

        private static Stopwatch timer1 = new Stopwatch();

        [STAThread]
        static void Main(string[] Args)
        {
        Again:
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter         = "All(*.*)|*.*";
            openDlg.Title          = "Select your image to blur: ";

            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                Bitmap                 bmp;
                BitmapData             bmpData;
                ColorStructure         rgbValues;
                GaussianDistribution   gaussianDistribution;
                GaussianBlurProcessing gBlurProcessing;
                Rectangle              rect;
                Rectangle              blurRect;

                int[]  userBlurRect = new int[4];
                int    userBlurRadius;
                sbyte  highlightingCoef;
                string userRectParams;
                string inputFileAddr;

                inputFileAddr    = openDlg.FileName;
                outputFileAddr   = Path.GetDirectoryName(inputFileAddr) + @"\[Result] " + (new FileInfo(inputFileAddr).Name);
                bmp              = new Bitmap(inputFileAddr);
                userBlurRadius   = Convert.ToInt32(Interaction.InputBox("Enter your blur radius.", "Blur radius selection", "1"));
                highlightingCoef = Convert.ToSByte(Interaction.InputBox("Enter your highlighting coefficient. It's should be in [-127 ; 127] \n\nTo save the same light level, leave default value: 0.",
                    "Highlighting coefficient selection", "0"));
                userRectParams   = Interaction.InputBox("Enter your blur rectangle using spaces between values (x  y  width  height).\nTo blur all image, enter \"$gb_blurAll\". \nOr \"$gb_blurCentre\" to blur whole centre line" +
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
                        throw new Exception("Incorrect rectangle value(s). Please, restart application.");
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

                    gd  = null;
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
                        using (Stream Str = new FileStream(Path.GetDirectoryName(inputFileAddr) + @"\GaussKernel2D["+ i +"].txt", FileMode.Create))
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
                    throw new Exception("Incorrect rectangle value(s). Please, restart application.");
                #endregion

                Console.WriteLine("--------------------------------- [Summary] ---------------------------------");
                Console.WriteLine("> Chosen image: {0} ({1} x {2})", inputFileAddr, bmp.Width, bmp.Height);
                Console.WriteLine("> Image pixel format: {0}", bmp.PixelFormat);
                Console.WriteLine("> Chosen blur radius: {0}", userBlurRadius);
                Console.WriteLine("> Chosen highlighting coefficient: {0}", highlightingCoef);
                Console.WriteLine("> Chosen blur rectangle: x = {0} | y = {1} | width = {2} | height = {3}", blurRect.X, blurRect.Y, blurRect.Width + 1, blurRect.Height + 1);
                Console.WriteLine("\n> Output file name: {0}", outputFileAddr);
                Console.WriteLine("-----------------------------------------------------------------------------");
                Console.Write("> If it's correct press '1', otherwise press another key: ");

                if (Console.ReadKey().KeyChar != '1')
                {
                    Console.Clear();
                    goto Again;
                }

                gaussianDistribution = new GaussianDistribution(userBlurRadius, highlightingCoef, true);
                rect                 = new Rectangle(0, 0, bmp.Width, bmp.Height);
                bmpData              = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);                
                rgbValues            = ConvertTo2D(ref bmpData, ref bmp);
                gBlurProcessing      = new GaussianBlurProcessing2D(gaussianDistribution, rgbValues, blurRect, bmp, bmpData);
                gBlurProcessing.Done += gbp_Done;

                Console.WriteLine("\n\n> Processing image. It could take awhile...");
                
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
            Bmp.Save(outputFileAddr/*, ImageFormat.Png*/);
        }

        public static ColorStructure ConvertTo2D(ref BitmapData bmpData, ref Bitmap bmp)
        {            
            int commonOffset      = 0;            
            byte[] commonRGB      = new byte[bmpData.Stride];
            ColorStructure colors = new ColorStructure();
      
            colors.Red            = new byte[bmpData.Height, bmpData.Width];
            colors.Green          = new byte[bmpData.Height, bmpData.Width];
            colors.Blue           = new byte[bmpData.Height, bmpData.Width];
            colors.GenericPadding = new byte[bmpData.Height, Math.Abs(bmpData.Stride - (bmpData.Width * 3))];            

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
            int redOffset   = 2;
            int greenOffset = 1;
            int blueOffset  = 0;
            byte[] Res = new byte[Math.Abs(stride) * height];
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

                redOffset   += colors.GenericPadding.GetLength(1);
                greenOffset += colors.GenericPadding.GetLength(1);
                blueOffset  += colors.GenericPadding.GetLength(1);
            }

            return Res;
        }
    }
}


/*                                             IMAGE PRE-PROCESSING INFO
 *                                                
 * The default input image stream looks like:
 *  54 0 5 6 4 2 3 4 6 8 7 9 3 1 15 27 255 247 145 65 3 1 8 27 29 87 33 50 105 63 78 80 55 33 0 204 111 57 15 97 36 100 0 0 2
 * Note: number of input stream values is always proportional to 3, as we use a RGB structure.
 * 
 * As soon as technically image consists from pixels, and each pixels, in their turn, have R, G, B values, image looks like: 
 *                   _______________________________________________________
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  |-------------------------------------------------------|
 *                  | R G B | R G B | R G B | R G B | R G B | R G B | R G B |
 *                  ¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯
 *                                      _______ 
 *                               Where | R G B | is one pixel.
 *                                      ¯¯¯¯¯¯¯ 
 * 
 * But it's not all the stuff. Image also contain a "padding". Padding not define how image looks like, but it define, how it
 * storing in memory. Padding is defined as a number of bytes need to move down a row of pixels, relative to the displayed image.
 * 
 * Actually, it looks like:
 * 
 * 
 *                  | <===================== Stride ======================> |     
 *                  | <-------- Image width --------------> |
 *                   _______________________________________________________
 *  Entry point --> |X                                      |///////////////|    
 *                  |                                       |///////////////|  
 *                  |                                       |///////////////|  
 *                  |                                       |///////////////|  
 *         Memory   |                                       |///// P ///////|  
 *         Address  |                                       |///// A ///////|  
 *           |      |                                       |///// D ///////|  
 *           |      |              IMAGE                    |///// D ///////|  
 *           |      |                                       |///// I ///////|  
 *           |      |                                       |///// N ///////|  
 *           |      |                                       |///// G ///////|  
 *           |      |                                       |///////////////|  
 *           ˅      |                                       |///////////////|  
 *                  |                                       |///////////////|  
 *                  |                                       |///////////////|  
 *                  |                                       |///////////////|   
 *                  ¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯ 
 *                                X - top right corner of image.
 * 
 * More info here: https://msdn.microsoft.com/en-us/library/windows/desktop/aa473780(v=vs.85).aspx
 * 
 * Now we know that in input image stream we should have:
 *  [54 0  5   6 4 2   3 4 6   8 7 9  3 1 15]   [27 255 247  145 65 3  1 8 27  29 87 33  50 105 63]   [78 80 55  33 0 204  111 57 15  97 36 100  0 0 2]
 *   |  |  |   | | |   | | |   | | |  | | |      |   |   |    |  |  |  | | |   |  |  |   |   |  |      |  |  |   |  |  |    |  |  |   |  |   |   | | | 
 *   R  G  B   R G B   R G B   R G B  P P P      R   G   B    R  G  B  R G B   R  G  B   P   P  P      R  G  B   R  G  B    R  G  B   R  G   B   P P P
 *
 * But, according to Format24bppRgb, actually we have: 
 *  [54 0  5   6 4 2   3 4 6   8 7 9  3 1 15]   [27 255 247  145 65 3  1 8 27  29 87 33  50 105 63]   [78 80 55  33 0 204  111 57 15  97 36 100  0 0 2]
 *   |  |  |   | | |   | | |   | | |  | | |      |   |   |    |  |  |  | | |   |  |  |   |   |  |      |  |  |   |  |  |    |  |  |   |  |   |   | | | 
 *   B  G  R   B G R   B G R   B G R  P P P      B   G   R    B  G  R  B G R   B  G  R   P   P  P      B  G  R   B  G  R    B  G  R   B  G   R   P P P
 *   
 * Actual image looks like (Format24bppRgb):
 *   __________________________________________________________________
 *  [54  0    5   | 6    4  2   | 3    4  6  | 8  7  9    | 3   1    15]  
 *   ------------------------------------------------------------------
 *  [27  255  247 | 145  65 3   | 1    8  27 | 29 87 33   | 50  105  63]
 *   ------------------------------------------------------------------
 *  [78  80   55  | 33   0  204 | 111  57 15 | 97 36 100  | 0   0     2]
 *   ¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯¯
 * So all what we have to do is to create an array with H rows and W columns for every channel value (Red, Green, Blue), 
 * and an array with H rows and (S - (W * 3)) columns for padding values. Where: W - width of image, H - height of image,
 * S - stride length. And then fill the arrays with channel "personal" offset and step = 3.
 * Note: from "address" [S - (S - W * 3)] to S - contains our padding. It should be saved "as is", and should be not modified. 
 *                                                                                                                                       
 *                                             (c) 2016 Nk185.
 */
