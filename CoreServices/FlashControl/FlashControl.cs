using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using BCI_Headset;
using CoreServices.Stimulus;

namespace CoreServices.FlashControl
{
    public class FlashTiming
    {

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task<int[]> GetFlashControlMessageAsync(ISourceBlock<int[]> messageBufferBlock)
        {
            int[] messageBuffer = await messageBufferBlock.ReceiveAsync();

            return messageBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task FlashDataAsync(IHeadset headset, CancellationToken cts, List<FlashTableEntry> flashSequenceTable,
                                         ITargetBlock<int[]> messageBufferBlock, ITargetBlock<double[]> dataBufferBlock)
        {
            const int CIRCULAR_BUF_LEN = 512;       // Num sensor samples per chan in the circular buffer

            const int MAX_CHANNELS = 7;             // Assumes no TRIG channel in montage, just (sensor - ref) values
            const int MARKER_LENGTH = 13;           // Number of 8 and 16 bit markers sent in header along with sensor data

            const int MAX_MSG_LENGTH = 8;           // Max message length sent back to main thread
            const int FLASH_ON = 1;                 // Message ID
            const int FLASH_OFF = 2;
            const int END_SEQUENCE = 3;

            const int msecPerSecond = 1000;
            const int sampleRate = 300;             // 300 samples per second

            const int samplesPerBatch = 9;          // 9 samples = 30 msec @ Fs=300 Hz

            // const double PRESET_1 = 100.0;          // 0.1 second
            const double PRESET_1 = 500.0;          // 0.5 second
            const double PRESET_2 = 1000.0;         // 1.0 second
            const double PRESET_3 = 90.0;           // 90 msec = Flash ON
            const double PRESET_4 = 60.0;           // 60 msec = Flash OFF
            const double PRESET_5 = 800.0;          // 800 msec delay after last flash

            const double batchIntervalMsec = (double)((samplesPerBatch * msecPerSecond) / sampleRate);

            int timeInMsecValue;
            int channelIndex;
            int sampleIndex;

            // Number of flashes in the complete flash sequence is equal to size of the flash table
            int numStimulusGroups = flashSequenceTable.Count;

            int numFlashSeqPerTarget = 15;

            int listIndex = 0;
            int flashCycleCount = 0;

            int batchCounter = 0;           // Track # of batches received from headset over one flash interval
            int phaseInSequence = 0;

            int bufferIndex;
            int dataIndex;
            int circBufReadIndex = 0;
            int circBufWriteIndex = 0;

            int[] messageBuffer = new int[MAX_MSG_LENGTH];

            double timeElapsed;
            double signalValue;

            // Group markers in dataBuffer header that will be eventually stored as unit16 value in file
            double sourceTime = 0.0;        // Time in msec when data acquired from headset
            double stimulusTime = 0.0;      // Time in msec (wall clock)
            double blueToothCode = 0.0;     // Place holder for future diagnostic info
            double matrixPosCode = 0.0;
            double attendedTarget = 0.0;    // Key ID that user is supposed to be watching during Calibration or Validation

            // Group markers in dataBuffer header that will be eventually stored as unit8 value in file
            double bufferLevel = 0.0;       // Number of samples (per chan) left in buffer after fetching packet
            double running = 1.0;           // Marker only needed for BCI2000 processing binary data file
            double recording = 1.0;         // Marker only needed for BCI2000 processing binary data file

            double stimulusCode = 0.0;
            double stimulusBegin = 0.0;
            double stimulusType = 0.0;

            double phInSeq = 0.0;
            //double sequenceNum = 0.0;
            //double target = 0.0;
            double stimulusON = 0.0;        // Flag indicating when flash is ON during flash interval


            double[] keyIDs = new double[FlashSequence.MAX_KEYS_PER_FLASH];

            double[,] CircularBuffer = new double[MAX_CHANNELS, CIRCULAR_BUF_LEN];

            double[] dataBuffer = new double[(MAX_CHANNELS + MARKER_LENGTH + FlashSequence.MAX_KEYS_PER_FLASH) * samplesPerBatch];

            // Stopwatch stopwatch = new Stopwatch();

            /* +++++++++++++++++++++++++++++++++++++++ */

            int numberOfChannels = headset.GetNumberOfChannels();

            while (!cts.IsCancellationRequested)
            {
                // This blocking call aims to ensure that batches of samples are picked up at
                // regular intervals (timestamps should be +/- 2 msec of expected interval).
                sourceTime = headset.WaitForBatch();

                // Start separate, independent timer to detect packet receipt errors.
                // stopwatch.Start();

                batchCounter++;

                // Track time since beginning of individual flash interval (e.g. 150 msec = Flash On + Flash Off)
                timeElapsed = batchCounter * batchIntervalMsec;

                for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                {
                    IntPtr c = headset.GetChannel(channelIndex);

                    bufferIndex = circBufWriteIndex;

                    for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                    {
                        // The background thread is filling the buffers. This is where you empty them:
                        signalValue = headset.ReadBuffered(c);

                        // Store the signal value in an array for processing.
                        CircularBuffer[channelIndex, bufferIndex] = signalValue;

                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                    }
                }

                circBufWriteIndex = (circBufWriteIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                /* ++++++++++++++++++++++++++++++++++++++ */

                // NOTE:  Rather than pass a mix of floating point raw sensor data and integer marker values
                // between tasks and file storage, the marker and key index data are sent as doubles.

                switch (phaseInSequence)
                {
                    case 0:
                        // Assign default value to markers for Phase 0
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPosCode = 0.0;
                            stimulusCode = 0.0;
                            stimulusType = 0.0;
                            stimulusBegin = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_1)
                        {
                            phaseInSequence = 1;
                            batchCounter = 0;
                        }
                        break;

                    case 1:
                        // Assign default value to markers for Phase 1
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPosCode = 0.0;
                            stimulusCode = 0.0;
                            stimulusType = 0.0;
                            stimulusBegin = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_2)
                        {   // Advance to next phase with stimulus flashing
                            phaseInSequence = 2;
                            batchCounter = 0;
                        }
                        break;

                    case 2:
                        // Fetch marker values from Flash Sequence table
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            stimulusON = 1.0;

                            // Fetch marker value and convert them to double at start of each new flash
                            matrixPosCode = (double)flashSequenceTable[listIndex].matrixPosition;
                            stimulusCode = (double)flashSequenceTable[listIndex].sequenceNum;
                            stimulusType = (double)flashSequenceTable[listIndex].target;

                            // This is a marker for BCI2000 to indicate the first block of data in flash interval
                            stimulusBegin = 1.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = (double)flashSequenceTable[listIndex].keyIDgrouping[i];
                            }

                            messageBuffer[0] = FLASH_ON;
                            messageBuffer[1] = flashSequenceTable[listIndex].sequenceNum;
                            messageBuffer[2] = phaseInSequence;
                            messageBuffer[3] = (int)(sourceTime * 1000.0);

                            messageBufferBlock.Post(messageBuffer);
                        }
                        else
                        {   // Set marker to 0.0 for remainder of flash interval
                            stimulusBegin = 0.0;
                        }

                        // NOTE: Value for Preset3 (Flash ON) must be >= Preset4 (Flash OFF)
                        if (stimulusON == 1.0)
                        {
                            if (timeElapsed > PRESET_3)
                            {
                                stimulusON = 0.0;
                                stimulusCode = 0.0;
                                stimulusType = 0.0;

                                messageBuffer[0] = FLASH_OFF;
                                messageBuffer[1] = flashSequenceTable[listIndex].sequenceNum;
                                messageBuffer[2] = phaseInSequence;
                                messageBuffer[3] = (int)(sourceTime * 1000.0);

                                messageBufferBlock.Post(messageBuffer);
                            }
                        }
                        else
                        {
                            if (timeElapsed >= (PRESET_3 + PRESET_4))
                            {
                                batchCounter = 0;

                                // Need to increment index into Stimulus table to read next entry
                                if (++listIndex == numStimulusGroups)
                                {
                                    listIndex = 0;

                                    // Need to keep count of entire # times passed thru entire flash sequence list.
                                    // Value for numFlashSeqPerTarget is a key user preference configuration preset!
                                    if (++flashCycleCount == numFlashSeqPerTarget)
                                    {
                                        phaseInSequence = 3;
                                        batchCounter = 0;
                                    }
                                }
                            }
                        }
                        break;

                    case 3:
                        // Assign default value to markers for Phase 3
                        if (batchCounter == 1)
                        {
                            phInSeq = (double)phaseInSequence;
                            matrixPosCode = 0.0;
                            stimulusCode = 0.0;
                            stimulusType = 0.0;
                            stimulusBegin = 0.0;
                            stimulusON = 0.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = 0.0;
                            }
                        }

