using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OvenAnalyserAppConsole
{
    //Holds data for individual tests
    internal class Test
    {

        public string testName { get; set; }
        public int frequency { get; set; }
        public OvenCycleType cycleType { get; set; }
        public int testColumnIndex { get; set; }
        public double average { get; set; }

        public Test(string testName, int frequency, OvenCycleType cycleType, int testColumnIndex) {
        
            this.testName = testName;
            this.frequency = frequency;
            this.cycleType = cycleType;
            this.testColumnIndex = testColumnIndex;
            average = 0.0;
        
        }

    }
}
