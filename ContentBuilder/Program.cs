using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ESuite;
using System.Diagnostics;

// How to operate:
// - make sure all raw images are in the RawContent folder
// - each image should be linked in the GuiScaleManager to a ImageGroup
// - in case a new ImageGroup needs to be made, make sure the entry is also added to the guiContainerInfos, and an entry in the method below is added!
// - the Raw image should have the upscale factor defined below. eg: image for the buttonbar should be 5x the nominal size. this means for a buttonbarheight of 60*5=300
// - then rebuild and run (rightClick->debug->start new instance)
// - find the output in bin/ContentOutput, and move this into the ESuite/Content map!

namespace ContentBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            string rawContentDir = Path.Combine(new string[] { System.IO.Directory.GetCurrentDirectory(), "RawContent" });

            //first check if all hi-res images are present
            HashSet<string> fileList = Scaler.imageNames;
            foreach (string file in fileList)
            {
                string inputFileFullPath = Path.Combine(rawContentDir, file + ".png");
                if (!File.Exists(inputFileFullPath)) throw new Exception("Input file " + file + ".png does not exist!");
            }

            //go convert!!
            foreach (string file in fileList.Where(x => x.Contains("indicator")))
            {
                string outputPath = Path.Combine(new string[] {
                    System.IO.Directory.GetCurrentDirectory(),
                    "..","Content",
                    Scaler.IMAGE_PATH
                });
                Directory.CreateDirectory(outputPath);

                string inputFileFullPath = Path.Combine(rawContentDir, file + ".png");

                Console.WriteLine("Converting " + file);
                foreach (Scaler.PixelDensity dpi in Enum.GetValues(typeof(Scaler.PixelDensity)))
                {
                    foreach (var scale in Scaler.guiScalers)
                    {
                        double rescalePercent = (double)dpi / (double)Scaler.BASE_DPI * scale.Value * 100.0 * Scaler.PRESCALE;
                        string outputFilename = Path.Combine(outputPath, Scaler.ImageFileName(file, dpi, scale.Key));
                        string convertArgs = String.Format("{0} -resize {1}% {2}",
                                                inputFileFullPath,
                                                rescalePercent,
                                                outputFilename);
                        Process p = new Process()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                //FileName = "C:\\Program Files\\ImageMagick-6.8.9-Q16\\convert.exe", 
                                FileName = "convert",
                                Arguments = convertArgs,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            }
                        };
                        p.Start();
                        p.WaitForExit();
                        Console.Write(p.StandardOutput.ReadToEnd());
                        Console.Write(p.StandardError.ReadToEnd());
                    }
                }
            }
        }

        static string TrimFilename(string fullPath)
        {
            int lastSlash = 0;
            while (fullPath.IndexOf('\\', lastSlash + 1) > 0)
                lastSlash = fullPath.IndexOf('\\', lastSlash + 1);

            return fullPath.Substring(lastSlash + 1, fullPath.Length - lastSlash - 1);
        }
    }
}