                        if (timeElapsed >= PRESET_5)
                        {
                            phaseInSequence = 0;
                            batchCounter = 0;

                            listIndex = 0;
                            flashCycleCount = 0;

                            messageBuffer[0] = END_SEQUENCE;
                            messageBufferBlock.Post(messageBuffer);
                        }
                        break;
                }  // switch(phaseInSequence)

                /* ++++++++++++++++++++++++++++++++++++++ */

                // 
                dataIndex = 0;
                bufferLevel = (double)headset.GetNumberOfBufferedSamples();

                timeInMsecValue = (int)(sourceTime * 1000.0) % 65536;
                sourceTime = (double)timeInMsecValue;

                // BCI2000 raw data format requires a separate timestamp for when the data is acquired and a wall clock 
                // time when the data is received. For now just use same time value until settle on wall clock source.
                stimulusTime = sourceTime;

                dataBuffer[dataIndex++] = sourceTime;
                dataBuffer[dataIndex++] = stimulusTime;
                dataBuffer[dataIndex++] = blueToothCode;
                dataBuffer[dataIndex++] = matrixPosCode;
                dataBuffer[dataIndex++] = attendedTarget;

                dataBuffer[dataIndex++] = bufferLevel;
                dataBuffer[dataIndex++] = running;
                dataBuffer[dataIndex++] = recording;

                dataBuffer[dataIndex++] = stimulusCode;
                dataBuffer[dataIndex++] = stimulusBegin;
                dataBuffer[dataIndex++] = stimulusType;

                dataBuffer[dataIndex++] = phInSeq;
                dataBuffer[dataIndex++] = numStimulusGroups;

                for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                {
                    dataBuffer[dataIndex++] = keyIDs[i];
                }

                bufferIndex = circBufReadIndex;
                for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                {
                    for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                    {
                        signalValue = CircularBuffer[channelIndex, bufferIndex];
                        dataBuffer[dataIndex++] = signalValue;
                    }

                    bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                }

                dataBufferBlock.Post(dataBuffer);

                circBufReadIndex = (circBufReadIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                /* ++++++++++++++++++++++++++++++++++++++ */

                /*
                // stopwatch.Stop();
                // int stopwatchElasped = stopwatch.Elapsed.Milliseconds;
                */

                /* ++++++++++++++++++++++++++++++++++++++ */

            }  // while (!cts.IsCancellationRequested)

        }  // FlashControlAsync()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
