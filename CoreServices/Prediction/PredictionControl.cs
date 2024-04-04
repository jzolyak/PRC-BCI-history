using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using CoreServices.Stimulus;
using CoreServices.SensorData;
using static CoreServices.Prediction.PredictionAlgorithm;


namespace CoreServices.Prediction
{

    public class PredictionControl
    {
        // Define enumerated types used in Prediction preset configuration.
        public enum PredictionMethod { KeyID = 0, StimulusCode = 1 };
        public enum StoppingMethod { FixedNumberOfSequences = 0, DynamicStopping = 1 };

        // Create temporary preset values until we can set them at BCI start up
        public const int WIN_COUNT_FOR_SELECTION = 3;   // Min number of times a keyID must be detected as
                                                        // winner of flash sequence before make final selection

        public const int PRESET_SEQUENCE_LIMIT = 15;    // Number of times the flash sequence data is collect
                                                        // before stopping and making a keyID selection

        public const double PREDICTION_SCORE_THRESHOLD = 5000.0;    // Accumulated score must be greater than
                                                                    // this level to be considered real signal
                                                                    // and not just noise.



        // Temporay variables until I find proper location to retrieve them from.
        public int numSensorChannels = SensorConstants.NUM_CHANNELS;

        public int weightCoefPerChannel = 240;  // Not sure how to extract from coef data

        public int maxNumFlashSequences = 15;

        public int numStimulusGroups =  0;      // Get value from sensor data packet header

