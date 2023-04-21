using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using CoreServices.Stimulus;

namespace CoreServices.Recording
{
    public class RecordBinaryData
    {

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task RecordDataAsync(CancellationToken cts, ISourceBlock<double[]> dataBufferBlock, BinaryWriter bw)
        {
            // Eventually I need to think of a better way than using constants here.
            const int NUM_MARKERS_8BIT = 8;
            const int NUM_MARKERS_16BIT = 5;
            const int NUM_TAG_MARKERS = 12;

            const int NUM_CHANNELS = 7;
            const int SAMPLES_PER_BATCH = 9;
            const int NUM_SAMPLE_SIZE = NUM_CHANNELS * SAMPLES_PER_BATCH;

            int bufferIndex;

            Byte[] markers_8bit = new Byte[NUM_MARKERS_8BIT];
            UInt16[] markers_16bit = new UInt16[NUM_MARKERS_16BIT];
            UInt16[] stimulusTags = new UInt16[NUM_TAG_MARKERS];
            Single[] sensorData = new Single[NUM_SAMPLE_SIZE];

            while (!cts.IsCancellationRequested)
            {
                double[] rawDataBuffer = await dataBufferBlock.ReceiveAsync();

                // First fetch the data from data buffer block and recast to pre-assigned recorded size.
                bufferIndex = 0;
                for (int i = 0; i < NUM_MARKERS_16BIT; i++)
                {
                    markers_16bit[i] = (UInt16)rawDataBuffer[bufferIndex++];
                }

                for (int i = 0; i < NUM_MARKERS_8BIT; i++)
                {
                    markers_8bit[i] = (Byte)rawDataBuffer[bufferIndex++];
                }

                for (int i = 0; i < NUM_TAG_MARKERS; i++)
                {
                    stimulusTags[i] = (UInt16)rawDataBuffer[bufferIndex++];
                }

                for (int i = 0; i < NUM_SAMPLE_SIZE; i++)
                {
                    sensorData[i] = (Single)rawDataBuffer[bufferIndex++];
                }

                // Store raw data into binary file by taking a copy of the marker and tag data and interleaving
                // a copy with each sample in the batch of sensor data samples. This is the way data is recorded
                // for BCI2000 input data files.
                try
                {
                    bufferIndex = 0;

                    for (int i = 0; i < SAMPLES_PER_BATCH; i++)
                    {
                        for (int j = 0; j < NUM_CHANNELS; j++)
                        {
                            bw.Write(sensorData[bufferIndex++]);
                        }

                        for (int j = 0; j < NUM_MARKERS_16BIT; j++)
                        {
                            bw.Write(markers_16bit[j]);
                        }

                        for (int j = 0; j < NUM_MARKERS_8BIT; j++)
                        {
                            bw.Write(markers_8bit[j]);
                        }

                        for (int j = 0; j < NUM_TAG_MARKERS; j++)
                        {
                            bw.Write(stimulusTags[j]);
                        }
                    }
                }
                catch (IOException e)
                {
                    Logger.Error(this, e.Message + "\n Cannot record binary data to file.");
                    return;
                }

            }  // while (!cts.IsCancellationRequested)
        
        }  // RecordDataAsync()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}
