using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using CoreServices.SensorData;

namespace CoreServices.SensorData
{

    public struct SensorConstants
    {
        public const int NUM_MARKERS_8BIT = 8;
        public const int NUM_MARKERS_16BIT = 5;
        public const int NUM_TAG_MARKERS = 12;

        // The next 5 values are directly tied to headset properties. Need to resolve this in the future
        public const int NUM_CHANNELS = 7;                  // Number of data collection sensors on the headset

        // Number of sample intervals collected in one read or write transfer
        public const int PACKETIZED_SAMPLES_PER_TRANSFER = 9;
        public const int INTERLEAVED_SAMPLES_PER_TRANSFER = 1;

        // Total number of sample values read or written at a time during data transfer
        public const int PACKETIZED_SAMPLE_SIZE = NUM_CHANNELS * PACKETIZED_SAMPLES_PER_TRANSFER;
        public const int INTERLEAVED_SAMPLE_SIZE = NUM_CHANNELS * INTERLEAVED_SAMPLES_PER_TRANSFER;
    }

    public struct PacketDataFields
    {
        public Byte[] markers_8bit;
        public UInt16[] markers_16bit;
        public UInt16[] stimulusTags;
        public double[] sensorDataValues;

        public PacketDataFields(SensorConstants constants)
        {
            markers_8bit = new Byte[SensorConstants.NUM_MARKERS_8BIT];
            markers_16bit = new UInt16[SensorConstants.NUM_MARKERS_16BIT];
            stimulusTags = new UInt16[SensorConstants.NUM_TAG_MARKERS];
            sensorDataValues = new double[SensorConstants.PACKETIZED_SAMPLE_SIZE];
        }
    }

