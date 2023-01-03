using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace OvenAnalyserAppConsole
{
    public enum OvenCycleType { Max, Min};

    class OvenDataAnalyser
    {
        public string ovenName { get; private set; }
        public DataTable ovenCsvData { get; private set; }
        public string? serialNumber { get; private set; }
        public string? testDate { get; private set; }

        public  List<Test> tests { get; private set; } = new();

        private int internalTempIndex;
        private int timeCodeIndex;
        private int v1_fbIndex;
        private int gIndex;
        private int cIndex;
        private int frequencyIndex;



        public OvenDataAnalyser(string xmlFilepath, string csvFilepath) {

            ovenCsvData = FileParser.GetDataTableFromCSVFile(csvFilepath);
            ovenName = ovenCsvData.TableName;
            GetSerialAndDateFromXml(xmlFilepath);
            ovenCsvData = FileParser.GetDataTableFromCSVFile(csvFilepath);
            GetColumnIndices();
            SetUpTests();
        }

        public void CalculateAverages()
        {

            ovenCsvData.Columns.Add("CycleType");
            ovenCsvData.Columns.Add("CycleNumber", typeof(int));
            ovenCsvData.Columns.Add("RoundedInternalTemp", typeof(int));
            List<(int cycleNumber, TimeSpan cycleDuration, DateTime cycleStartTime, DateTime cycleEndTime)> cycleTimes = new List<(int, TimeSpan, DateTime, DateTime)>();

            DataRow[] foundRows = ovenCsvData.Select();

            CalculateTemperatureCycles(foundRows);
            CalculateValidCycles(cycleTimes);

            
            cycleTimes.Reverse();
   
            
            if(cycleTimes.Count > 2) {

                //Loop through each test in tests list

                for (int j = 0; j < tests.Count; j++)
                {
                    //Check for max and min cycle match for that test type

                    for(int i = 0; i < 2 ; i++)
                    {
                        DataRow[] filteredCycleRows;

                        //If the test is looking for a minimum cycle
                        if (tests[j].cycleType == OvenCycleType.Min)
                        {
                            //Filter cycle i for minimum cycle
                            filteredCycleRows = ovenCsvData.Select($"CycleNumber = {cycleTimes[i].cycleNumber} and frequency = {tests[j].frequency} and CycleType = 'Min'");
                        }
                        else
                        {
                            //Filter cycle i for maximum cycle
                            filteredCycleRows = ovenCsvData.Select($"CycleNumber = {cycleTimes[i].cycleNumber} and frequency = {tests[j].frequency} and CycleType = 'Max'");
                        }

                        double sum = 0;

                        //If there are any results for the query calculate the average and assign the current test the result for that average
                        if (filteredCycleRows.Length > 0)
                        {
                            foreach (DataRow dataRow in filteredCycleRows)
                            {
                                sum += Convert.ToDouble(dataRow.Field<string>(tests[j].testColumnIndex));
                            }

                            double average = sum / filteredCycleRows.Length;

                            Console.WriteLine($"Average for {tests[j].testName} at {tests[j].frequency} {average}");

                            tests[j].average = average;
                        }
                    }
                }
            }

            //FileParser.ToCSV(ovenCsvData, $"{serialNumber} Dump.csv");
        }


        //Gets the serial number and start date from the given xml filepath
        private void GetSerialAndDateFromXml(string xmlFilepath)
        {

            XmlDocument doc = new XmlDocument();

            try
            {
                doc.Load(xmlFilepath);

                //Get text of serial number and timestamp
                serialNumber = doc.DocumentElement.SelectSingleNode("/OvenCyclingData/SerialNumber").InnerText;
                string testTime = doc.DocumentElement.SelectSingleNode("/OvenCyclingData/CompletedTimeStamp").InnerText;
                testDate = Convert.ToDateTime(testTime).Date.ToString("yyyy/MM/dd");


            }
            catch (Exception e)
            {
                if(e is NullReferenceException)
                {
                    Console.WriteLine("File not found");
                }
                else if(e is XmlException)
                {
                    Console.WriteLine("Malformed xml");
                }

                
            }
        }

        //Finds column indices for used columns
        private void GetColumnIndices()
        {
            DataColumnCollection columns = ovenCsvData.Columns;

            if (columns.Contains("internal_temperature"))
            {
                internalTempIndex = columns.IndexOf("internal_temperature");
            }
            else { internalTempIndex = 0; }

            if (columns.Contains("timecode"))
            {
                timeCodeIndex = columns.IndexOf("timecode");
            }
            else { timeCodeIndex = 0; }

            if (columns.Contains("V1_fb"))
            {
                v1_fbIndex = columns.IndexOf("V1_fb");
            }
            else
            {
                v1_fbIndex = 0;
            }
            if (columns.Contains("G"))
            {
                gIndex = columns.IndexOf("G");
            }
            else
            {
                gIndex = 0;
            }
            if (columns.Contains("C"))
            {
                cIndex = columns.IndexOf("C");
            }
            else
            {
                cIndex = 0;
            }
            if (columns.Contains("frequency"))
            {
                frequencyIndex = columns.IndexOf("frequency");
            }
        }

        //Identifies high and low temperatures from internal temperature row, and calculates the cycles
        private void CalculateTemperatureCycles(DataRow[] foundRows)
        {
            int cycles = 0;

            //Calculate rounded value of each temp to nearest degree
            for (int i = foundRows.Length - 1; i >= 0; i--)
            {
                foundRows[i]["RoundedInternalTemp"] = Math.Round(Convert.ToDouble(foundRows[i].Field<string>(internalTempIndex)), 0);
            }

            //Create an array of each unique temperature
            int[] uniqueTemperatures = foundRows.AsEnumerable().Select(r => r.Field<int>("RoundedInternalTemp")).Distinct().ToArray();
            Array.Sort(uniqueTemperatures);

            Dictionary<int, int> temperatureCounts = new();

            foreach (int temperature in uniqueTemperatures)
            {
                temperatureCounts.Add(temperature, 0);
            }

            //Calculate how many times each unique temperature occurs
            foreach (DataRow row in foundRows)
            {
                int rowTemperature = row.Field<int>("RoundedInternalTemp");

                if (temperatureCounts.ContainsKey(rowTemperature))
                {
                    temperatureCounts[rowTemperature] = temperatureCounts[rowTemperature] + 1;
                }
            }

            //Sort the temperatures by their frequency
            var sortedTempFrequencyDict = from entry in temperatureCounts orderby entry.Value descending select entry;

            /*  Get the first two elements of the temperature frequency list and set the larger temperature as the
                maximum temperature for that cycle, and lower as the minimum temperature for that cycle */
            (int max, int min) cycleTemps = (Math.Max(sortedTempFrequencyDict.ElementAt(0).Key, sortedTempFrequencyDict.ElementAt(1).Key), Math.Min(sortedTempFrequencyDict.ElementAt(0).Key, sortedTempFrequencyDict.ElementAt(1).Key));


            /*Cycle through each rounded temperature, and if it is within 1 degree of the min/max cycle temperature assign it to high or low cycle type 
              If it is not within this range then set it to 'changing temperature' to indicate it is a transitionary period between high and low */

            for (int i = 0; i < foundRows.Length; i++)
            {
                //If temp is within 1 degree of max cycle temp
                if (Math.Abs(foundRows[i].Field<int>("RoundedInternalTemp") - cycleTemps.max) <= 1)
                {
                    foundRows[i]["CycleType"] = "Max";

                    //If previous row was not of the same cycle then identify this as the start of a new cycle
                    if (i > 0 && foundRows[i - 1]["CycleType"] != "Max")
                    {
                        foundRows[i]["CycleNumber"] = cycles++;
                    }
                    else
                    {
                        foundRows[i]["CycleNumber"] = cycles;
                    }
                }
                //If temp is within 1 degree of min cycle temp
                else if (Math.Abs(foundRows[i].Field<int>("RoundedInternalTemp") - cycleTemps.min) <= 1)
                {
                    //If previous row was not of the same cycle then identify this as the start of a new cycle
                    foundRows[i]["CycleType"] = "Min";
                    if (i > 0 && foundRows[i - 1]["CycleType"] != "Min")
                    {
                        foundRows[i]["CycleNumber"] = cycles++;
                    }
                    else
                    {
                        foundRows[i]["CycleNumber"] = cycles;
                    }
                }
                //If not max or min cycle temp then mark as transitionary temperature
                else { foundRows[i]["CycleType"] = "Changing temperature"; }
            }

        }

        private void CalculateValidCycles(List<(int cycleNumber, TimeSpan cycleDuration, DateTime cycleStartTime, DateTime cycleEndTime)> cycleTimes)
        {
            int numCycles = Convert.ToInt32(ovenCsvData.Compute("max([CycleNumber])", string.Empty));

            //Loop through each cycle and add the cycle number, cycle duration, cycle start time and cycle end time to the tuple list cycletimes
            for (int i = 0; i < numCycles; i++)
            {
                DataRow[] currentCycleRows = ovenCsvData.Select($"CycleNumber = {i}");
                DateTime startTime = Convert.ToDateTime(currentCycleRows[0]["timecode"]);
                DateTime endTime = Convert.ToDateTime(currentCycleRows[currentCycleRows.Length - 1]["timecode"]);
                cycleTimes.Add((i, endTime - startTime, startTime, endTime));
            }


            List<TimeSpan> roundedCycleDurations = new List<TimeSpan>();

            //Add each cycle duration rounded to the nearest hour to a new list of cycle durations
            foreach ((int cycleNumber, TimeSpan cycleDuration, DateTime cycleStartTime, DateTime cycleEndTime) in cycleTimes)
            {
                roundedCycleDurations.Add(RoundTimeToHours(cycleDuration));
            }

            //Calculate the modal cycle duration from the rounded cycle durations
            TimeSpan mode = GetMode(roundedCycleDurations);

            /*Loop through each identified cycle and remove it if its rounded cycle duration does not match the modal duration
              this prunes any mis-identified cycles (due to reading fluctuations etc), as the cycles are supposed to be a fixed length*/
            for (int i = 0; i < cycleTimes.Count; i++)
            {
                if (RoundTimeToHours(cycleTimes[i].cycleDuration) != mode)
                {
                    Console.WriteLine($"Removing {cycleTimes[i].cycleNumber} from {serialNumber}");
                    cycleTimes.Remove(cycleTimes[i]);
                }
            }
        }

        //Adds a set of predefined tests to the test list.
        private void SetUpTests()
        {
            tests.Add(new("Average V1_fb", 50, OvenCycleType.Max, v1_fbIndex));
            tests.Add(new("Average V1_fb", 50, OvenCycleType.Min, v1_fbIndex));
            tests.Add(new("Average G", 50, OvenCycleType.Max, gIndex));
            tests.Add(new("Average G", 50, OvenCycleType.Min, gIndex));
            tests.Add(new("Average C", 50, OvenCycleType.Max, cIndex));
            tests.Add(new("Average C", 50, OvenCycleType.Min, cIndex));
            tests.Add(new("Average V1_fb", 64, OvenCycleType.Max, v1_fbIndex));
            tests.Add(new("Average V1_fb", 64, OvenCycleType.Min, v1_fbIndex));
            tests.Add(new("Average G", 64, OvenCycleType.Max, gIndex));
            tests.Add(new("Average G", 64, OvenCycleType.Min, gIndex));
            tests.Add(new("Average C", 64, OvenCycleType.Max, cIndex));
            tests.Add(new("Average C", 64, OvenCycleType.Min, cIndex));
        }

        //Allows for the adding of additional tests. Takes a tuple, testName is the name along with frequency and cycle type that will be output in the CSV to identify the test
        //Frequency is the frequency to sample from, cycletype indicates whether we are targetting a max or minimum temperature for the cycle, testVariableIndex is the table index the variable to test is found at
        //Average is the result for the test, should generally be given 0 initially.
        public void AddTest(Test newTest)
        {
            tests.Add(newTest);
        }

        private TimeSpan RoundTimeToHours(TimeSpan timeSpan)
        {
            return TimeSpan.FromHours(Math.Round(timeSpan.TotalHours));
        }


        private TimeSpan GetMode(List<TimeSpan> items)
        {
            var mode = items.GroupBy(n => n).
                OrderByDescending(g => g.Count()).
                Select(g => g.Key).FirstOrDefault();

            return mode;
        }
    }
}
