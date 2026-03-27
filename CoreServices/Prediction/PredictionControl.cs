using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using CoreServices.Stimulus;
using CoreServices.SensorData;
using CoreServices.Presets;
using static CoreServices.Prediction.PredictionAlgorithm;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using static CoreServices.SensorData.ReadBinaryFileData;

namespace CoreServices.Prediction
{
    // NOTE: Dataflow BufferBlock only send and receive "atomic" size data. In order to send
    // or receive an array of int[] or double[], you must must make array appear atomic, else
    // it only send/receives first value in the array.
    // Attempted using TryReceiveAll() to send/receive an array, but nothing transferred.

    public struct PredictStatusMessage
    {
        public const int STATUS_MSG_SIZE = 24;

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

    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    public class PredictionControl
    {
        // Prediction Control Message ID numbers
        public enum PredictControlMsgID : int {
            MARK_STATE_CHANGE = 18,     // Report when stimulus changes state (for debug only)
            MARK_STIM_CHANGE = 20,      // Report when stimulus changes stim code (for debug only)
            PREDICTION_STATUS = 21,     // Detailed predict status at end of each flash sequence
            STIM_STATE_ERROR = 30,      // Report when unexpected value appeared while parsing packet data
            STIM_CODE_ERROR = 32        // Report when unexpected value appeared while parsing packet data
        }

        // Indicate if just reporting current status, or which final decision was reached at end of flash sequence.
        public enum EndDecision : int
        {
            STATUS_UPDATE_MSG = 0,      // Current prediction status at end of each flash sequence
            ABSTENTION_PREDICT = 1,     // Report when reach max number of flash sequences, but no key selected
            FORCED_PREDICTION = 2,      // Report when reach max number of flash seq, force select with highest win count
            END_OF_PREDICTION = 3       // Report keyID when reach max number of flash sequences or dynamic stopping point
        }

        /*
        public string[] EndDecisionStrings =
        {
            "STATUS_UPDATE", "ABSTENTION_PREDICT", "FORCED_PREDICTION", "END_OF_PREDICTION"
        };
        */

        public class EndDecisionStrings
        {
            /*
            private string[] _decisionString =
            {
                "STATUS_UPDATE", "ABSTENTION_PREDICT", "FORCED_PREDICTION", "END_OF_PREDICTION"
            };
            public string outputString = "";

            public string GetString(int enumIndex)
            {
                outputString = _decisionString[enumIndex];
                return outputString;
            }
            */

            private string[] _decisionString =
            {
                "STATUS_UPDATE", "ABSTENTION_PREDICT", "FORCED_PREDICTION", "END_OF_PREDICTION"
            };

            public string GetString(int enumIndex)
            {
                return _decisionString[enumIndex]; ;
            }
        };

        // Report current evaluation of the ongoing prediction accuracy until a selection is made.
        // "GOOD_CHOICE" and "BAD_CHOICE" refer to outcome if prediction preset values changed 
        // (i.e. lower win count from 3 to 2), and you had to make a choice at current flash sequence.
        public enum PredictionAccuracy : int 
        {
            NO_TARGET = 0,      // In real-time prediction, you can't predict accuracy without knowing attended target
            NO_CHOICE = 1,      // No intersecting key ID found in top stim code scores, so no prediction made
            BAD_CHOICE = 2,     // Predicted key ID did NOT match Attended key ID, but not reach final decision criteria
            BAD_SELECT = 3,     // Predicted key ID did NOT match Attended key ID, and reached final incorrect selection
            GOOD_CHOICE = 4,    // Predicted key ID DID match Attended key ID, but not reach final decision criteria
            GOOD_SELECT = 5     // Predicted key ID DID match Attended key ID, AND reached final correct selection
        }

        /*
        public string[] PredictionAccuracyStrings =
        {
            "NO_TARGET", "NO_CHOICE", "BAD_CHOICE", "BAD_SELECT", "GOOD_CHOICE", "GOOD_SELECT"
        };
        */

        public class PredictionAccuracyStrings
        {
            private string[] _accuracyString =
            {
            "NO_TARGET", "NO_CHOICE", "BAD_CHOICE", "BAD_SELECT", "GOOD_CHOICE", "GOOD_SELECT"
            };

            public string GetString(int enumIndex)
            {
                return _accuracyString[enumIndex]; ;
            }
        }

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
                               ISourceBlock<int[]> commandMsgBufferBlock, ITargetBlock<PredictStatusMessage> statusMsgBufferBlock,
                               SemaphoreSlim _signal, TransmitMethod transmitMethod)
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

