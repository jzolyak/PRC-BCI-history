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
using System.Security.Cryptography.X509Certificates;

namespace CoreServices.Prediction
{
    // NOTE: Dataflow BufferBlock only send and receive "atomic" size data. In order to send
    // or receive an array of int[] or double[], you must must make array appear atomic, else
    // it only send/receives first value in the array.
    // Attempted using TryReceiveAll() to send/receive an array, but nothing transferred.

    public struct PredictStatusMessage
    {
        public const int STATUS_MSG_SIZE = 20;

        public int[] content;

        public PredictStatusMessage()
        {
            content = new int[STATUS_MSG_SIZE];
        }
    }

    public struct PredictCommandMessage
    {
        public const int COMMAND_MSG_SIZE = 10;

        public int[] content;

        public PredictCommandMessage()
        {
            content = new int[COMMAND_MSG_SIZE];
        }
    }

    public class WeightCoefficients
    //public struct WeightCoefficients
    {
        public const int COEF_BUFFER_SIZE = 7 * 240;

        public double[] values;

        public WeightCoefficients()
        {
            values = new double[COEF_BUFFER_SIZE];
        }
    }


    public class PredictionControl
    {

        // Temporay variables until I find proper location to retrieve them from.
        public int numSensorChannels = SensorConstants.NUM_CHANNELS;

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task<int[]> GetPredictControlMessageAsync(ISourceBlock<int[]> messageBufferBlock)
        {
            // Prediction state engine uses this method to receive messages sent from Prediction Control
            int[] messageBuffer = await messageBufferBlock.ReceiveAsync();

            return messageBuffer;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public async Task PredictionControlAsync(CancellationToken cts, PredictionPresets predictionConfiguration,
                               IReceivableSourceBlock<WeightCoefficients> weightCoefBufferBlock, ISourceBlock<double[]> packetDataBufferBlock,
                               ISourceBlock<int[]> commandMsgBufferBlock, ITargetBlock<PredictStatusMessage> statusMsgBufferBlock)
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


            // All packet header and sensor sample data are managed by instance of sensorData class.
            PacketizedSensorData sensorData = new PacketizedSensorData();
            PacketDataFields packetRecord = new PacketDataFields();

            StimulusEvent stimulusEvent = new StimulusEvent();
            EventAction eventAction = new EventAction();

            PredictionScoring predictionScoring = new PredictionScoring();
            PredictionAlgorithm predictionAlgorithm = new PredictionAlgorithm();

            PredictionAlgorithm.TopScoreEntry[] topAccumulatedScores;

            PredictionAlgorithm.WinCountEntry highestWinCount;

            PredictionAlgorithm.PredictionResult predictionResult = new PredictionResult(); ;


            /* **************** */
 
            // Configuration predition algorithm execution based on preset parameter values. 
            PredictionMethod predictionMethod = predictionConfiguration.predictionMethod;

            StoppingMethod stoppingMethod = predictionConfiguration.stoppingMethod;

            int winCountForSelection = predictionConfiguration.winCountForSelection;
            int fixedSequenceLimit = predictionConfiguration.fixedSequenceLimit;

            double predictionScoreThreshold = predictionConfiguration.predictScoreThreshold;

            /* **************** */

            bool stimulusEnded = false;
            bool finishedParsing = false;
            bool performPrediction = false;
            bool reportPredictionStatus = false;
            bool reportErrorStatus = false;
            bool logTestData = false;
            bool weightCoeffReceived = false;

            /* **************** */

            const int MAX_MSG_LENGTH = 8;           // Max message length sent back to main thread

            // Message ID numbers
            const int END_OF_PREDICTION = 99;          // Report when reach max number of flash sequences (for debug only)
            const int MARK_STATE_CHANGE = 18;       // Report when stimulus changes state (for debug only)
            const int MARK_STIM_CHANGE = 20;       // Report when stimulus changes stim code (for debug only)
            const int PREDICT_SELECTION = 21;       // Simplified prediction report info (for debug only)
            //const int PREDICTION_REPORT = 22;       // Detailed predict status at end of each flash sequence

            int statusMessageID = 0;
            int messageLength = 0;
            int phaseInSeq = 0;
            int stimulusCode = 0;
            int reportedAction = 0;

            PredictStatusMessage statusMessage = new PredictStatusMessage();
            //PredictStatusMessage statusMessage;

            WeightCoefficients weightCoefficientBuffer = new WeightCoefficients();
            //WeightCoefficients weightCoefficientBuffer;



            /* **************** */

            int rawDataReadIndex;
            int rawDataBufferLen;
            int eventStartHeaderIndex;
            int windowLength = 0;

            int flashIntervalCount = 0;     // Increment once for each flash ON/OFF interval
            int flashSequenceCount = 0;     // Increment after one complete cycle of stimulus inverals has been flashed

            int numStimulusGroups = 0;      // Get value from sensor data packet header

            double[] meanScores;            // size based on number of sensor channels
            double[] stdDevScores;          // size based on number of sensor channels
            double[] flashIntervalScores;   // size based on number of sensor channels

            double[] combinedWeightedScores;    // size based on numStimulusGroups
            double[] accumulatedStimScores;     // size based on numStimulusGroups

            double[] combinedWeightedKeyIdScores;   // size based on MAX_NUM_KEYIDS
            double[] accumulatedKeyIdScores;        // size based on MAX_NUM_KEYIDS


            /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

            sensorData.CreatePacketHeaderTable();

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
                    //if (rawDataReadIndex >= rawDataBufferLen)
                    //{
                    //    finishedParsing = true;
                    //}
                    finishedParsing = true;
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

                    /* ++++++++++++++++++++++++ */

                    case EventType.StateChange:

                        eventStartHeaderIndex = eventAction.eventStartHeaderIndex;

                        // Collect debug info to report to main thread ...
                        reportPredictionStatus = true;          // Set false to disable reporting this msg

                        statusMessageID = MARK_STATE_CHANGE;
                        messageLength = 5;
                        phaseInSeq = sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                        stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;

                        if (eventAction.actionType == ActionType.MarkStateChange)
                        {
                            reportedAction = (int)ActionType.MarkStateChange;
                        }

                        else if (eventAction.actionType == ActionType.ComputeStats)
                        {
                            windowLength = eventAction.windowLength; 
                            reportedAction = (int)ActionType.ComputeStats;

                            meanScores = predictionScoring.ComputeMean(sensorData, eventStartHeaderIndex, windowLength);
                            stdDevScores = predictionScoring.ComputeStandardDeviation(sensorData, meanScores, eventStartHeaderIndex, windowLength);
                        }

                        else if (eventAction.actionType == ActionType.AllocateScoringTables)
                        {
                            reportedAction = (int)ActionType.AllocateScoringTables;

                            numStimulusGroups = sensorData.packetHeaderTable[eventStartHeaderIndex].numStimulusGroups;

                            predictionScoring.AllocateScoringTables(numSensorChannels, numStimulusGroups, fixedSequenceLimit);

                            sensorData.CreateStimulusGroupingTable(numStimulusGroups);
                        }

                        else if (eventAction.actionType == ActionType.StimulusEnd)
                        {
                            // Flashing stimulus has stopped. Decide what to do next later in the code.
                            // It also serves as a reminder that need to handle Pause/Resume in the future.
                            stimulusEnded = true;
                            reportedAction = (int)ActionType.StimulusEnd;
                        }
                        break;

                    /* ++++++++++++++++++++++++ */

                    case EventType.StimCodeChange:

                        if (eventAction.actionType == ActionType.NoAction)
                        {
                            reportedAction = (int)ActionType.NoAction;
                        }


                        eventStartHeaderIndex = eventAction.eventStartHeaderIndex;
                        windowLength = eventAction.windowLength;

                        // Collect debug info to report to main thread ...
                        reportPredictionStatus = true;      // Set false to disable reporting this msg

                        statusMessageID = MARK_STIM_CHANGE;
                        messageLength = 5;
                        phaseInSeq = sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                        stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;
                        reportedAction = (int)eventAction.actionType;

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

                    /* ++++++++++++++++++++++++ */

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

                }  // switch (eventAction.eventType)

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
                    // Skip this logic until error checking is finished...

