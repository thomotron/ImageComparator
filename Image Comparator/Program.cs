using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageComparator
{
    class MainClass
    {
        [STAThread]
        private static void Main(string[] args) // All args are test dirs
        {
            OpenFileDialog fd = new OpenFileDialog
            {
                CheckFileExists = true,
                Multiselect = true,
                Filter = "Image Files (*.png *.jpg *.jpeg *.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };

            List<string> sourceFiles = new List<string>();
            fd.ShowDialog();
            foreach (var fileName in fd.FileNames)
            {
                sourceFiles.Add(fileName);
            }

            List<string> testFiles = new List<string>();
            foreach (var arg in args)
            {
                if (Directory.Exists(arg))
                {
                    foreach (var file in Directory.GetFiles(arg, "*", SearchOption.AllDirectories))
                    {
                        testFiles.Add(file);
                    }
                }
            }

            List<string> matches = new List<string>();

            List<Task> tasks = new List<Task>();
            foreach (var sourceFile in sourceFiles)
            {
                if (!File.Exists(Path.GetFullPath(sourceFile)))
                {
                    Console.WriteLine("Invalid source path: " + sourceFile);
                }
                else
                {
                    tasks.Add(Task.Factory.StartNew(() => RunEverythingForThisFile(testFiles.ToArray(), sourceFile, matches)));
                }
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine();
            Console.WriteLine("===== Matches =====");

            foreach (var match in matches)
            {
                Console.WriteLine(match);
            }

            Console.Write("Press any key to close...");
            Console.ReadKey();
        }

        /// <summary>
        /// Runs up to four similarity tests on two images. Returns true if similarity is above threshold.
        /// </summary>
        /// <param name="source">Image to be tested against.</param>
        /// <param name="test">Image to be tested.</param>
        /// <param name="tolerance">Maximum value of colour variation between pixels before marking them as dissimilar.</param>
        /// <param name="threshold">Minimum percentage of similar pixels before returning the image as similar.</param>
        /// <returns></returns>
        public static Tuple<Tuple<bool, double>, Tuple<bool, double>, Tuple<bool, double>, Tuple<bool, double>> RunTests(Bitmap source, Bitmap test, int tolerance, double threshold)
        {
            Tuple<bool, double> okay = new Tuple<bool, double>(false, 0);
            Tuple<bool, double> smart = new Tuple<bool, double>(false, 0);
            Tuple<bool, double> expert = new Tuple<bool, double>(false, 0);

            Tuple<bool, double> dumb = Compare(16, source, test, tolerance, threshold);

            if (dumb.Item1)
            {
                okay = Compare(128, source, test, tolerance, threshold);
            }

            if (okay.Item1)
            {
                smart = Compare(512, source, test, tolerance, threshold);
            }

            if (smart.Item1)
            {
                expert = Compare(1024, source, test, tolerance, threshold);
            }

            return new Tuple<Tuple<bool, double>, Tuple<bool, double>, Tuple<bool, double>, Tuple<bool, double>>(dumb, okay, smart, expert);
        }

        /// <summary>
        /// Compares two images scaled down to <c>bitmapSize</c> for similar features. Returns true if similarity is above threshold.
        /// </summary>
        /// <param name="bitmapSize">Dimensions to test at. All comparisons are square.</param>
        /// <param name="source">Image to be tested against.</param>
        /// <param name="test">Image to be tested.</param>
        /// <param name="tolerance">Maximum value of colour variation between pixels before marking them as dissimilar.</param>
        /// <param name="threshold">Minimum percentage of similar pixels before returning the image as similar.</param>
        /// <returns>Tuple containing whether a match occurred and the percentage similarity of the images</returns>
        private static Tuple<bool, double> Compare(int bitmapSize, Bitmap source, Bitmap test, int tolerance, double threshold)
        {
            // Scale to test size
            Bitmap newSource = new Bitmap(source, bitmapSize, bitmapSize);
            Bitmap newTest = new Bitmap(test, bitmapSize, bitmapSize);

            double matchesR = 0;
            double matchesG = 0;
            double matchesB = 0;

            double totalPixels = bitmapSize * bitmapSize;

            // Compare
            for (int x = 0; x < newTest.Width && x < newSource.Width; x++) // X
            {
                for (int y = 0; y < newTest.Height && y < newSource.Height; y++) // Y
                {
                    var sPixel = newSource.GetPixel(x, y);
                    var tPixel = newTest.GetPixel(x, y);

                    if ((sPixel.R - tPixel.R) >= (0 - tolerance) && (sPixel.R - tPixel.R) <= (0 + tolerance))
                    {
                        matchesR++;
                    }

                    if ((sPixel.G - tPixel.G) >= (0 - tolerance) && (sPixel.G - tPixel.G) <= (0 + tolerance))
                    {
                        matchesG++;
                    }

                    if ((sPixel.B - tPixel.B) >= (0 - tolerance) && (sPixel.B - tPixel.B) <= (0 + tolerance))
                    {
                        matchesB++;
                    }
                }
            }

            double matchChance = (matchesR / totalPixels + matchesG / totalPixels + matchesB / totalPixels) / 3;

            newSource.Dispose();
            newTest.Dispose();

            if (matchChance >= threshold)
            {
                return new Tuple<bool, double>(true, matchChance);
            }
            return new Tuple<bool, double>(false, matchChance);
        }

        public static void RunEverythingForThisFile(string[] testFiles, string sourceFile, List<string> matches)
        {
            foreach (var file in testFiles)
            {
                if (Path.GetExtension(file).ToLower() == ".png" || Path.GetExtension(file).ToLower() == ".jpg" || Path.GetExtension(file).ToLower() == ".jpeg" || Path.GetExtension(file).ToLower() == ".bmp")
                {
                    var source = new Bitmap(sourceFile);
                    var fileStream = File.OpenRead(file);
                    if (!(fileStream.Length >= 300000000)) // No more than ~150kB
                    {
                        var test = new Bitmap(fileStream);
                        var testResults = MainClass.RunTests(source, test, 2, 0.75);



                        if (testResults.Item1.Item1 && testResults.Item2.Item1 && testResults.Item3.Item1 && testResults.Item4.Item1)
                        {
                            if (Path.GetFullPath(file) != Path.GetFullPath(sourceFile))
                            {
                                matches.Add("Source: " + Path.GetFullPath(sourceFile));
                                matches.Add("  [" + Math.Round(testResults.Item4.Item2 * 100, 2) + "%] " + Path.GetFullPath(file));
                                Console.WriteLine("[" + Math.Round(testResults.Item4.Item2 * 100, 2) + "%] " + Path.GetFullPath(file));
                            }
                        }
                        else
                        {
                            double result;
                            if (testResults.Item4.Item1)
                            {
                                result = testResults.Item4.Item2;
                            }
                            else if (testResults.Item3.Item1)
                            {
                                result = testResults.Item3.Item2;
                            }
                            else if (testResults.Item2.Item1)
                            {
                                result = testResults.Item2.Item2;
                            }
                            else
                            {
                                result = testResults.Item1.Item2;
                            }
                            Console.WriteLine("[" + Math.Round(result * 100, 2) + "%] " + Path.GetFullPath(file));
                        }
                        test.Dispose();
                    }
                    source.Dispose();
                }
            }
        }
    }
}