    public struct StimGroupEntry
    {
        public UInt16 attendedTarget;
        public UInt16 matrixPosCode;
        public UInt16 stimulusType;
        public UInt16[] keyIDs;
    }

    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    public class PacketizedSensorData
    {
        public const int HEADER_CIRC_BUF_LEN = 64;

        public const int SAMPLE_CIRC_BUF_LEN = 4096;

        // There is one common header for SAMPLES_PER_BATCH of sensor data values in each packet
        // public Byte[] markers_8bit = new Byte[SensorConstants.NUM_MARKERS_8BIT];
        // public UInt16[] markers_16bit = new UInt16[NUM_MARKERS_16BIT];
        // public UInt16[] stimulusTags = new UInt16[NUM_TAG_MARKERS];

        // *** THIS MAKES NO SENSE!!!
        // Samples received from headset are double float point values and move thru pipeline as doubles.
        // public double[] sensorDataValues = new double[NUM_SAMPLE_SIZE];

        public struct PacketTableEntry
        {
            public int crossRefIndex;

            public UInt16 sourceTime;
            public UInt16 stimulusTime;
            public UInt16 blueToothCode;
            public UInt16 matrixPosCode;
            public UInt16 attendedTarget;

            public Byte bufferLevel; 
            public Byte running; 
            public Byte recording; 
            public Byte stimulusCode; 
            public Byte stimulusBegin; 
            public Byte stimulusType; 
            public Byte phaseInSeq; 
            public Byte numStimulusGroups; 

            public UInt16[] keyIDs;
        }

        // After a block of sensor packetized data has been received, it is parsed and stored 
        // into two separate circular buffers. The first circular buffer contains just the
        // packet header information (with an index link back to where the corresponding
        // samples values are stores) for tracking when the stimulus is active (also 
        // referred to a phases).

        public int headerTableWriteIndex = 0;
        public int headerTableReadIndex = 0;

        public PacketTableEntry[] packetHeaderTable = new PacketTableEntry[HEADER_CIRC_BUF_LEN];

        // The second circular buffer stores the sampled sensor data received from the headet.
        // The specific headset has NUM_CHANNELS of sensors and sends SAMPLES_PER_BATCH values
        // at a time. The data is stored in a 2D array with the columns representing the
        // individual sensors, and the rows the containing one sample reading collected from
        // each sensor at the heatset sample rate.

        public int sampleValueWriteIndex = 0;
        public int sampleReadIndex = 0;

        public double[,] sampleValues = new double[SensorConstants.NUM_CHANNELS, SAMPLE_CIRC_BUF_LEN];

        //
        public StimGroupEntry[] stimulusGroupingTable;


        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public PacketDataFields ParseRawDataBuffer(double[] rawDataBuffer, ref int startReadIndex)
        {
            int bufferIndex;

            PacketDataFields packetRecord = new PacketDataFields();


            // First fetch the data from data buffer block and recast to pre-assigned recorded size.
            bufferIndex = startReadIndex;
            for (int i = 0; i < SensorConstants.NUM_MARKERS_16BIT; i++)
            {
                packetRecord.markers_16bit[i] = (UInt16)rawDataBuffer[bufferIndex++];
            }

            for (int i = 0; i < SensorConstants.NUM_MARKERS_8BIT; i++)
            {
                packetRecord.markers_8bit[i] = (Byte)rawDataBuffer[bufferIndex++];
            }

            for (int i = 0; i < SensorConstants.NUM_TAG_MARKERS; i++)
            {
                packetRecord.stimulusTags[i] = (UInt16)rawDataBuffer[bufferIndex++];
            }

            for (int i = 0; i < SensorConstants.PACKETIZED_SAMPLE_SIZE; i++)
            {
                packetRecord.sensorDataValues[i] = rawDataBuffer[bufferIndex++];
            }

            // Return index where stopped reading from rawDataBuffer
            startReadIndex = bufferIndex;

            return packetRecord;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void StoreHeaderAndSensorData(PacketDataFields packetRecord)
        {
            // Create link to where sensor data is stored associated with this packet header.
            // The value in sampleValueWriteIndex always points to where the next sample is to be
            // stored in sampleValues array.
            packetHeaderTable[headerTableWriteIndex].crossRefIndex = sampleValueWriteIndex;

            packetHeaderTable[headerTableWriteIndex].sourceTime   = packetRecord.markers_16bit[0];
            packetHeaderTable[headerTableWriteIndex].stimulusTime = packetRecord.markers_16bit[1];
            packetHeaderTable[headerTableWriteIndex].blueToothCode = packetRecord.markers_16bit[2];
            packetHeaderTable[headerTableWriteIndex].matrixPosCode = packetRecord.markers_16bit[3];
            packetHeaderTable[headerTableWriteIndex].attendedTarget = packetRecord.markers_16bit[4];

            packetHeaderTable[headerTableWriteIndex].bufferLevel = packetRecord.markers_8bit[0];
            packetHeaderTable[headerTableWriteIndex].running     = packetRecord.markers_8bit[1];
            packetHeaderTable[headerTableWriteIndex].recording   = packetRecord.markers_8bit[2];
            packetHeaderTable[headerTableWriteIndex].stimulusCode  = packetRecord.markers_8bit[3];
            packetHeaderTable[headerTableWriteIndex].stimulusBegin = packetRecord.markers_8bit[4];
            packetHeaderTable[headerTableWriteIndex].stimulusType  = packetRecord.markers_8bit[5];
            packetHeaderTable[headerTableWriteIndex].phaseInSeq    = packetRecord.markers_8bit[6];
            packetHeaderTable[headerTableWriteIndex].numStimulusGroups = packetRecord.markers_8bit[7];

            for (int i = 0; i < SensorConstants.NUM_TAG_MARKERS; i++)
            {
                packetHeaderTable[headerTableWriteIndex].keyIDs[i] = packetRecord.stimulusTags[i];
            }

            // Update where the next packet header will be stored.
            headerTableWriteIndex = (++headerTableWriteIndex) & (HEADER_CIRC_BUF_LEN - 1);

            // Store data the way it is received from headset, i.e. NUM_CHANNELS per sample interval.
            int k = 0;
            for (int i = 0; i < SensorConstants.PACKETIZED_SAMPLES_PER_TRANSFER; i++)
            {
                for (int j = 0; j < SensorConstants.NUM_CHANNELS; j++)
                {
                    sampleValues[j, sampleValueWriteIndex] = packetRecord.sensorDataValues[k++];
                }
                sampleValueWriteIndex = (++sampleValueWriteIndex) & (SAMPLE_CIRC_BUF_LEN - 1);
            }

        }  // StoreHeaderAndSensorData()

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void CreateStimulusGroupingTable(int numStimGroupings)
        {
            StimGroupEntry[] stimulusGroupingTable = new StimGroupEntry[numStimGroupings + 1];
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void AddStimulusGroupingEntry(int headerIndex)
        {
            int stimCode = (int)packetHeaderTable[headerIndex].stimulusCode;

            stimulusGroupingTable[stimCode].attendedTarget = packetHeaderTable[headerIndex].attendedTarget;
            stimulusGroupingTable[stimCode].matrixPosCode = packetHeaderTable[headerIndex].matrixPosCode;
            stimulusGroupingTable[stimCode].stimulusType = (UInt16)packetHeaderTable[headerIndex].stimulusType;

            for (int i = 0; i < SensorConstants.NUM_TAG_MARKERS; i++)
            {
                stimulusGroupingTable[stimCode].keyIDs[i] = packetHeaderTable[headerIndex].keyIDs[i];
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }

    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    // The code for this class belongs in its own separate file.
    // It takes sensor data collected during Calibration (in BCI2000 interleaved format)
    // and converts it into sensor packet data used in Prediction
    //
    // public class SensorInterleavedData{ }

}