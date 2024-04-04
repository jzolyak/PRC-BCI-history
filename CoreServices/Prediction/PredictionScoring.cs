using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using CoreServices.Stimulus;
using CoreServices.SensorData;

namespace CoreServices.Prediction
{
    public class PredictionScoring
    {
        // Create temporary constant for maximum number of KeyIDs to declare storage size
        // of tables used in KeyID Prediction Algorithm
        public const int MAX_NUM_KEYIDS = 144;

        // The number of flash stimulus intervals changes with each target selection.
        // Don't know the number of stimulus groupings until the start of Phase 2 (ActiveStim).

        public double[,] weightedScores;

        public double[,] combinedStimScoreTable;

        public double[,] accumulatedStimScoreTable;

        public double[,] combinedKeyIdScoreTable;

        public double[,] accumulatedKeyIdScoreTable;

        // Create dummy array in order to compile for now...
        public double[,] weightCoef = new double[7, 240];


        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void AllocateScoringTables(int numberChannels, int numStimGroupings, int numFlashSequences)
        {
            double[,] weightedScores = new double[numberChannels, numStimGroupings+1];

            double[] combinedWeightedScores = new double[numStimGroupings+1];

            double[,] combinedStimScoreTable = new double[numStimGroupings+1, numFlashSequences];

            double[,] accumulatedStimScoreTable = new double[numStimGroupings+1, numFlashSequences];

            double[,] combinedKeyIdScoreTable = new double[MAX_NUM_KEYIDS+1, numFlashSequences];

            double[,] accumulatedKeyIdScoreTable = new double[MAX_NUM_KEYIDS + 1, numFlashSequences];
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeMean(PacketizedSensorData sensorData, int eventStartHeaderIndex, int windowLength)
        {
            int sampleIndex;
            int startIndex;
            int numChannels = SensorConstants.NUM_CHANNELS;

            double sum;
            double[] meanScores = new double[numChannels];

            // Fetch starting column index into sampleValues[,] associated with event in packet header
            startIndex = sensorData.packetHeaderTable[eventStartHeaderIndex].crossRefIndex;

            for (int channelIndex = 0; channelIndex < numChannels; channelIndex++)
            {
                sum = 0.0;
                sampleIndex = startIndex;

                for (int i = 0; i < windowLength; i++)
                {
                    sum += sensorData.sampleValues[channelIndex, sampleIndex];
                    sampleIndex = (++sampleIndex) & (PacketizedSensorData.SAMPLE_CIRC_BUF_LEN - 1);
                }

                meanScores[channelIndex] = sum / (double)windowLength;
            }
            return meanScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeStandardDeviation(PacketizedSensorData sensorData, double[] meanScores, 
                                                 int eventStartHeaderIndex, int windowLength)
        {
            int sampleIndex;
            int startIndex;
            int numChannels = SensorConstants.NUM_CHANNELS;

            double meanValue;
            double difference;
            double variance;

            double[] standardDevScores = new double[numChannels];

            // Fetch starting column index into sampleValues[,] associated with event in packet header
            startIndex = sensorData.packetHeaderTable[eventStartHeaderIndex].crossRefIndex;

            for (int channelIndex = 0; channelIndex < numChannels; channelIndex++)
            {
                variance = 0.0;
                meanValue = meanScores[channelIndex];
                sampleIndex = startIndex;
                 
                for (int i = 0; i < windowLength; i++)
                {
                    difference = sensorData.sampleValues[channelIndex, sampleIndex] - meanValue;
                    sampleIndex = (++sampleIndex) & (PacketizedSensorData.SAMPLE_CIRC_BUF_LEN - 1);

                    variance = variance + (difference * difference);
                }

                standardDevScores[channelIndex] = Math.Sqrt((variance / (double)(windowLength - 1)));
            }
            return standardDevScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeWeightedSensorScores(PacketizedSensorData sensorData, 
                                                    int eventStartHeaderIndex, int windowLength)
        {
            int sampleIndex;
            int startIndex;
            int numChannels = SensorConstants.NUM_CHANNELS;

            double sum;
            double[] flashScores = new double[numChannels];

            // Fetch starting column index into sampleValues[,] associated with event in packet header
            startIndex = sensorData.packetHeaderTable[eventStartHeaderIndex].crossRefIndex;

            for (int channelIndex = 0; channelIndex < numChannels; channelIndex++)
            {
                sum = 0.0;
                sampleIndex = startIndex;

                for (int i = 0; i < windowLength; i++)
                {
                    // Compute a weighted score for flash interval over a window length for each sensor
                    sum += sensorData.sampleValues[channelIndex, sampleIndex] * weightCoef[channelIndex,i];
                    sampleIndex = (++sampleIndex) & (PacketizedSensorData.SAMPLE_CIRC_BUF_LEN - 1);
                }

                flashScores[channelIndex] = sum;
            }
            return flashScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void StoreWeightedSensorScores(double[] flashScores, int stimulusCode)
        {
            int numChannels = SensorConstants.NUM_CHANNELS;

            for (int channelIndex = 0; channelIndex < numChannels; channelIndex++)
            {
                // Store the weighted score for each sensor into single table convering all
                // stimulus intervals in one flash sequence.
                weightedScores[channelIndex, stimulusCode] = flashScores[channelIndex];
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void StoreWeightCoefficients(double[] rawDataBuffer, int numberChannels, int weightCoefPerChannel)
        {
            int bufIndex = 0;

            // The weight coefficient values computed in the Calibration mode are saved to a file.
            // The Prediction task receives the coefficient values from a data buffer block. The
            // buffer contents are unpacked and stored into a 2 dimensional array format here.
            for (int i = 0; i < weightCoefPerChannel; i++)
            {
                for (int channelIndex = 0; channelIndex < numberChannels; channelIndex++)
                {
                    weightCoef[channelIndex, i] = rawDataBuffer[bufIndex++];
                }
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeCombinedWeightedScores(int numberChannels, int numStimGroupings)
        {
            double sum;
            double[] combinedWeightedScores = new double[numStimGroupings];

            for (int stimCode = 1; stimCode <= numStimGroupings; stimCode++)
            {
                sum = 0.0;

                for (int channelIndex = 0; channelIndex < numberChannels; channelIndex++)
                {
                    // Add weighted score from each sensor channel into a single combined score
                    // for a stimulus code interval.
                    sum += weightedScores[channelIndex, stimCode];
                }

                // Now have single weighted score for each stimulus interval over one complete flash sequence.
                combinedWeightedScores[stimCode] = sum;
            }

            return combinedWeightedScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void StoreCombinedWeigtedStimScores(double[] combinedWeightedScores, int numStimGroupings, int flashSeqNum)
        {
            // combinedWeightedScores[] holds score for each stimulus group over one flash sequence
            for (int stimCode = 0; stimCode < numStimGroupings; stimCode++)
            {
                // combinedStimScoreTable[] holds score for each stimulus group over each "good" flash sequence
                combinedStimScoreTable[stimCode, flashSeqNum] = combinedWeightedScores[stimCode];
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeAccumulatedWeightedStimScores(int numStimGroupings, int flashSeqNum)
        {
            double acumSum;
            double[] accumulatedStimScores = new double[numStimGroupings];

            if (flashSeqNum == 1)
            {
                for (int stimCode = 0; stimCode < numStimGroupings; stimCode++)
                {   // Transfer the score value from first flash sequence to accumulated score initial value.
                    // Store accumulated scores over each flash sequences to later create a heat map of data to  
                    // show how the evoked response rises to top pair of stim scores over time.
                    accumulatedStimScoreTable[stimCode, flashSeqNum] = combinedStimScoreTable[stimCode, flashSeqNum];

                    accumulatedStimScores[stimCode] = combinedStimScoreTable[stimCode, flashSeqNum];
                }
            }
            else
            {
                for (int stimCode = 0; stimCode < numStimGroupings; stimCode++)
                {
                    acumSum = accumulatedStimScoreTable[stimCode, flashSeqNum-1] + combinedStimScoreTable[stimCode, flashSeqNum];
                    accumulatedStimScoreTable[stimCode, flashSeqNum] = acumSum;

                    // Return back combined accumulated score covering all the flash sequences thus far 
                    // for each stimulus code interval.
                    accumulatedStimScores[stimCode] = acumSum;
                }
            }
            return accumulatedStimScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ConvertStimCodeToKeyIdScores(double[] combinedWeightedScores, 
                                                     StimGroupEntry[] stimulusGroupingTable, int numStimGroupings)
        {
            int keyID;

            double stimCodeWeightedScore;

            double[] combinedKeyIdScores = new double[MAX_NUM_KEYIDS+1];

            // Zero out keyID scores before perform stim code score to keyID score conversion.
            for (keyID = 0; keyID <= MAX_NUM_KEYIDS; keyID++)
            {
                combinedKeyIdScores[keyID] = 0.0;
            }

            for (int stimCode = 1; stimCode <= numStimGroupings; stimCode++)
            {
                // Fetch the combined weighted sensor score for flash stimulus code interval
                stimCodeWeightedScore = combinedWeightedScores[stimCode];

                for (int i = 0; i < SensorConstants.NUM_TAG_MARKERS; i++)
                {
                    // Fetch keyID in flash stimulus key grouping
                    keyID = stimulusGroupingTable[stimCode].keyIDs[i];

                    if (keyID != 0)
                    {
                        // Add weighted score from stimulus code to each keyID that comprised that
                        // stimulus grouping for that flash stimulus code interval. In general,
                        // each keyID is found in two separate key groupings across the flash
                        // stimulus code intervals in one complete flash sequence.
                        combinedKeyIdScores[keyID] += stimCodeWeightedScore;
                    }
                    else
                    {   // Reached end of keys in the flash grouping
                        break;
                    }
                }
            }

            // Now have converted combined weighted score for each stimulus code into a combined
            // keyID score over one complete flash sequence.

            return combinedKeyIdScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void StoreCombinedWeigtedKeyIdScores(double[] combinedKeyIdScores, int flashSeqNum)
        {
            // combinedWeightedScores[] holds score for each keyID over one flash sequence
            for (int keyID = 0; keyID <= MAX_NUM_KEYIDS; keyID++)
            {
                // combinedStimScoreTable[] holds score for each keyID over each "good" flash sequence
                combinedKeyIdScoreTable[keyID, flashSeqNum] = combinedKeyIdScores[keyID];
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public double[] ComputeAccumulatedWeightedKeyIdScores(int flashSeqNum)
        {
            double acumSum;
            double[] accumulatedKeyIdScores = new double[MAX_NUM_KEYIDS];

            if (flashSeqNum == 1)
            {
                for (int keyID = 0; keyID <= MAX_NUM_KEYIDS; keyID++)
                {   // Transfer the score value from first flash sequence to accumulated score initial value.
                    // Store accumulated scores over each flash sequences to later create a heat map of data to  
                    // show how the evoked response rises to top of keyID scores over time.
                    accumulatedKeyIdScoreTable[keyID, flashSeqNum] = combinedKeyIdScoreTable[keyID, flashSeqNum];

                    accumulatedKeyIdScores[keyID] = combinedKeyIdScoreTable[keyID, flashSeqNum];
                }
            }
            else
            {
                for (int keyID = 0; keyID < MAX_NUM_KEYIDS; keyID++)
                {
                    acumSum = accumulatedKeyIdScoreTable[keyID, flashSeqNum - 1] + combinedKeyIdScoreTable[keyID, flashSeqNum];
                    accumulatedKeyIdScoreTable[keyID, flashSeqNum] = acumSum;

                    // Return back combined accumulated score covering all the flash sequences thus far 
                    // for each stimulus code interval.
                    accumulatedKeyIdScores[keyID] = acumSum;
                }
            }
            return accumulatedKeyIdScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}