            PredictionAlgorithm.TopScoreEntry[] topAccumulatedScores = null;

            PredictionAlgorithm.WinCountEntry highestWinCount = new WinCountEntry();

            PredictionAlgorithm.PredictionResult predictionResult = new PredictionResult(); ;


            /* **************** */

            // Configuration predition algorithm execution based on preset parameter values. 
            DecisionLogicPresets.ScoringMethod scoringMethod = predictionConfiguration.DecisionLogicPresets.scoringMethod;
            DecisionLogicPresets.StoppingMethod stoppingMethod = predictionConfiguration.DecisionLogicPresets.stoppingMethod;
            DecisionLogicPresets.DecisionMethod finalDecisionType = predictionConfiguration.DecisionLogicPresets.finalDecisionType;

            int maxNumFlashSequences = predictionConfiguration.DecisionLogicPresets.maxNumFlashSequences;
            int scoringWindowLength = predictionConfiguration.DecisionLogicPresets.scoringWindowLength;

            int highestTopScores = predictionConfiguration.PredictionCriteriaPresets.highestTopScores;
            int winCountForSelection = predictionConfiguration.PredictionCriteriaPresets.winCountForSelection;
            double predictionScoreThreshold = (double) predictionConfiguration.PredictionCriteriaPresets.predictScoreThreshold;

            /* **************** */

            bool stimulusEnded = false;
            bool finishedParsing = false;
            bool performPrediction = false;
            bool reportPredictionStatus = false;
            bool reportErrorStatus = false;
            bool logTestData = false;
            bool weightCoeffReceived = false;

            /* **************** */

            /*
            // const int MAX_MSG_LENGTH = 8;           // Max message length sent back to main thread

            // Prediction Control Message ID numbers
            const int MARK_STATE_CHANGE = 18;       // Report when stimulus changes state (for debug only)
            const int MARK_STIM_CHANGE = 20;        // Report when stimulus changes stim code (for debug only)

            const int STIM_STATE_ERROR = 30;        // Report when unexpected value appeared in packet data
            const int STIM_CODE_ERROR = 32;         // Report when unexpected value appeared in packet data

            const int PREDICTION_STATUS = 21;          // Detailed predict status at end of each flash sequence
            const int ABSTENTION_PREDICT = 92;      // Report when reach max number of flash sequences, but no key selected
            const int FORCED_PREDICTION = 94;          // Report when reach max number of flash seq, force select with highest win count
            const int END_OF_PREDICTION = 99;       // Report keyID when reach max number of flash sequences or reach dynamic stopping

            // Report current evaluation of the ongoing prediction accuracy until a selection is made
            const int NO_TARGET = 0;                // 
            const int NO_CHOICE = 1;                // 
            const int BAD_CHOICE = 2;               // 
            const int BAD_SELECT = 3;               // 
            const int GOOD_CHOICE = 4;              // 
            const int GOOD_SELECT = 5;              // 
            */

            int messageLength = 0;
            int phaseInSeq = 0;
            int stimulusCode = 0;
            int reportedAction = 0;
            int attendedKeyID = 0;

            PredictControlMsgID messageID = PredictControlMsgID.PREDICTION_STATUS;

            PredictStatusMessage statusMessage = new PredictStatusMessage();

            WeightCoefficients weightCoefficientBuffer = new WeightCoefficients();


            /* **************** */

            int packetCount = 0;
            int incorrectSizeCount = 0;

            int blockSize;
            int rawDataReadIndex;
            int rawDataBufferLen = 0;
            int eventStartHeaderIndex;
            int windowLength = 0;

            int flashIntervalCount = 0;     // Increment once for each flash ON/OFF interval
            int flashSequenceCount = 0;     // Increment after one complete cycle of stimulus inverals has been flashed

