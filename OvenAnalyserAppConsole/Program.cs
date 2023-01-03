
using System.Xml;
using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace OvenAnalyserAppConsole
{
    class Program
    {
        public static void Main()
        {
            Console.WriteLine("Input data filepath: ");
            string testDirectory = Console.ReadLine();

            List<OvenDataAnalyser> ovenData = new List<OvenDataAnalyser>();
            PopulateOvenData(ovenData, testDirectory);

            //If any oven test data has been found
            if(ovenData.Count > 0)
            {
                foreach (OvenDataAnalyser oven in ovenData)
                {
                    oven.CalculateAverages();
                }
            } else { Console.WriteLine("No test data found in the given directory"); }

            SaveToCsv($"{testDirectory}/testAverages.csv", ovenData);
        }

        //Checks for files in each sub-folder of the given directory that match the naming convention for the xml and csv files
        private static void PopulateOvenData(List<OvenDataAnalyser> ovenData, string testDirectory)
        {
            if (Directory.Exists(testDirectory))
            {
                string[] foldersInDirectory = Directory.GetDirectories(testDirectory);
                foreach (string folder in foldersInDirectory)
                {
                    string[] filesInDirectory = Directory.GetFiles(folder);
                    string cyclingDataFilePath = "";
                    string ovenReadingsFilePath = "";

                    foreach (string file in filesInDirectory)
                    {
                        Regex cyclingDataRx = new Regex(@"ovencyclingdata\d+\.xml", RegexOptions.IgnoreCase);
                        if (cyclingDataRx.IsMatch(file.ToLowerInvariant()))
                        {
                            cyclingDataFilePath = file;
                        }

                        Regex ovenReadingsRx = new Regex(@"processed_ovenreadings_full.csv", RegexOptions.IgnoreCase);
                        if (ovenReadingsRx.IsMatch(file.ToLowerInvariant()))
                        {
                            ovenReadingsFilePath = file;
                        }
                    }
                    if (!string.IsNullOrEmpty(ovenReadingsFilePath) && !string.IsNullOrEmpty(cyclingDataFilePath))
                    {
                        ovenData.Add(new OvenDataAnalyser(cyclingDataFilePath, ovenReadingsFilePath));
                    }

                }

            }
        }

        //Creates a CSV file and populates it with the results of tests
        private static void SaveToCsv(string filePath, List<OvenDataAnalyser> ovenData)
        {
            StreamWriter sw = new StreamWriter(filePath, false);

            sw.WriteLine("Serial Number, Test Date, Average V1_fb at 50kHz at max," +
                "Average V1_fb at 50kHz at min," +
                "Average G at 50kHz at max," +
                "Average G at 50kHz at min," +
                "Average C at 50kHz at max," +
                "Average C at 50kHz at min," +
                "Average V1_fb at 64kHz at max," +
                "Average V1_fb at 64kHz at min," +
                "Average G at 64kHz at max," +
                "Average G at 64kHz at min," +
                "Average C at 64kHz at max," +
                "Average C at 64kHz at min");

             

            foreach(OvenDataAnalyser oven in ovenData)
            {
                sw.Write(oven.serialNumber + "," + oven.testDate + ",");
                foreach(Test test in oven.tests)
                {
                    sw.Write(test.average + ",");
                }

                sw.WriteLine();
                
            }

            sw.Close();
        }

    }
}


