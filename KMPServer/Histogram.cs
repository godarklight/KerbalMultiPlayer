using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;

namespace KMPServer
{
    public class ServerProfiler : IDisposable
    {
        //Don't touch my private parts

        private bool initialized = false;
        private const string HISTOGRAM_DIR = "histogram";
        //Time in ms to flush data to disk
        private const int DISK_FLUSH = 5000;
        private string histogramStartTime;
        private string histogramFullPath;
        //Keep an incoming queue so we don't modify dictionaries during write
        private Queue<incomingDataEntry> incomingQueue;
        //Each message type has a dictionary containing the data
        private Dictionary<int, SortedDictionary<int, int>> histogramData;
        //Disk thread so we don't block the add data call
        private Thread diskThread;

        //Object constructor
        public bool Init()
        {
            try
            {

                #region Setting up paths
                histogramStartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                histogramFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HISTOGRAM_DIR, histogramStartTime);
                if (!recursivelyCreateDirectory(histogramFullPath))
                {
                    return false;
                }

                incomingQueue = new Queue<incomingDataEntry>();
                histogramData = new Dictionary<int, SortedDictionary<int, int>>();

                diskThread = new Thread(new ThreadStart(DiskMain));
                initialized = true;
                diskThread.Start();
                #endregion
            }
            catch (Exception e)
            {
                Log.Debug("Error: " + e);
            }
            return false;
        }

        //I suppose I could have just used the current working directory, but whatever, this is safer. And recursion.
        private bool recursivelyCreateDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    string parent = Directory.GetParent(dir).FullName;
                    if (!recursivelyCreateDirectory(parent))
                    {
                        return false;
                    }
                    else
                    {
                        Directory.CreateDirectory(dir);
                        return true;
                    }

                }
                return true;
            }
            catch (Exception e)
            {
                Log.Debug("Error: " + e);
            }
            return false;
        }

        #region This is the code I call externally.
        //It should be quick like the fox. Not like the brown lazy dog.
        public void AddData(int messageType, double startTime, double endTime)
        {
            if (!initialized)
            {
                return;
            }
            //Convert 100ns 'ticks' to ms.
            int timeDelta = (int)Math.Round(endTime - startTime, 0);
            incomingDataEntry incomingMessage = new incomingDataEntry();
            incomingMessage.messageType = messageType;
            incomingMessage.messageHandleTime = timeDelta;
            incomingQueue.Enqueue(incomingMessage);
        }
        #endregion

        public void DiskMain()
        {
            //Flush data to disk every 500ms.
            while (initialized)
            {
                Thread.Sleep(DISK_FLUSH);
                //Add all new data to the dictionaries
                while (incomingQueue.Count > 0)
                {
                    incomingDataEntry incomingDataEntry = incomingQueue.Dequeue();

                    if (histogramData.ContainsKey(incomingDataEntry.messageType))
                    {
                        SortedDictionary<int, int> existingMessageTypeDictionary = histogramData[incomingDataEntry.messageType];
                        if (existingMessageTypeDictionary.ContainsKey(incomingDataEntry.messageHandleTime))
                        {
                            existingMessageTypeDictionary[incomingDataEntry.messageHandleTime]++;
                        }
                        else
                        {
                            existingMessageTypeDictionary[incomingDataEntry.messageHandleTime] = 1;
                        }
                    }
                    else
                    {
                        SortedDictionary<int, int> newMessageTypeDictionary = new SortedDictionary<int, int>();
                        newMessageTypeDictionary.Add(incomingDataEntry.messageHandleTime, 1);
                        histogramData.Add(incomingDataEntry.messageType, newMessageTypeDictionary);
                    }
                }

                //Flush the write data to disk
                foreach (int messageType in histogramData.Keys)
                {
                    string newFileData = "";
                    foreach(KeyValuePair<int, int> existingMessageTypeDictionary in histogramData[messageType]) {
                        newFileData += existingMessageTypeDictionary.Key + " " + existingMessageTypeDictionary.Value + "\n";
                    }
                    File.WriteAllText(Path.Combine(histogramFullPath, ((KMPCommon.ClientMessageID)messageType).ToString() + ".txt"), newFileData);
                }
            }
        }

        public void Dispose()
        {
            initialized = false;
            diskThread.Join();
        }
    }

    public struct incomingDataEntry
    {
        public int messageType;
        public int messageHandleTime;
    }
}

