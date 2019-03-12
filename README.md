# TM2Stat

Work in progress

An simple program that tracks training statistics as one is training maps for some tournament in Trackmania 2 Stadium.
Created as an attempt to train as efficient as possible, as training for TM2 tournaments is typically time consuming with the release of new maps every now and then (often one week).
As of now, metrics consist of the following rudimental set: 

mean; the average time from the session on a particular map
standard deviation; the average deviation from the mean (lower values effectively indicate good consistency, although the mean might be slow)
number of crashes; self explanatory
finish to crash (C/F) ratio; simple indicator of how well you are doing, consistency wise
effective playtime; the actual time spent driving

Finish times are also tracked and are exported to CSV together with the aforementioned metrics.

The program accomplishes this by reading the ManiaPlanet's process memory, in particulary the variable that holds the timer. As a result, this program is likely to break with older or newer versions that the game's current version. 