                    if (sensorDataAccepted == true)
                    {
                        // Predict which keyID the user is watching.
                    }
                    else
                    {
                        // Skip making prediction altogether using data from this flash sequence
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
                            topAccumulatedScores, predictionScoreThreshold);

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
                            predictionAlgorithm.KeyIdPredictionAlgorithm(topAccumulatedScores, predictionScoreThreshold);

                        // Fill in remaining fields of predictionResults? flashSequenceCount? ...
                    }


                    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    // Now comes the fun part. First, there are two methods of predicting which keyID the user 
                    // is watching. Next, there are also two ways to decide when to stop monitoring the prediction
                    // results and make a keyboard selection...

                    // Report the target keyID the user is watching extracted from Calibration or Validation raw data.
                    // The attended target is not known in advance (keyID = 0 in packet header) during Prediction.

                    predictionResult.attendedKeyID = 0;     // Need to fix this later for prediction accuracy reporting...

                    if (stoppingMethod == StoppingMethod.FixedNumberOfSequences)
                    {
                        // Once the preset number of flash sequence data has been collected, whichever non-zero value
                        // keyID is specified as the selectedKeyID, it is the keyID clicked in the state engine.
                        if (flashSequenceCount == fixedSequenceLimit)
                        {
                            predictionResult.selectedKeyID = predictionResult.predictedKeyID;
                            statusMessageID = END_OF_PREDICTION;
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                            statusMessageID = PREDICT_SELECTION;
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
                        if (predictionResult.predictWinCount >= winCountForSelection)
                        {
                            predictionResult.selectedKeyID = predictionResult.predictedKeyID;
                            statusMessageID = END_OF_PREDICTION;
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                            statusMessageID = PREDICT_SELECTION;
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

                // Next poll for input buffer block of array of weight coefficients values
                // sent from PredictionStateEngine. Store values in [#chan, #coef] 2D array.

                weightCoeffReceived = weightCoefBufferBlock.TryReceive(out weightCoefficientBuffer);

                if (weightCoeffReceived == true)
                {
                    weightCoeffReceived = false;

                    // Store values in [#chan, #coef] 2D array.
                    predictionScoring.StoreWeightCoefficients(weightCoefficientBuffer);
                }

                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */


                if (reportPredictionStatus == true)
                {
                    reportPredictionStatus = false;

                    // Send prediction status messages back to prediction state engine this way
                    // until I figure out a better way to do it ...

                    if (statusMessageID == END_OF_PREDICTION)
                    {
                        statusMessage.content[0] = END_OF_PREDICTION;     // Message ID
                        statusMessage.content[1] = messageLength;         // Message length
                    }
                    else if (statusMessageID == MARK_STATE_CHANGE)
                    {
                        statusMessage.content[0] = MARK_STATE_CHANGE;     // Message ID
                        statusMessage.content[1] = messageLength;         // Message length
                        statusMessage.content[2] = phaseInSeq;
                        statusMessage.content[3] = stimulusCode;
                        statusMessage.content[4] = reportedAction;
                    }
                    else if (statusMessageID == MARK_STIM_CHANGE)
                    {
                        statusMessage.content[0] = MARK_STIM_CHANGE;     // Message ID
                        statusMessage.content[1] = messageLength;         // Message length
                        statusMessage.content[2] = phaseInSeq;
                        statusMessage.content[3] = stimulusCode;
                        statusMessage.content[4] = reportedAction;
                    }
                    else if (statusMessageID == PREDICT_SELECTION)
                    {
                        statusMessage.content[0] = PREDICT_SELECTION;     // Message ID
                        statusMessage.content[1] = 5;                     // Message length
                        statusMessage.content[2] = predictionResult.predictedKeyID;
                        statusMessage.content[3] = predictionResult.predictWinCount;
                        statusMessage.content[4] = predictionResult.selectedKeyID;
                    }
                    else
                    {
                        // Send dummy message to indicate OOOPS!!!
                        statusMessage.content[0] = 0;
                        statusMessage.content[1] = 4;
                        statusMessage.content[2] = 0;
                        statusMessage.content[3] = 0;
                    }

                    // Send prediction status message back to main thread
                    statusMsgBufferBlock.Post(statusMessage);
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