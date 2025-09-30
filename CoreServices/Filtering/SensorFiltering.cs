using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CoreServices.Filtering
{
    public class SensorFiltering
    {

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task FilterDataAsync(CancellationToken cts, ISourceBlock<double[]> inputBufferBlock,
                                               ITargetBlock<double[]> outputBufferBlock)
        {
            const int MAX_CHANNELS = 7;  // Assumes no TRIG channel in montage, just (sensor - ref) values
            const int BATCH_SIZE = 9;    // 9 samples = 30 msec @ Fs=300 Hz
            const int HEADER_SIZE = 25;  // Number of 8-bit and 16-bit markers, plus keyID Tags prefacing each batch of data

            const int PACKET_TOTAL_SIZE = (HEADER_SIZE + MAX_CHANNELS * BATCH_SIZE);

            bool filteringDisabled = false;
            //bool filteringDisabled = true;

            int blockSize;
            int bufferIndex;

            double[] rawDataBuffer = null;

            double[] HPF_bcoef = new double[] { 1.00000000000000, -1.00000000000000 };    // Filter transfer numerator coef
            //double[] HPF_acoef = new double[] { 1.00000000000000, -0.996378967773846 };   // Filter transfer denominator coef, Fc = 0.1 Hz
            //double[] HPF_acoef = new double[] { 1.00000000000000, -0.985594038922712 };   // Filter transfer denominator coef, Fc = 0.4 Hz
            double[] HPF_acoef = new double[] { 1.00000000000000, -0.982024858591416 };   // Filter transfer denominator coef, Fc = 0.5 Hz

            double[] Notch_bcoef = new double[] { 1.00000000000000, -0.618033988749895, 1.00000000000000 };
            //double[] Notch_acoef = new double[] { 1.00000000000000, -0.556230589874906, 0.810000000000000 };  // Notch width = 0.1
            double[] Notch_acoef = new double[] { 1.00000000000000, -0.370820393249937, 0.360000000000000 };  // Notch width = 0.4

            // Fs = 300 Hz, Fpass = 15 Hz, Fstop = 30 Hz, Astop = 12 dB attenuation
            //double[] LPF_bcoef = new double[] { 0.00589798046521114, 0.0176939413956334, 0.0176939413956334, 0.00589798046521114 };
            //double[] LPF_acoef = new double[] { 1.00000000000000, -2.18768735399289, 1.67381733173863, -0.438946134024050 };

            // Fs = 300 Hz, Fpass = 20 Hz, Fstop = 50 Hz, Astop = 30 dB attenuation
            double[] LPF_bcoef = new double[] { 0.000803296929195750, 0.00401648464597875, 0.00803296929195750, 0.00803296929195750, 0.00401648464597875, 0.000803296929195750 };
            double[] LPF_acoef = new double[] { 1.00000000000000, -3.18324708716957, 4.27761443724719, -2.97985186657190, 1.06783705587382, -0.156647037645276 };

            int HPF_order = HPF_acoef.Length - 1;       // IIR Filter order is 1 less than number of denominator coef
            int Notch_order = Notch_acoef.Length - 1;
            int LPF_order = LPF_acoef.Length - 1;

            double[] Z1delay = new double[HPF_order];
            double[] Z2delay = new double[Notch_order];
            double[] Z3delay = new double[LPF_order];

            double[,] HPF_delay = new double[MAX_CHANNELS, HPF_order];
            double[,] Notch_delay = new double[MAX_CHANNELS, Notch_order];
            double[,] LPF_delay = new double[MAX_CHANNELS, LPF_order];

            double[] Xin = new double[BATCH_SIZE];
            double[] Y1out = new double[BATCH_SIZE];
            double[] Y2out = new double[BATCH_SIZE];
            double[] Y3out = new double[BATCH_SIZE];

            double[,] SensorInputData = new double[MAX_CHANNELS, BATCH_SIZE];
            double[,] SensorOutputData = new double[MAX_CHANNELS, BATCH_SIZE];

            double[] filterOutputBuffer = new double[PACKET_TOTAL_SIZE];


            // Stopwatch stopwatch = new Stopwatch();

            while (!cts.IsCancellationRequested)
            {
                // Start separate, independent timer to measure filter execution time.
                // stopwatch.Start();

                // Wait for next block of sensor packet data sent from preceeding pipeline task.
                if (await inputBufferBlock.OutputAvailableAsync(cts))
                {
                    try
                    {
                        rawDataBuffer = await inputBufferBlock.ReceiveAsync(cts);
                        blockSize = rawDataBuffer.Length;
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

                // If sensor filtering is disabled, then directly pass sensor data packet to next stage.
                if (filteringDisabled == true)
                {
                    for (int i = 0; i < PACKET_TOTAL_SIZE; i++)
                    {
                        filterOutputBuffer[i] = rawDataBuffer[i];
                    }
                    outputBufferBlock.Post(filterOutputBuffer);
                }
                else
                {
                    // Leave the packet header in the original buffer and fetch the raw sensor data beyond that point
                    bufferIndex = HEADER_SIZE;

                    // De-interleave raw data from one sample per sensor into an array row of samples per sensor
                    for (int sampleIndex = 0; sampleIndex < BATCH_SIZE; sampleIndex++)
                    {
                        for (int channelIndex = 0; channelIndex < MAX_CHANNELS; channelIndex++)
                        {
                            SensorInputData[channelIndex, sampleIndex] = rawDataBuffer[bufferIndex++];
                        }
                    }

                    // Perform the signal processing stages on each sensor channel
                    for (int channelIndex = 0; channelIndex < MAX_CHANNELS; channelIndex++)
                    {
                        // Filter pipeline stage 1
                        for (int sampleIndex = 0; sampleIndex < BATCH_SIZE; sampleIndex++)
                        {
                            Xin[sampleIndex] = SensorInputData[channelIndex, sampleIndex];
                        }
                        for (int j = 0; j < HPF_order; j++)
                        {
                            Z1delay[j] = HPF_delay[channelIndex, j];
                        }

                        Y1out = HighpassFilter(HPF_bcoef, HPF_acoef, Xin, ref Z1delay);

                        for (int j = 0; j < HPF_order; j++)
                        {
                            HPF_delay[channelIndex, j] = Z1delay[j];
                        }

                        // **************************************

                        // Filter pipeline stage 2
                        for (int j = 0; j < Notch_order; j++)
                        {
                            Z2delay[j] = Notch_delay[channelIndex, j];
                        }

                        // Use the stage 1 filter output as the stage 2 filter input
                        Y2out = NotchFilter(Notch_bcoef, Notch_acoef, Y1out, ref Z2delay);

                        for (int j = 0; j < Notch_order; j++)
                        {
                            Notch_delay[channelIndex, j] = Z2delay[j];
                        }

                        // **************************************

                        // Filter pipeline stage 3
                        for (int j = 0; j < LPF_order; j++)
                        {
                            Z3delay[j] = LPF_delay[channelIndex, j];
                        }

                        Y3out = LowpassFilter(LPF_bcoef, LPF_acoef, Y2out, ref Z3delay);   // IIR???

                        for (int j = 0; j < LPF_order; j++)
                        {
                            LPF_delay[channelIndex, j] = Z3delay[j];
                        }

                        // **************************************

                        // Copy final filtered output to buffer to be interleaved at the end
                        for (int sampleIndex = 0; sampleIndex < BATCH_SIZE; sampleIndex++)
                        {
                            SensorOutputData[channelIndex, sampleIndex] = Y3out[sampleIndex];
                        }

                    }  // for (channelIndex)


                    // Copy the original packet header to the filter output buffer
                    for (int i = 0; i < HEADER_SIZE; i++)
                    {
                        filterOutputBuffer[i] = rawDataBuffer[i];
                    }

                    // Interleave filtered data from an array row of samples per sensor into a row of one sample per sensor
                    bufferIndex = HEADER_SIZE;
                    for (int sampleIndex = 0; sampleIndex < BATCH_SIZE; sampleIndex++)
                    {
                        for (int channelIndex = 0; channelIndex < MAX_CHANNELS; channelIndex++)
                        {
                            filterOutputBuffer[bufferIndex++] = SensorOutputData[channelIndex, sampleIndex];
                        }
                    }

                    /*
                    // stopwatch.Stop();
                    // int stopwatchElasped = stopwatch.Elapsed.Milliseconds;
                    */

                    // Send the filtered data to the next processing task via the buffer block
                    outputBufferBlock.Post(filterOutputBuffer);

                }  // if (filteringDisabled)

            }  // while()
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] HighpassFilter(double[] HPF_bcoef, double[] HPF_acoef,
                                       double[] filterInput, ref double[] Zdelay)
        {
            int numSamples = filterInput.Length;
            int filterOrder = HPF_acoef.Length - 1;     // For IIR design filters the filter order is the
                                                        // number of filter delay taps which is equal to 
                                                        // the number of acoef minus one.

            double[] filterOutput = new double[numSamples];

            for (int m = 0; m < numSamples; m++)
            {
                filterOutput[m] = HPF_bcoef[0] * filterInput[m] + Zdelay[0];

                for (int n = 1; n < filterOrder; n++)
                {
                    Zdelay[n - 1] = (HPF_bcoef[n] * filterInput[m]) + Zdelay[n] - (HPF_acoef[n] * filterOutput[m]);
                }
                Zdelay[filterOrder - 1] = (HPF_bcoef[filterOrder] * filterInput[m]) - (HPF_acoef[filterOrder] * filterOutput[m]);
            }
            return filterOutput;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] NotchFilter(double[] Notch_bcoef, double[] Notch_acoef,
                                    double[] filterInput, ref double[] Zdelay)
        {
            int numSamples = filterInput.Length;
            int filterOrder = Notch_acoef.Length - 1;   // For IIR design filters the filter order is the
                                                        // number of filter delay taps which is equal to 
                                                        // the number of acoef minus one.

            double[] filterOutput = new double[numSamples];

            for (int m = 0; m < numSamples; m++)
            {
                filterOutput[m] = Notch_bcoef[0] * filterInput[m] + Zdelay[0];

                for (int n = 1; n < filterOrder; n++)
                {
                    Zdelay[n - 1] = (Notch_bcoef[n] * filterInput[m]) + Zdelay[n] - (Notch_acoef[n] * filterOutput[m]);
                }
                Zdelay[filterOrder - 1] = (Notch_bcoef[filterOrder] * filterInput[m]) - (Notch_acoef[filterOrder] * filterOutput[m]);
            }
            return filterOutput;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] LowpassFilter(double[] LPF_bcoef, double[] LPF_acoef,
                                      double[] filterInput, ref double[] Zdelay)
        {
            int numSamples = filterInput.Length;
            int filterOrder = LPF_acoef.Length - 1;     // For IIR design filters the filter order is the
                                                        // number of filter delay taps which is equal to 
                                                        // the number of acoef minus one.

            double[] filterOutput = new double[numSamples];

            for (int m = 0; m < numSamples; m++)
            {
                filterOutput[m] = LPF_bcoef[0] * filterInput[m] + Zdelay[0];

                for (int n = 1; n < filterOrder; n++)
                {
                    Zdelay[n - 1] = (LPF_bcoef[n] * filterInput[m]) + Zdelay[n] - (LPF_acoef[n] * filterOutput[m]);
                }
                Zdelay[filterOrder - 1] = (LPF_bcoef[filterOrder] * filterInput[m]) - (LPF_acoef[filterOrder] * filterOutput[m]);
            }
            return filterOutput;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}