        public double presetScoreThreshold;


        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task<int[]> GetPredictControlMessageAsync(ISourceBlock<int[]> messageBufferBlock)
        {
            // Prediction state engine uses this method to receive messages sent from Prediction Control
            int[] messageBuffer = await messageBufferBlock.ReceiveAsync();

            return messageBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task PredictionControlAsync(CancellationToken cts, ISourceBlock<double[]> packetDataBufferBlock,
                                         ITargetBlock<int[]> messageBufferBlock, ITargetBlock<double[]> logDataBufferBlock)
        {
            // NOTE 1: For the first prototype, weightCoef[,] is an array of 7*240 double values that doesn't
            //         change during the course of Prediction. Not sure of the best way to pass this array
            //         to this task. The dimensions should be obtainable from the size of the array itself.

            // NOTE 2: What controls the number of sensor channels being processed? Is it decided by the
            //         number of sensors on the headset, or the number of sensors that were calibrated
            //         and used to compute the weight coefficients? What do we do if they don't match?

            // NOTE 3: For first prototype, we will get the constant values from struct SensorConstants
            //         and simply create a dummy test matrix filled with ones to act as weight coefficients.

            // NOTE 4: Still need to define structure of preset parameters and pass it in to this task
            //         as the start of execution

            // All packet header and sensor sample data are managed by instance of this class.
            PacketizedSensorData sensorData = new PacketizedSensorData();
            PacketDataFields packetRecord = new PacketDataFields();

            StimulusEvent stimulusEvent = new StimulusEvent();
            EventAction eventAction = new EventAction();

            PredictionScoring predictionScoring = new PredictionScoring();
            PredictionAlgorithm predictionAlgorithm = new PredictionAlgorithm();

            PredictionAlgorithm.TopScoreEntry[] topAccumulatedScores;

            PredictionAlgorithm.WinCountEntry highestWinCount;

            PredictionAlgorithm.PredictionResult predictionResult;

            // Temporarily assign preset parameters their starting value here...
            PredictionMethod predictionMethod = PredictionMethod.StimulusCode;

            StoppingMethod stoppingMethod = StoppingMethod.FixedNumberOfSequences;

            double presetScoreThreshold = PREDICTION_SCORE_THRESHOLD;



        bool stimulusEnded = false;
            bool finishedParsing = false;
            bool performPrediction = false;
            bool intersectionTestPassed = false;
            bool thresholdTestPassed = false;
            bool reportPredictionStatus = false;
            bool reportErrorStatus = false;
            bool logTestData = false;

            int rawDataReadIndex;
            int rawDataBufferLen;
            int eventStartHeaderIndex;

            int stimulusCode;
            int windowLength;

            int flashIntervalCount = 0;     // Increment once for each flash ON/OFF interval
            int flashSequenceCount = 0;     // Increment after one complete cycle of stimulus inverals has been flashed


            double[] meanScores;            // size based on number of sensor channels
            double[] stdDevScores;          // size based on number of sensor channels
            double[] flashIntervalScores;   // size based on number of sensor channels

            double[] combinedWeightedScores;    // size based on numStimulusGroups
            double[] accumulatedStimScores;     // size based on numStimulusGroups

            double[] combinedWeightedKeyIdScores;   // size based on MAX_NUM_KEYIDS
            double[] accumulatedKeyIdScores;        // size based on MAX_NUM_KEYIDS


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            while (!cts.IsCancellationRequested)
            {
                // Wait for next block of sensor packet data sent from preceeding pipeline task.
                double[] rawDataBuffer = await packetDataBufferBlock.ReceiveAsync();

                rawDataBufferLen = rawDataBuffer.Length;
                rawDataReadIndex = 0;

                finishedParsing = false;

                while (!finishedParsing) 
                {
                    // Parse the packet data into specific typecast arrays
                    packetRecord = sensorData.ParseRawDataBuffer(rawDataBuffer, ref rawDataReadIndex);

                    // Copy sensor header and sensor sample values into separate circular buffers
                    sensorData.StoreHeaderAndSensorData(packetRecord);

                    // Continue reading next packet in block from where you left off until reach end of block
                    if (rawDataReadIndex >= rawDataBufferLen)
                    {
                        finishedParsing = true;
                    }
                }

                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // Next examine stimulus markers in packet headers for change in stimulus state
                // or stimulus code value and report it back as an event action.
                eventAction = stimulusEvent.TrackStimulusMarkers(sensorData);

                // Take corresponding action in response to stimulus event
                switch (eventAction.eventType)
                {
                    case EventType.NoEvent:
                        // No action to be taken while waiting for change to occur
                        break;

                    case EventType.StateChange:

                        // In future I may want do more when transition between each stimulus phase.
                        // For now just focus on transition from Phase 1 to Phase 2.
                        if (eventAction.actionType == ActionType.ComputeStats)
                        {
                            eventStartHeaderIndex = eventAction.eventStartHeaderIndex;
                            windowLength = eventAction.windowLength;

                            meanScores = predictionScoring.ComputeMean(sensorData, eventStartHeaderIndex, windowLength);
                            stdDevScores = predictionScoring.ComputeStandardDeviation(sensorData, meanScores, eventStartHeaderIndex, windowLength);

                            numStimulusGroups = sensorData.packetHeaderTable[eventStartHeaderIndex].numStimulusGroups;

                            predictionScoring.AllocateScoringTables(numSensorChannels, numStimulusGroups, maxNumFlashSequences);

                            sensorData.CreateStimulusGroupingTable(numStimulusGroups);
                        }

                        else if (eventAction.actionType == ActionType.StimulusEnd)
                        {
                            // Flashing stimulus has stopped. Decide what to do next later in the code.
                            // It also serves as a reminder that need to handle Pause/Resume in the future.
                            stimulusEnded = true;
                        }
                        break;

                    case EventType.StimCodeChange:

                        eventStartHeaderIndex = eventAction.eventStartHeaderIndex;
                        windowLength = eventAction.windowLength;

                        stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;

                        // Compute weighted score for single stimulus interval over window length for each sensor
                        flashIntervalScores = predictionScoring.ComputeWeightedSensorScores(sensorData, eventStartHeaderIndex, windowLength);

                        // Store flash interval scores into larger table covering entire flash sequence
                        predictionScoring.StoreWeightedSensorScores(flashIntervalScores, stimulusCode);

                        // Construct table one flash interval at a time from first flash sequence
                        if (flashIntervalCount == 0)
                        {
                            sensorData.AddStimulusGroupingEntry(eventStartHeaderIndex);
                        }

                        // Determine if number of flash intervals processed is one complete flash sequence yet
                        if (++flashIntervalCount == numStimulusGroups)
                        {
                            // Reset count for start of next flash sequence
                            flashIntervalCount = 0;

                            // Increment after one complete sequences of stimulus inverals has been flashed
                            ++flashSequenceCount;

                            //if (flashSequenceCount == 1)
                            //{   // Older Calibration data files don't directly state what the target 
                            //    // stimulus keyID is. It can be derived from other fields in raw data.
                            //    // Add this function in the future.
                            //    targetKeyID = sensorData.GetTargetKeyID(numStimulusGroups);
                            //}

                            performPrediction = true;
                        }
                        else
                        {
                            performPrediction = false;
                        }
                        break;

                    case EventType.ErrorEvent:

                        if (eventAction.actionType == ActionType.ReportStateError)
                        {
                            // Prepare stimulus state tracking error message to be logged...
                        }
                        else if (eventAction.actionType == ActionType.ReportStimError)
                        {
                            // Prepare stimulus code tracking error message to be logged...
                        }
                        reportErrorStatus = true;
                        break;
                }

                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // Once the weighted score has been computed for every flash interval for
                // one complete flash sequence, we have enough data to update the Prediction
                // decision making process with the inclusion of the additional information.
                if (performPrediction == true)
                {
                    // Combine weighted scores from multiple sensors into single score value per stimulus code.
                    combinedWeightedScores = 
                        predictionScoring.ComputeCombinedWeightedScores(numSensorChannels, numStimulusGroups);

                    // Perform Golldilock test to detect out of range sensor readings using combinedWeightedScores here...

                    // Check for user looking away, or frequent inattentiveness (i.e. asleep) here...

                    /* ****************************************************
                    // Skip this until error checking is finished...

                    if (sensorDataAccepted == true)
                    {
                        // Predict which keyID the user is watching.
                    }
                    else
                    {
                        // Report top weighted sensor scores without making a prediction.
                    }
                    ***************************************************** */

                     
                    // Execute prediction algorithm based on preset configuration parameter...
                    if (predictionMethod == PredictionMethod.StimulusCode)
                    {
                        // Use StimulusCode Prediction Method to identify the attended target.

                        // Don't include problematic data in scoring tables once error checking is working.
                        predictionScoring.StoreCombinedWeigtedStimScores(combinedWeightedScores, 
                            numStimulusGroups, flashSequenceCount);

                        // For now assume all sensor data is good and continue with prediction algorithm steps.
                        accumulatedStimScores = 
                            predictionScoring.ComputeAccumulatedWeightedStimScores(numStimulusGroups, flashSequenceCount);

                        // Create short list of highest weighted sensor scores so that we can track
                        // how they change over time for each flash sequence.
                        topAccumulatedScores = predictionAlgorithm.FindTopAccumulatedScores(accumulatedStimScores);

                        // Prepare message with top 6 pairings of stimulus code values and accumulated scores...

                        predictionResult = 
                            predictionAlgorithm.StimCodePredictionAlgorithm(sensorData.stimulusGroupingTable, 
                            topAccumulatedScores, presetScoreThreshold);

                        // Fill in remaining fields of predictionResults? flashSequenceCount? ...
                    }
                    else
                    {
                        // Use KeyID Prediction Method to identify the attended target.

                        // Combine weighted scores from multiple sensors into single score value per keyID
                        combinedWeightedKeyIdScores =
                            predictionScoring.ConvertStimCodeToKeyIdScores(combinedWeightedScores, 
                            sensorData.stimulusGroupingTable, numStimulusGroups);

                        // Don't include problematic data in scoring tables once error checking is working.
                        predictionScoring.StoreCombinedWeigtedKeyIdScores(combinedWeightedKeyIdScores,
                            flashSequenceCount);

                        // For now assume all sensor data is good and continue with prediction algorithm steps.
                        accumulatedKeyIdScores =
                            predictionScoring.ComputeAccumulatedWeightedKeyIdScores(flashSequenceCount);

                        // Create short list of highest weighted sensor scores so that we can track
                        // how they change over time for each flash sequence.
                        topAccumulatedScores = predictionAlgorithm.FindTopAccumulatedScores(accumulatedKeyIdScores);

                        // Prepare message with top 6 pairings of keyID values and accumulated scores...

                        predictionResult =
                            predictionAlgorithm.KeyIdPredictionAlgorithm(sensorData.stimulusGroupingTable, 
                            topAccumulatedScores, presetScoreThreshold);

                        // Fill in remaining fields of predictionResults? flashSequenceCount? ...
                    }


                    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    // Now comes the fun part. First, there are two methods of predicting which keyID the user 
                    // is watching. Next, there are also two ways to decide when to stop monitoring the prediction
                    // results and make a keyboard selection...

                    // Report the target keyID the user is watching extracted from Calibration or Validation raw data.
                    // The attended target is not known in advance (keyID = 0) during Prediction.

                    predictionResult.attendedKeyID = 0;     // Need to fix this later for prediction accuracy reporting...

                    if (stoppingMethod == StoppingMethod.FixedNumberOfSequences)
                    {
                        // Once the preset number of flash sequence data has been collected, which ever non-zero value
                        // keyID is specified as the selectedKeyID, it is the keyID clicked in the state engine.
                        if (flashSequenceCount == PRESET_SEQUENCE_LIMIT)
                        {
                            predictionResult.selectedKeyID = predictionResult.predictedKeyID;
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                        }
                    }
                    else
                    {
                        // The Dynamic Stopping method used to make a target selection relies not only on
                        // the keyID having the highest accumulated combined weighted sensor score. This
                        // algorithm focuses on the validity of the intersecting point between pairs of
                        // stimulus codes. The algorithm also takes into account the number of times the
                        // selected keyID met additional signal threshold requirements. The algorithm keeps
                        // track of the number of times each keyID has met all the detection requirements,
                        // and once the number of wins reaches a preset level count, prediction is stopped
                        // and that key is selected.
                        if (predictionResult.predictWinCount >= WIN_COUNT_FOR_SELECTION)
                        {
                            predictionResult.selectedKeyID = predictionResult.predictedKeyID;
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                        }
                    }

                    // Set flag to send prediction report at the end of each flash sequence.
                    reportPredictionStatus = true;

                }  // if (performPrediction == true)

                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // !!! ***  Future Features goes here  *** !!!

                // Poll buffer block to check for command messages sent from state engine.
                // The state engine determines when a PAUSE, RESUME, BCI MENU, or mouse click
                // user interface selection has been made and it sends that information to
                // PredictionControl to direct what should happen next.

                // Also need to poll input buffer block for array of weight coefficients values
                // sent from PredictionStateEngine. Store values in [#chan, #coef] 2D array.
                // StoreWeightCoefficients(coefDataBuffer, numberChannels, weightCoefPerChannel)


                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */


                if (reportPredictionStatus == true)
                {
                    // Send prediction status messages back to prediction state engine...
                }

                if (reportErrorStatus == true)
                {
                    // Send error message back to prediction state engine...
                }

                if (logTestData == true)
                {
                    // Send numerical data (in ASCII format?) back to prediction state engine...
                }


            }  // while (!cts.IsCancellationRequested)

        }  // PredictionControlAsync()
    }
}