            int numStimulusGroups = 0;      // Get value from sensor data packet header

            double[] rawDataBuffer = null;

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
                if (await packetDataBufferBlock.OutputAvailableAsync(cts))
                {
                    try
                    {
                        rawDataBuffer = await packetDataBufferBlock.ReceiveAsync(cts);
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
                        reportPredictionStatus = true;          // Needs to be set true for now as a work around
                        										// to trigger when to send weight coef data to task.

                        messageID = PredictControlMsgID.MARK_STATE_CHANGE;
                        messageLength = 5;
                        phaseInSeq = (int)sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                        stimulusCode = (int)sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;

                        if (eventAction.actionType == ActionType.MarkStateChange)
                        {
                            reportedAction = (int)eventAction.actionType;
                        }

                        else if (eventAction.actionType == ActionType.ComputeStats)
                        {
                            reportedAction = (int)eventAction.actionType;

                            windowLength = eventAction.windowLength;

                            meanScores = predictionScoring.ComputeMean(sensorData, eventStartHeaderIndex, windowLength);
                            stdDevScores = predictionScoring.ComputeStandardDeviation(sensorData, meanScores, eventStartHeaderIndex, windowLength);
                        }

                        else if (eventAction.actionType == ActionType.AllocateScoringTables)
                        {
                            reportedAction = (int)eventAction.actionType;

                            numStimulusGroups = sensorData.packetHeaderTable[eventStartHeaderIndex].numStimulusGroups;

                            predictionScoring.AllocateScoringTables(numSensorChannels, numStimulusGroups, maxNumFlashSequences);

                            sensorData.CreateStimulusGroupingTable(numStimulusGroups);
                        }

                        else if (eventAction.actionType == ActionType.StimulusEnd)
                        {
                            // Flashing stimulus has stopped. Decide what to do next later in the code.
                            // It also serves as a reminder that need to handle Pause/Resume in the future.
                            stimulusEnded = true;
                            reportedAction = (int)eventAction.actionType;
                        }
                        break;

                    /* ++++++++++++++++++++++++ */

                    case EventType.StimCodeChange:

                        if (eventAction.actionType == ActionType.NoAction)
                        {
                            reportedAction = (int)eventAction.actionType;
                        }

                        else if (eventAction.actionType == ActionType.ComputeScore)
                        {
                            eventStartHeaderIndex = eventAction.eventStartHeaderIndex;
                            windowLength = eventAction.windowLength;

                            // Collect debug info to report to main thread ...
                            reportPredictionStatus = false;      // Set false to disable reporting this msg

                            messageID = PredictControlMsgID.MARK_STIM_CHANGE;
                            messageLength = 5;
                            phaseInSeq = sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                            stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;
                            reportedAction = (int)eventAction.actionType;

                            // Compute weighted score for single stimulus interval over window length for each sensor
                            flashIntervalScores = predictionScoring.ComputeWeightedSensorScores(sensorData, eventStartHeaderIndex, windowLength);

                            // Store flash interval scores into larger table covering entire flash sequence
                            predictionScoring.StoreWeightedSensorScores(flashIntervalScores, stimulusCode);

                            // Construct table one flash interval at a time from first flash sequence
                            if (flashSequenceCount == 0)
                            {
                                sensorData.AddStimulusGroupingEntry(eventStartHeaderIndex);

                                if (sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusType != 0)
                                {
                                    // This stim code was marked (set to 1) as containing target keyID value during Calibration
                                    // or Validation. Save the keyID value to test the prediction accuracy.
                                    attendedKeyID = (int)sensorData.packetHeaderTable[eventStartHeaderIndex].attendedTarget;
                                }
                            }

                            // Determine if number of flash intervals processed is one complete flash sequence yet
                            if (++flashIntervalCount == numStimulusGroups)
                            {
                                // Reset count for start of next flash sequence
                                flashIntervalCount = 0;

                                // Increment after one complete sequences of stimulus inverals has been flashed
                                // ++flashSequenceCount;

                                // Have collected enough sensor data to run Prediction algorithm.
                                performPrediction = true;
                            }
                            else
                            {
                                performPrediction = false;
                            }
                        }
                        break;

                    /* ++++++++++++++++++++++++ */

                    case EventType.ErrorEvent:

                        eventStartHeaderIndex = eventAction.eventStartHeaderIndex;

                        if (eventAction.actionType == ActionType.ReportStateError)
                        {
                            // Prepare stimulus state tracking error message to be logged.
                            messageID = PredictControlMsgID.STIM_STATE_ERROR;
                            messageLength = 5;
                            phaseInSeq = sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                            stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;
                            reportedAction = (int)eventAction.actionType;
                        }
                        else if (eventAction.actionType == ActionType.ReportStimError)
                        {
                            // Prepare stimulus code tracking error message to be logged.
                            messageID = PredictControlMsgID.STIM_CODE_ERROR;
                            messageLength = 5;
                            phaseInSeq = sensorData.packetHeaderTable[eventStartHeaderIndex].phaseInSeq;
                            stimulusCode = sensorData.packetHeaderTable[eventStartHeaderIndex].stimulusCode;
                            reportedAction = (int)eventAction.actionType;
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
                    performPrediction = false;

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
                    if (scoringMethod == DecisionLogicPresets.ScoringMethod.StimulusCode)
                    {
                        // Use StimulusCode Scoring Method to identify the attended target.

                        // Don't include problematic data in scoring tables once error checking is working.
                        predictionScoring.StoreCombinedWeigtedStimScores(combinedWeightedScores, 
                            numStimulusGroups, flashSequenceCount);

                        // For now assume all sensor data is good and continue with prediction algorithm steps.
                        accumulatedStimScores = 
                            predictionScoring.ComputeAccumulatedWeightedStimScores(numStimulusGroups, flashSequenceCount, scoringWindowLength);

                        // Create short list of highest weighted sensor scores so that we can track
                        // how they change over time for each flash sequence.
                        topAccumulatedScores = predictionAlgorithm.FindTopAccumulatedScores(accumulatedStimScores);

                        // Prepare message with top 6 pairings of stimulus code values and accumulated scores...

                        predictionResult = 
                            predictionAlgorithm.StimCodePredictionAlgorithm(sensorData.stimulusGroupingTable, 
                            topAccumulatedScores, predictionScoreThreshold, highestTopScores);

                        // Fill in remaining fields of predictionResults? flashSequenceCount? ...
                        predictionResult.flashSequenceCount = flashSequenceCount;
                    }
                    else
                    {
                        // Use KeyID Scoring Method to identify the attended target.

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
                        predictionResult.flashSequenceCount = flashSequenceCount;
                    }


                    /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                    // Now comes the fun part. There are also two ways to decide when to stop monitoring the prediction
                    // results and make a keyboard selection: Fixed Number of Flash Sequences, and Dynamic Stopping...

                    // The original method of selecting the predicted winning key was based on the keyID having the
                    // highest accumulated score at the end of the fixed number of sequences. Large signal artifacts
                    // cause by sneezing, headset movement, can create a large noise spike that impacts the total
                    // accumulated score.

                    // New version of selecting the predicted keyID is to keep track of which keyID won (i.e. met the
                    // detection criteria) for each individual flash sequence period. The keyID that has the highest
                    // win total count when it is time to stop is predicted as the selected key. If the win count of
                    // the keyID does not equal or exceed a preset threshold count value, then no keyID (Abstention)
                    // is decided.

                    if ((predictionResult.predictedKeyID != 0) && (predictionResult.predictWinCount > 0))
                    {
                        // Confirmed that keyID met winning criteria.
                        if (predictionResult.predictWinCount >= highestWinCount.winCount)
                        {
                            highestWinCount.keyID = predictionResult.predictedKeyID;
                            highestWinCount.winCount = predictionResult.predictWinCount;
                        }
                    }

                    // Increment after one complete sequences of stimulus inverals has been flashed
                    ++flashSequenceCount;
                    messageID = PredictControlMsgID.PREDICTION_STATUS;

                    // Report the target keyID the user is watching extracted from Calibration or Validation raw data.
                    // The attended target is not known in advance (keyID = 0 in packet header) during Prediction.

                    predictionResult.attendedKeyID = attendedKeyID;

                    if (stoppingMethod == DecisionLogicPresets.StoppingMethod.FixedNumberOfSequences)
                    {
                        // Once the preset number of flash sequence data has been collected, whichever non-zero value
                        // keyID is specified as the selectedKeyID, it is the keyID clicked in the state engine.
                        if (flashSequenceCount == maxNumFlashSequences)
                        {
                            if (highestWinCount.winCount >= winCountForSelection)
                            {
                                predictionResult.selectedKeyID = highestWinCount.keyID; // Select this key!
                                predictionResult.decision = (int)EndDecision.END_OF_PREDICTION;
                            }
                            else
                            {
                                // Check preset to determine if should report abstention (no selection)
                                // or force a selection of the keyID with the current highest score.
                                if (finalDecisionType == DecisionLogicPresets.DecisionMethod.Abstention)
                                {
                                    predictionResult.selectedKeyID = 0;     // Report Abstention (keyID = 0)
                                    predictionResult.decision = (int)EndDecision.ABSTENTION_PREDICT;
                                }
                                else
                                {
                                    predictionResult.selectedKeyID = highestWinCount.keyID; // Force select of this key!
                                    predictionResult.decision = (int)EndDecision.FORCED_PREDICTION;
                                }
                            }
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                            predictionResult.decision = (int)EndDecision.STATUS_UPDATE_MSG;
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
                        if (highestWinCount.winCount >= winCountForSelection)
                        {
                            predictionResult.selectedKeyID = highestWinCount.keyID;
                            predictionResult.decision = (int)EndDecision.END_OF_PREDICTION;
                        }
                        else if (flashSequenceCount == maxNumFlashSequences)
                        {
                            // If reached the fixed limit, and NOT MADE A SELECTION, then stop current prediction.
                            // Check preset to determine if should report abstention (no selection)
                            // or force a selection of the current highest score.
                            if (finalDecisionType == DecisionLogicPresets.DecisionMethod.Abstention)
                            {
                                predictionResult.selectedKeyID = 0;     // Report Abstention (keyID = 0)
                                predictionResult.decision = (int)EndDecision.ABSTENTION_PREDICT;
                            }
                            else
                            {
                                predictionResult.selectedKeyID = highestWinCount.keyID; // Force select of this key!
                                predictionResult.decision = (int)EndDecision.FORCED_PREDICTION;
                            }
                        }
                        else
                        {
                            // Continue collecting data without making a prediction
                            predictionResult.selectedKeyID = 0;
                            predictionResult.decision = (int)EndDecision.STATUS_UPDATE_MSG;
                        }
                    }

                    // During Calibration and Validation the attended target keyID value is known in advance, and
                    // it is sent as a nonzero value in the sensor packet data. (During Prediction, the software 
                    // doesn't know which key the user is supposed to watching, hence the target value is zero).
                    // Evaluate the accuracy of the prediction and report it back with the other results.
                    if (attendedKeyID != 0)
                    {
                        if (predictionResult.predictedKeyID == attendedKeyID)
                        {
                            if (predictionResult.selectedKeyID == attendedKeyID)
                            {   // Met select criteria for final choice.
                                predictionResult.accuracy = (int)PredictionAccuracy.GOOD_SELECT;
                            }
                            else
                            {   // On the right track, but not ready to make final decision yet
                                predictionResult.accuracy = (int)PredictionAccuracy.GOOD_CHOICE;
                            }
                        }
                        else
                        {
                            if (predictionResult.predictedKeyID == 0)
                            {   // No intersecting point amongst the top accumulated scores, not certain of anything
                                predictionResult.accuracy = (int)PredictionAccuracy.NO_CHOICE;
                            }
                            else if (predictionResult.selectedKeyID != 0)
                            {   // Met select criteria for final choice, but it wasn't correct target keyID
                                predictionResult.accuracy = (int)PredictionAccuracy.BAD_SELECT;
                            }
                            else
                            {   // On the wrong track, but not ready to make final decision yet
                                predictionResult.accuracy = (int)PredictionAccuracy.BAD_CHOICE;
                            }
                        }
                    }
                    else
                    {   // During real-time prediction, user can select any target key, hence it is not
                        // known in advance to evaluate the accuracy of the prediction.
                        predictionResult.accuracy = (int)PredictionAccuracy.NO_TARGET;
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

                // Check if it is time to send status message back to the state engine
                if (reportPredictionStatus == true)
                {
                    reportPredictionStatus = false;

                    // Send prediction status messages back to prediction state engine this way
                    // until I figure out a better way to do it ...

                    if (messageID == PredictControlMsgID.MARK_STATE_CHANGE)
                    {
                        statusMessage.content[0] = (int)messageID;
                        statusMessage.content[1] = messageLength;         // Message length
                        statusMessage.content[2] = phaseInSeq;
                        statusMessage.content[3] = stimulusCode;
                        statusMessage.content[4] = reportedAction;
                    }

                    else if (messageID == PredictControlMsgID.MARK_STIM_CHANGE)
                    {
                        statusMessage.content[0] = (int)messageID;
                        statusMessage.content[1] = messageLength;         // Message length
                        statusMessage.content[2] = phaseInSeq;
                        statusMessage.content[3] = stimulusCode;
                        statusMessage.content[4] = reportedAction;
                    }

                    else if (messageID == PredictControlMsgID.PREDICTION_STATUS)
                    {
                        // Send back prediction status 
                        statusMessage.content[0] = (int)messageID;   // Message ID
                        statusMessage.content[1] = 21;                     // Message length
                        statusMessage.content[2] = predictionResult.flashSequenceCount;
                        statusMessage.content[3] = topAccumulatedScores[0].indexValue;
                        statusMessage.content[4] = topAccumulatedScores[1].indexValue;
                        statusMessage.content[5] = topAccumulatedScores[2].indexValue;
                        statusMessage.content[6] = topAccumulatedScores[3].indexValue;
                        statusMessage.content[7] = topAccumulatedScores[4].indexValue;

                        statusMessage.content[8] = predictionResult.stimCodeIndex1;     // stim codes that intersect at common key ID
                        statusMessage.content[9] = predictionResult.stimCodeIndex2;

                        // Conversion accuracy is not critical. Truncate double to create integer value score
                        statusMessage.content[10] = (int)topAccumulatedScores[0].accumScore;
                        statusMessage.content[11] = (int)topAccumulatedScores[1].accumScore;
                        statusMessage.content[12] = (int)topAccumulatedScores[2].accumScore;
                        statusMessage.content[13] = (int)topAccumulatedScores[3].accumScore;
                        statusMessage.content[14] = (int)topAccumulatedScores[4].accumScore;

                        statusMessage.content[15] = predictionResult.predictedKeyID;
                        statusMessage.content[16] = predictionResult.predictWinCount;
                        statusMessage.content[17] = predictionResult.selectedKeyID;
                        statusMessage.content[18] = predictionResult.attendedKeyID;
                        statusMessage.content[19] = predictionResult.decision;
                        statusMessage.content[20] = predictionResult.accuracy;
                    }

                    // Send prediction status message back to main thread
                    statusMsgBufferBlock.Post(statusMessage);
                }

                if (reportErrorStatus == true)
                {
                    // Separated error status from prediction status in order to report error as soon as it occurred.
                    reportErrorStatus = false;

                    // Send error message back to prediction state engine.
                    statusMessage.content[0] = (int)messageID;  // Message ID
                    statusMessage.content[1] = messageLength;         // Message length
                    statusMessage.content[2] = phaseInSeq;
                    statusMessage.content[3] = stimulusCode;
                    statusMessage.content[4] = reportedAction;

                    statusMsgBufferBlock.Post(statusMessage);
                }

                if (logTestData == true)
                {
                    // Send numerical data (in ASCII format?) back to prediction state engine...
                }

                /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

                // Send signal to ReadFileDataAsync() to send next packet of sensor data if the 
                // transmitMethod = PullMethod, and running Prediction code as an offline task
                // reading sensor packet data from pre-collected Calibration .bin files.
                // In normal Prediction mode the headset pushes data thru processing pipeline.
                if (transmitMethod == TransmitMethod.PullMethod)
                {
                    _signal.Release();
                }

            }  // while (!cts.IsCancellationRequested)

        }  // PredictionControlAsync()
    }
}