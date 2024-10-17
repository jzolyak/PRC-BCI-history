using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

        public async Task<int[]> GetFlashControlMessageAsync(ISourceBlock<int[]> flashMsgBufferBlock)
        {
            int[] messageBuffer = await flashMsgBufferBlock.ReceiveAsync();

            return messageBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task FlashDataAsync(IHeadset headset, CancellationToken cts, List<FlashTableEntry> flashSequenceTable,
                                         ISourceBlock<double[]> headsetDataBufferBlock, ITargetBlock<double[]> flashDataBufferBlock,
                                         ITargetBlock<int[]> flashMsgBufferBlock)
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
            // const double PRESET_1 = 500.0;          // 0.5 second
            // const double PRESET_1 = 1000.0;         // 1.0 second
            const double PRESET_1 = 3000.0;         // 3.0 second, increase by 2 sec to give user more time to locate target

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

            int numFlashSeqPerTarget = 1;
            //int numFlashSeqPerTarget = 4;

            int listIndex = 0;
            int flashCycleCount = 0;
            int shuffledIndex = 0;

            int batchCounter = 0;           // Track # of batches received from headset over one flash interval
            int phaseInSequence = 0;

            int blockSize;
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

            double[] headsetDataBuffer = null;

            double[] keyIDs = new double[FlashSequence.MAX_KEYS_PER_FLASH];

            double[,] CircularBuffer = new double[MAX_CHANNELS, CIRCULAR_BUF_LEN];

            // Buffer is sized to hold one sensor data packet. Packet size = one header per batch of sample data.
            double[] dataBuffer = new double[(MARKER_LENGTH + FlashSequence.MAX_KEYS_PER_FLASH) + (MAX_CHANNELS * samplesPerBatch)];

            // Stopwatch stopwatch = new Stopwatch();

            FlashOrder flashOrder = new FlashOrder();

            int[] flashOrderTable = flashOrder.InitializeTableOrdering(flashSequenceTable);

            //flashOrder.ShuffleTableOrdering(ref flashOrderTable);


            /* +++++++++++++++++++++++++++++++++++++++ */

            int numberOfChannels = headset.GetNumberOfChannels();

            while (!cts.IsCancellationRequested)
            {
                // This blocking call aims to ensure that batches of samples are picked up at
                // regular intervals (timestamps should be +/- 2 msec of expected interval).
                // sourceTime = headset.WaitForBatch();

                if (await headsetDataBufferBlock.OutputAvailableAsync(cts))
                {
                    try
                    {
                        headsetDataBuffer = await headsetDataBufferBlock.ReceiveAsync(cts);
                        blockSize = headsetDataBuffer.Length;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(this, ex.Message);
                        continue;
                    }
                    //break;
                }
                else
                {
                    continue;
                }

                // Start separate, independent timer to detect packet receipt errors.
                // stopwatch.Start();

                batchCounter++;

                // Track time since beginning of each phase in sequnce, or individual
                // flash interval (e.g. 150 msec = Flash On + Flash Off)
                timeElapsed = batchCounter * batchIntervalMsec;

                dataIndex = 0;
                bufferIndex = circBufWriteIndex;

                for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                {
                    for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                    {
                        // The background thread is filling the buffers. This is where you empty them:
                        signalValue = headsetDataBuffer[dataIndex++];

                        // Store the signal value in an array for processing.
                        CircularBuffer[channelIndex, bufferIndex] = signalValue;
                    }

                    bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
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

                            // Use shuffled index to access flashSequenceTable[] to randomize flash order for each flash sequence.
                            shuffledIndex = flashOrderTable[listIndex];

                            // Fetch marker value and convert them to double at start of each new flash
                            matrixPosCode = (double)flashSequenceTable[shuffledIndex].matrixPosition;
                            stimulusCode = (double)flashSequenceTable[shuffledIndex].sequenceNum;
                            stimulusType = (double)flashSequenceTable[shuffledIndex].target;

                            // This is a marker for BCI2000 to indicate the first block of data in flash interval
                            stimulusBegin = 1.0;

                            for (int i = 0; i < FlashSequence.MAX_KEYS_PER_FLASH; i++)
                            {
                                keyIDs[i] = (double)flashSequenceTable[shuffledIndex].keyIDgrouping[i];
                            }

                            messageBuffer[0] = FLASH_ON;
                            messageBuffer[1] = flashSequenceTable[shuffledIndex].sequenceNum;
                            messageBuffer[2] = phaseInSequence;
                            messageBuffer[3] = (int)(sourceTime * 1000.0);

                            flashMsgBufferBlock.Post(messageBuffer);
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

                                shuffledIndex = flashOrderTable[listIndex];

                                messageBuffer[0] = FLASH_OFF;
                                messageBuffer[1] = flashSequenceTable[shuffledIndex].sequenceNum;
                                messageBuffer[2] = phaseInSequence;
                                messageBuffer[3] = (int)(sourceTime * 1000.0);

                                flashMsgBufferBlock.Post(messageBuffer);
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

                                    // Shuffle the order in which the stim codes are flashed for each flash sequence.
                                    // The key grouping remain the same, just the relative order gets shuffled around.
                                    flashOrder.ShuffleTableOrdering3(ref flashOrderTable);

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
                            flashMsgBufferBlock.Post(messageBuffer);
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

                circBufReadIndex = (circBufReadIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                flashDataBufferBlock.Post(dataBuffer);

                /* ++++++++++++++++++++++++++++++++++++++ */

                /*
                // stopwatch.Stop();
                // int stopwatchElasped = stopwatch.Elapsed.Milliseconds;
                */

                /* ++++++++++++++++++++++++++++++++++++++ */

            }  // while (!cts.IsCancellationRequested)

        }  // FlashControlAsync()

    }

    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    public class FlashOrder
    {
        public int maxtrix1_Size;
        public int maxtrix2_Size;
        public int row_col_size;
        public int numStimGroups;

        public int num_rows;
        public int num_cols;

        public int[] rowIndexes;
        public int[] colIndexes;

        Random rnd = new Random();


        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int[] InitializeTableOrdering(List<FlashTableEntry> flashSequenceTable)
        {
            numStimGroups = flashSequenceTable.Count;

            num_rows = numStimGroups / 2;
            num_cols = numStimGroups / 2;

            rowIndexes = new int[num_rows];
            colIndexes = new int[num_cols];

            for (int i = 0; i < num_cols; i++)
            {
                rowIndexes[i] = i;
                colIndexes[i] = num_cols + i;
            }

            RandomizeIndexes(ref rowIndexes);
            RandomizeIndexes(ref colIndexes);

            int[] flashOrderTable = new int[numStimGroups];

            // Test if HiddenMatrix1 and HiddenMatrix2 have the same square (row*col) matrix size.
            if ( (numStimGroups % 4) == 0)
            {
                maxtrix1_Size = numStimGroups / 4;
                maxtrix2_Size = numStimGroups / 4;
            }
            else
            {
                // numStimGroups = maxtrix1_Size_Row + maxtrix1_Size_Col + maxtrix2_Size_Row + maxtrix2_Size_Col
                // numStimGroups = (row_col_size + 1) + (row_col_size + 1) + (row_col_size) + (row_col_size)
                // numStimGroups = (4 * row_col_size) + 2

                row_col_size = (numStimGroups - 2) / 4;

                // maxtrix1_Size is always greater than or equal to maxtrix2_Size. (See FlashGrouping)
                maxtrix2_Size = row_col_size;
                maxtrix1_Size = row_col_size + 1;
            }

            for (int i = 0; i < numStimGroups; i++)
            {
                flashOrderTable[i] = flashSequenceTable[i].sequenceNum - 1;
            }

            return flashOrderTable;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void RandomizeIndexes(ref int[] tableOfIndexes)
        {
            int randomValue;
            int indexValue;

            int numIndexes = tableOfIndexes.Length;

            // Use Fisher-Yates algorithm to randomize the elements of an array.
            for (int n = numIndexes - 1; n > 0; --n)
            {
                // Randomly pick an item which has not been shuffled
                randomValue = rnd.Next(n + 1);

                // Swap the selected item woth the last unstruck inumber in the table
                indexValue = tableOfIndexes[n];
                tableOfIndexes[n] = tableOfIndexes[randomValue];
                tableOfIndexes[randomValue] = indexValue;
            }
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ShuffleTableOrdering(ref int[] flashOrderTable)
        {
            int prevStimCode;
            int newStimCode;
            int randomValue1;
            int randomValue2;
            int lowerLimit;
            int upperLimit;

            randomValue1 = rnd.Next(1, maxtrix1_Size);

            lowerLimit = 0;
            upperLimit = maxtrix1_Size;

            // Hidden Matrix 1 - row stim code assignment
            for (int i = lowerLimit; i < upperLimit; i++)
            {
                prevStimCode = flashOrderTable[i];
                newStimCode = (prevStimCode + randomValue1);

                if (newStimCode >= upperLimit)
                {
                    newStimCode = newStimCode - maxtrix1_Size;
                }

                flashOrderTable[i] = newStimCode;
            }

            randomValue2 = rnd.Next(1, maxtrix2_Size);

            lowerLimit = maxtrix1_Size;
            upperLimit = maxtrix1_Size + maxtrix2_Size;

            // Hidden Matrix 2 - row stim code assignment
            for (int i = lowerLimit; i < upperLimit; i++)
            {
                prevStimCode = flashOrderTable[i];
                newStimCode = (prevStimCode + randomValue2);

                if (newStimCode >= upperLimit)
                {
                    newStimCode = newStimCode - maxtrix2_Size;
                }

                flashOrderTable[i] = newStimCode;
            }

            lowerLimit = maxtrix1_Size + maxtrix2_Size;
            upperLimit = maxtrix1_Size + maxtrix2_Size + maxtrix1_Size;

            // Hidden Matrix 1 - col stim code assignment
            for (int i = lowerLimit; i < upperLimit; i++)
            {
                prevStimCode = flashOrderTable[i];
                newStimCode = (prevStimCode + randomValue1);

                if (newStimCode >= upperLimit)
                {
                    newStimCode = newStimCode - maxtrix1_Size;
                }

                flashOrderTable[i] = newStimCode;
            }

            lowerLimit = maxtrix1_Size + maxtrix2_Size + maxtrix1_Size;
            upperLimit = maxtrix1_Size + maxtrix2_Size + maxtrix1_Size + maxtrix2_Size;

            // Hidden Matrix 2 - col stim code assignment
            for (int i = lowerLimit; i < upperLimit; i++)
            {
                prevStimCode = flashOrderTable[i];
                newStimCode = (prevStimCode + randomValue1);

                if (newStimCode >= upperLimit)
                {
                    newStimCode = newStimCode - maxtrix2_Size;
                }

                flashOrderTable[i] = newStimCode;
            }

        }  // ShuffleTableOrdering()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ShuffleTableOrdering2(ref int[] flashOrderTable)
        {

            int randomValue1;
            int randomValue2;
            int randomValue3;
            int randomValue4;
            int stimCode1;
            int stimCode2;

            int maxtrix1_rows = 0;
            int maxtrix2_rows = maxtrix1_Size;
            int maxtrix1_cols = maxtrix1_Size + maxtrix2_Size;
            int maxtrix2_cols = maxtrix1_Size + maxtrix2_Size + maxtrix1_Size;

            // Hidden Matrix 1 - row stim code assignment
            for (int i = 0; i < maxtrix2_Size; i++)
            {
                randomValue1 = rnd.Next(0, maxtrix1_Size);
                randomValue2 = rnd.Next(0, maxtrix1_Size);
                randomValue3 = rnd.Next(0, maxtrix2_Size);
                randomValue4 = rnd.Next(0, maxtrix2_Size);

                // Hidden Matrix 1 - swap stim code row assignment
                stimCode1 = flashOrderTable[maxtrix1_rows + randomValue1];
                stimCode2 = flashOrderTable[maxtrix1_rows + randomValue2];
                flashOrderTable[maxtrix1_rows + randomValue1] = stimCode2;
                flashOrderTable[maxtrix1_rows + randomValue2] = stimCode1;

                // Hidden Matrix 2 - swap stim code row assignment
                stimCode1 = flashOrderTable[maxtrix2_rows + randomValue3];
                stimCode2 = flashOrderTable[maxtrix2_rows + randomValue4];
                flashOrderTable[maxtrix2_rows + randomValue3] = stimCode2;
                flashOrderTable[maxtrix2_rows + randomValue4] = stimCode1;

                // Hidden Matrix 1 - swap stim code col assignment
                stimCode1 = flashOrderTable[maxtrix1_cols + randomValue2];
                stimCode2 = flashOrderTable[maxtrix1_cols + randomValue1];
                flashOrderTable[maxtrix1_cols + randomValue2] = stimCode2;
                flashOrderTable[maxtrix1_cols + randomValue1] = stimCode1;

                // Hidden Matrix 2 - swap stim code col assignment
                stimCode1 = flashOrderTable[maxtrix2_cols + randomValue4];
                stimCode2 = flashOrderTable[maxtrix2_cols + randomValue3];
                flashOrderTable[maxtrix2_cols + randomValue4] = stimCode2;
                flashOrderTable[maxtrix2_cols + randomValue3] = stimCode1;
            }

        }  // ShuffleTableOrdering2()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void ShuffleTableOrdering3(ref int[] flashOrderTable)
        {

            RandomizeIndexes(ref rowIndexes);
            RandomizeIndexes(ref colIndexes);

            for (int i = 0; i < num_cols; i++)
            {
                flashOrderTable[i] = rowIndexes[i];
                flashOrderTable[num_cols + i] = colIndexes[i];
            }

        }  // ShuffleTableOrdering3()

    }  // class FlashOrder
}
