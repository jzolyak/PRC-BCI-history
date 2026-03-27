using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;


namespace CoreServices.SensorData
{

    // NOTE: Dataflow BufferBlock only send and receive "atomic" size data. In order to send
    // or receive an array of int[] or double[], you must must make array appear atomic, else
    // it only send/receives first value in the array.
    // Attempted using TryReceiveAll() to send/receive an array, but nothing transferred.

    public struct ReadBinaryStatusMessage
    {
        public const int STATUS_MSG_SIZE = 4;

        public int[] content;

        public ReadBinaryStatusMessage()
        {
            content = new int[STATUS_MSG_SIZE];
        }
    }

    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    public class ReadBinaryFileData
    {
        // There are two methods to read raw binary data from the file. The PUSH method simulates
        // receiving data at a regular packet interval rate from the headset. This is a good test
        // for checking for timing releated issues because all the tasks in the processing pipeline
        // must complete their work before the next data packet "arrives". The down side to this
        // method is that testing takes as long to complete as it to collect the raw data.
        //
        // The PULL method is used to process the sensor data as fast as possible. The Prediction
        // task at the end of the processing task takes the longest time to complete...

        public enum TransmitMethod : int { PushMethod = 1, PullMethod = 2 };

        public const int TOTAL_PACKET_SIZE_IN_DOUBLE_VALUES = 
            SensorConstants.NUM_MARKERS_16BIT + SensorConstants.NUM_MARKERS_8BIT + SensorConstants.NUM_TAG_MARKERS + SensorConstants.PACKETIZED_SAMPLE_SIZE;

        // Read Binary File Data Message ID numbers
        public const int REACHED_END_OF_FILE = 6;       // Report when successfully reach end of binary file data
        public const int ERROR_READING_FILE  = 7;       // Report error when not enough data available to create a packet


        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task ReadFileDataAsync(CancellationToken cts, SemaphoreSlim _signal, ITargetBlock<double[]> outputBufferBlock, 
                                            ITargetBlock<ReadBinaryStatusMessage> statusMsgBufferBlock,
                                            BinaryReader br, TransmitMethod transmitMethod, int interPacketDelay)
        {
            bool moreDataAvailable;

            int dataIndex;
            int bufferIndex;
            int sampleCounter;
            int numReadPackets = 0;
            int numWritePackets = 0;

            Byte[] markers_8bit = new Byte[SensorConstants.NUM_MARKERS_8BIT];
            UInt16[] markers_16bit = new UInt16[SensorConstants.NUM_MARKERS_16BIT];
            UInt16[] stimulusTags = new UInt16[SensorConstants.NUM_TAG_MARKERS];
            Single[] sensorDataValues = new Single[SensorConstants.PACKETIZED_SAMPLE_SIZE];

            double[] rawDataBuffer = new double[TOTAL_PACKET_SIZE_IN_DOUBLE_VALUES];

            ReadBinaryStatusMessage statusMessage = new ReadBinaryStatusMessage();

            moreDataAvailable = true;
            sampleCounter = 0;

            while (!cts.IsCancellationRequested)
            {
                dataIndex = 0;

                // Continue reading interleaved sensor data until reach the end of binary file.
                if (moreDataAvailable == true)
                {
                    for (int i = 0; i < SensorConstants.PACKETIZED_SAMPLES_PER_TRANSFER; i++)
                    {
                        if (br.BaseStream.Position < br.BaseStream.Length)
                        {
                            try
                            {
                                // For compatability issues with BCI2000 file storage format, sensor sample
                                // data values received from headset and processed in BCI App as 8 byte double
                                // floating point values are converted into single precison (4 byte) floating
                                // point values when they are recorded.
                                for (int j = 0; j < SensorConstants.NUM_CHANNELS; j++)
                                {
                                    sensorDataValues[dataIndex++] = br.ReadSingle();
                                }

                                for (int j = 0; j < SensorConstants.NUM_MARKERS_16BIT; j++)
                                {
                                    markers_16bit[j] = br.ReadUInt16();
                                }

                                for (int j = 0; j < SensorConstants.NUM_MARKERS_8BIT; j++)
                                {
                                    markers_8bit[j] = br.ReadByte();
                                }

                                for (int j = 0; j < SensorConstants.NUM_TAG_MARKERS; j++)
                                {
                                    stimulusTags[j] = br.ReadUInt16();
                                }

                                // Increment the number of times of succesfully read data
                                sampleCounter++;
                                numReadPackets++;
                            }
                            catch(IOException e)
                            {
                                Logger.Error(this, e.Message + "\n Cannot read from binary data file.");

                                // Send message to state engine that error occurred read from binary data file.
                                statusMessage.content[0] = ERROR_READING_FILE;                          // Message ID
                                statusMessage.content[1] = ReadBinaryStatusMessage.STATUS_MSG_SIZE;     // Message length
                                statusMessage.content[2] = 0;                                           // empty field
                                statusMessage.content[3] = 0;                                           // empty field

                                // Send Read Binary File status message back to main thread
                                statusMsgBufferBlock.Post(statusMessage);
                            }
                        }

                        else
                        {
                            moreDataAvailable = false;

                            // Post message to state engine indicating that we reached the end of currnt file.
                            // State engine will then set the cancellation token to stop the running tasks.
                            // Stop processing pipeline tasks by cancelling the shared token pipelineTokenSource.Cancel();

                            statusMessage.content[0] = REACHED_END_OF_FILE;                         // Message ID
                            statusMessage.content[1] = ReadBinaryStatusMessage.STATUS_MSG_SIZE;     // Message length
                            statusMessage.content[2] = 0;                                           // empty field
                            statusMessage.content[3] = 0;                                           // empty field

                            // Send Read Binary File status message back to main thread
                            statusMsgBufferBlock.Post(statusMessage);
                        }
                    }

                    // Verify we have read enough raw data from file to produce one sensor data packet.
                    if (sampleCounter == SensorConstants.PACKETIZED_SAMPLES_PER_TRANSFER)
                    {
                        sampleCounter = 0;

                        // Store the read data into a double precision floating point block buffer
                        // that is interfaced to Prediction task.
                        bufferIndex = 0;

                        for (int i = 0; i < SensorConstants.NUM_MARKERS_16BIT; i++)
                        {
                            rawDataBuffer[bufferIndex++] = (double)markers_16bit[i];
                        }

                        for (int i = 0; i < SensorConstants.NUM_MARKERS_8BIT; i++)
                        {
                            rawDataBuffer[bufferIndex++] = (double)markers_8bit[i];
                        }

                        for (int i = 0; i < SensorConstants.NUM_TAG_MARKERS; i++)
                        {
                            rawDataBuffer[bufferIndex++] = (double)stimulusTags[i];
                        }

                        for (int i = 0; i < SensorConstants.PACKETIZED_SAMPLE_SIZE; i++)
                        {
                            rawDataBuffer[bufferIndex++] = (double)sensorDataValues[i];
                        }

                        outputBufferBlock.Post(rawDataBuffer);
                        numWritePackets++;
                    }

                    
                    // Next, determine which mechanism to use to control when more data is read from binary data file.
                    if (transmitMethod == TransmitMethod.PushMethod)
                    {
                        // Send next packet after programmable delay to simulate data received from headset.
                        await Task.Delay(interPacketDelay);
                    }
                    else
                    {
                        // Prediction pulls next data packet as soon as it has processed the previous one.
                        // Wait to receive _signal to start next read packet.
                        await _signal.WaitAsync();
                    }


                }  // if (moreDataAvailable == true)

            }  // while (!cts.IsCancellationRequested)

        }  // ReadBinaryFileData()

    }
}
