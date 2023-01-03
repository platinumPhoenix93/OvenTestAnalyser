# OvenTestAnalyser

Takes file path of a folder containing subfolders that have OvenCyclingData xml file and processed_ovenreadings_full csv file. Enter the filepath into the console window
when prompted.
Current known issues:
  1. Cycles occasionally are mislabelled and not being picked up by the pruning process
  2. Cycles where there is a fluctuation in temperature mid cycle will be discarded
