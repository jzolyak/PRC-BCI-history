using BCI_Headset;
using CoreServices.Common;
using CoreServices.Prediction;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CoreServices.Headset
{
    public struct HeadsetControlMessage
    {
        public const int COMMAND_MSG_SIZE = 10;

        public int[] content;

        public HeadsetControlMessage()
        {
            content = new int[COMMAND_MSG_SIZE];
        }
    }

    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */


    public class HeadsetControl
    {
        // NOTE TO SEKHAR:  (JAZ, 04-26-2024)
        //
        // Currently there is a mess when it comes to instantiating a handle to create a headset.
        // I don't believe it belongs in either the HeadsetServices or in each individual state
        // engine mode, and yet we are doing once in each place!!! That eventually needs fixed.
        //
        // I think HeadsetControl class should have an explicit constructor, and that is where
        // the creation of the headset handle should take place. Likewise, HeadsetServices
        // needs to fetch a number of preset configuration variables (i.e. montage, reference,
        // samplesPerBatch, bufferAheadSec), and pass them to HeadsetControl() constructor.
        // The constructor should also return the headset handle to the calling routine.

        public async Task ReadHeadsetDataAsync(IHeadset headset, CancellationToken cts, ITargetBlock<double[]> headsetDataBufferBlock,
                                                IReceivableSourceBlock<HeadsetControlMessage> headsetMsgBufferBlock)
        {

            const int ENABLE_PIPELINE_SENSOR_DATA = 1;
            const int DISABLE_PIPELINE_SENSOR_DATA = 2;

            const int CIRCULAR_BUF_LEN = 512;       // Num sensor samples per chan in the circular buffer
            const int MAX_CHANNELS = 7;             // Assumes no TRIG channel in montage, just (sensor - ref) values

            const int samplesPerBatch = 9;          // 9 samples = 30 msec @ Fs=300 Hz

            int messageType;
            int channelIndex;
            int sampleIndex;

            int bufferIndex;
            int dataIndex;
            int circBufReadIndex = 0;
            int circBufWriteIndex = 0;

            bool headsetMsgReceived = false;
            bool pipelineDataflowEnabled = false;

            double sourceTime = 0.0;        // Time in msec when data acquired from headset
            double signalValue;

            double[] rawDataBuffer = new double[MAX_CHANNELS * samplesPerBatch];

            double[,] CircularBuffer = new double[MAX_CHANNELS, CIRCULAR_BUF_LEN];

            HeadsetControlMessage headsetControlMessage = new HeadsetControlMessage();

            // Stopwatch stopwatch = new Stopwatch();


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */


            int numberOfChannels = headset.GetNumberOfChannels();

            while (!cts.IsCancellationRequested)
            {
                // This blocking call aims to ensure that batches of samples are picked up at
                // regular intervals (timestamps should be +/- 2 msec of expected interval).
                sourceTime = headset.WaitForBatch();

                // Start separate, independent timer to detect packet receipt errors.
                // stopwatch.Start();

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


                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // The headset is constantly transmitting raw sensor data once data acquisition has started.
                // It must be read (emptied) by the headset driver, otherwise the driver buffer will overflow.
                // The sensor data is continuously read by this task, but the task does not transfer it to
                // the processing pipeline until the state engine sends the enable command.

                if (pipelineDataflowEnabled == true)
                {
                    dataIndex = 0;
                    bufferIndex = circBufReadIndex;

                    for (sampleIndex = 0; sampleIndex < samplesPerBatch; sampleIndex++)
                    {
                        for (channelIndex = 0; channelIndex < numberOfChannels; channelIndex++)
                        {
                            signalValue = CircularBuffer[channelIndex, bufferIndex];
                            rawDataBuffer[dataIndex++] = signalValue;
                        }

                        bufferIndex = (++bufferIndex) & (CIRCULAR_BUF_LEN - 1);
                    }

                    circBufReadIndex = (circBufReadIndex + samplesPerBatch) & (CIRCULAR_BUF_LEN - 1);

                    headsetDataBufferBlock.Post(rawDataBuffer);
                }


                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // Poll buffer block to check for command messages sent from state engine.
                headsetMsgReceived = headsetMsgBufferBlock.TryReceive(out headsetControlMessage);

                if (headsetMsgReceived == true)
                {
                    messageType = headsetControlMessage.content[0];

                    if (messageType == ENABLE_PIPELINE_SENSOR_DATA)
                    {
                        pipelineDataflowEnabled = true;

                        // Synchronize sensor dataflow of buffered data with flash sequence.
                        headset.FlushBuffers();

                        // Synch buffer index for writing data into CircularBuffer and reading data out of the 
                        // buffer one batch at a time when enabled by the state engine.
                        circBufReadIndex = 0;
                        circBufWriteIndex = 0;
                    }

                    else if (messageType == DISABLE_PIPELINE_SENSOR_DATA)
                    {
                        pipelineDataflowEnabled = false;
                    }
                }

            }  // while (!cts.IsCancellationRequested)
        }

    }  // class HeadsetControl
}
