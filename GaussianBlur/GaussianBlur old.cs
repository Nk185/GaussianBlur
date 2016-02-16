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
    class GaussianDistribution
    {
        private readonly int blurRadius;
        private readonly bool useTwoDemensions;
        private double Div = 0.0;
        private double[] Kernel_SingleDemention;
        private double[,] Kernel_TwoDemention;

        private const int SigmaMultiply = 6; // Minimal value should be 3

        public double[] GetKernel { get { return Kernel_SingleDemention; } }
        public double[,] GetKernel2D { get { return Kernel_TwoDemention; } }
        public double GetDiv { get { return Div; } }
        public double GetOffset { get { return 0.0; } }
        public int GetRadius { get { return blurRadius * SigmaMultiply; } }

        public GaussianDistribution(int blurRadius, bool useTwoDemensions)
        {
            this.blurRadius = blurRadius;
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

    class GaussianBlurProcessing
    {
        private readonly GaussianDistribution _GaussianDistributedKernel;
        private readonly ColorStructure       _ImageRGBColorValues;
        private readonly Action               _ProcessFinished;
        private Thread                        _DataProcessingThreade;

        public GaussianBlurProcessing(GaussianDistribution gaussianKernel, ref ColorStructure rgbValues, Action onFinish)
        {
            this._GaussianDistributedKernel = gaussianKernel;
            this._ImageRGBColorValues       = rgbValues;
            this._ProcessFinished           = onFinish;
        }

        public void StartProcessing()
        {

        }

    }

    struct ColorStructure
    {
        public byte[,] Red;
        public byte[,] Green;
        public byte[,] Blue;
        public byte[,] GenericPadding;
        public byte[] ImageStreameItself;
    }

    class GaussianBlur
    {
        [STAThread]
        static void Main(string[] Args)
        {
        Again:
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "JPEG (*.jpg)|*.jpg";
            openDlg.Title = "Select your image: ";

            if (openDlg.ShowDialog() == DialogResult.OK)
            {
                int[] userRect = new int[4];
                int userRad = Convert.ToInt32(Interaction.InputBox("Enter your blur radius:", "Blur radius selection", "1"));
                long operationsCount = 0;
                string userRectMsg = Interaction.InputBox("Enter your blur rectangle using spaces between values (x  y  width  height).\nTo blur all image, write \"$gb_blurAll\".",
                    "Blur rectangle selection", "$gb_blurAll");
                string inputFile = openDlg.FileName;
                string outputFilepath = Path.GetDirectoryName(inputFile) + @"\[Result]" + (new FileInfo(inputFile).Name);
                Bitmap bmp = new Bitmap(inputFile);
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                Rectangle blurRect;

                if (userRectMsg != "$gb_blurAll")
                {
                    int i = 0;
                    foreach (string value in userRectMsg.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        userRect[i] = Convert.ToInt32(value);
                        i++;
                    }

                    if ((userRect[0] + userRect[2] <= bmp.Width) && (userRect[1] + userRect[3] <= bmp.Width) && (userRect[0] >= 0) & (userRect[1] >= 0))
                        blurRect = new Rectangle(userRect[0], userRect[1], userRect[2] - 1, userRect[3] - 1);
                    else throw new Exception("Uncorrect rectangle value(s). Please, restart application.");
                }
                else
                {
                    blurRect = new Rectangle(0, 0, bmp.Width - 1, bmp.Height - 1);
                }

                Console.WriteLine("---------------------------- [Summary] ----------------------------");
                Console.WriteLine(">Choosed image: {0} ({1} x {2})", inputFile, bmp.Width, bmp.Height);
                Console.WriteLine(">Choosed blur radius: {0}", userRad);
                Console.WriteLine(">Choosed blur rectangle: x = {0} | y = {1} | width = {2} | height = {3}", blurRect.X, blurRect.Y, blurRect.Width + 1, blurRect.Height + 1);
                Console.WriteLine("\n>Output file name: {0}", outputFilepath);
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine(">If it's correct press '1', otherwise press another key.");

                if (Convert.ToInt32(Console.ReadLine()) == 2)
                {
                    Console.Clear();
                    goto Again;
                }

                Console.WriteLine(">Processing image. It could take awhile... \n");

                Stopwatch StopWatch = Stopwatch.StartNew();
                GaussianDistribution GD = new GaussianDistribution(userRad, false);
                BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
                ColorStructure rgbValues2D = ConvertTo2D(ref bmpData, ref bmp);

                #region RenderImageWithBlur
                ProcessRedH(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[16 %]");
                ProcessGreenH(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[32 %]");
                ProcessBlueH(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[47 %]");

                ProcessRedV(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[63 %]");
                ProcessGreenV(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[79 %]");
                ProcessBlueV(ref rgbValues2D, ref GD, blurRect, ref operationsCount);
                Console.WriteLine(">DONE................[100 %]\n");
                #endregion


                Marshal.Copy(ConvertToSingle(rgbValues2D, bmp.Height, bmp.Width, bmpData.Stride), 0, bmpData.Scan0, Math.Abs(bmpData.Height * bmpData.Stride));
                bmp.UnlockBits(bmpData);
                bmp.Save(outputFilepath);
                StopWatch.Stop();

                #region MyRegion
                /*double[] GDK = GD.GetKernel;

              foreach (double koef in GDK)
                  Console.WriteLine(koef);

              using (Stream Str = new FileStream(@"D:\1.txt", FileMode.Create))
              {
                  using (StreamWriter  StrWriter = new StreamWriter(Str))
                  {
                      foreach (double koef in GDK)
                          StrWriter.WriteLine(koef);
                  }
              }*/
                #endregion

                Console.WriteLine(">Elapsed time: {0} for {1} operations.", StopWatch.Elapsed, operationsCount.ToString("0 000 000 000 000 000 000"));
                Console.ReadKey();
            }
        }

        // [B G R] instead of [R G B]
        public static void ProcessRedH(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Red[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                    }

                    rgbValues.Red[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        public static void ProcessRedV(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Red[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                    }

                    rgbValues.Red[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        public static void ProcessGreenH(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Green[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                    }

                    rgbValues.Green[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        public static void ProcessGreenV(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Green[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                        
                    }

                    rgbValues.Green[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion
        }
        public static void ProcessBlueH(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Blue[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                    }

                    rgbValues.Blue[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion*/
        }
        public static void ProcessBlueV(ref ColorStructure rgbValues, ref GaussianDistribution gd, Rectangle blurRect, ref long numOfOperations)
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
                            numOfOperations++;
                        }
                        else
                        {
                            newValue += rgbValues.Blue[i, j] * gd.GetKernel[kernelAddr];
                            divisor += gd.GetKernel[kernelAddr];
                            numOfOperations++;
                        }
                    }

                    rgbValues.Blue[i, j] = Convert.ToByte(newValue / divisor + gd.GetOffset);
                    newValue = 0.0;
                }
            }
            #endregion
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