using CoreServices.SensorData;
using CoreServices.Stimulus;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CoreServices.Prediction
{
    public class PredictionAlgorithm
    {
        // Want to track how scoring changes for top KeyID or StimCode over time
        public const int MAX_NUM_TOP_SCORES = 5;


        public struct TopScoreEntry
        {                                  // Record used to create array of top accumulated scores
            public int indexValue;         // Either stim code value or keyID value
            public double accumScore;      // accumulated sensor score at that index value

            public TopScoreEntry()
            {
                indexValue = 0;
                accumScore = 0.0;
            }
        }

        public struct IntersectPoint
        {
            public int intersectKeyID;
            public int stimCodeIndex1;
            public int stimCodeIndex2;
            public double stimCodeScore1;
            public double stimCodeScore2;

            public IntersectPoint()
            {
                // Default values indicate no intersecting point found
                intersectKeyID = 0;
                stimCodeIndex1 = 0;
                stimCodeIndex2 = 0;
                stimCodeScore1 = 0.0;
                stimCodeScore2 = 0.0;
            }
        }

        public struct PredictionResult
        {
            public int flashSequenceCount;
            public int stimCodeIndex1;
            public int stimCodeIndex2;
            public int truncatedStimScore1;
            public int truncatedStimScore2;
            public int acceptSensorData;        // Accept sensor data = 1, Reject data = 0
            public int candidatetKeyID;         // keyID with highest accum score
            public int predictedKeyID;          // keyID with the highest win total thus far
            public int predictWinCount;         // # times keyID was predicted winner of flash sequence
            public int attendedKeyID;           // Key user is watching during Calib or Valid
            public int selectedKeyID;           // keyID that is selected at and of stopping method

            public PredictionResult()
            {
                flashSequenceCount = 0;
                stimCodeIndex1 = 0;
                stimCodeIndex2 = 0;
                truncatedStimScore1 = 0;
                truncatedStimScore2 = 0;
                acceptSensorData = 1;           // Mark sensor data as always accepted until create test
                candidatetKeyID = 0;
                predictedKeyID = 0;
                predictWinCount = 0;
                attendedKeyID = 0;
                selectedKeyID = 0;
            }
        }

        public struct WinCountEntry
        {
            public int keyID;          // KeyID that passed intersection and threshold tests
            public int winCount;       // How many times the keyID was predicted as the winner

            public WinCountEntry()
            {
                keyID = 0;              // Init value to no keyID found that meets predition criteria
                winCount = 0;
            }
        }

        //public WinCountEntry topWinner;

        public int[] predictedtWinCount = new int[PredictionScoring.MAX_NUM_KEYIDS+1];


        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public TopScoreEntry[] FindTopAccumulatedScores(double[] accumulatedScores)
        {
            int numScores = accumulatedScores.Length;

            int tempIndex;
            double tempScore;

            TopScoreEntry[] topAccumScores = new TopScoreEntry[MAX_NUM_TOP_SCORES];

            for (int index = 0; index < numScores; index++)
            {
                // Check if next score is greater than lowest value in the list of top scores.
                if (accumulatedScores[index] > topAccumScores[MAX_NUM_TOP_SCORES - 1].accumScore)
                {
                    // Replace bottom score with new score and then perform ascending sort
                    topAccumScores[MAX_NUM_TOP_SCORES - 1].accumScore = accumulatedScores[index];
                    topAccumScores[MAX_NUM_TOP_SCORES - 1].indexValue = index;

                    for (int i = (MAX_NUM_TOP_SCORES - 1); i > 0; i--)
                    {
                        if (topAccumScores[i].accumScore > topAccumScores[i-1].accumScore)
                        {
                            // Swap entries moving the higher score up to first element in the array
                            tempScore = topAccumScores[i - 1].accumScore;
                            tempIndex = topAccumScores[i - 1].indexValue;
                            topAccumScores[i - 1].accumScore = topAccumScores[i].accumScore;
                            topAccumScores[i - 1].indexValue = topAccumScores[i].indexValue;
                            topAccumScores[i].accumScore = tempScore;
                            topAccumScores[i].indexValue = tempIndex;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return topAccumScores;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int FindIntersectingKeyId(StimGroupEntry[] stimulusGroupingTable, int stimCodeIndex1, int stimCodeIndex2)
        {
            int keyId1;
            int keyId2;

            int maxKeyIdPerGroup = SensorConstants.NUM_TAG_MARKERS;
            int interesctingKeyId = 0;

            for (int i = 0; i < maxKeyIdPerGroup; i++)
            {
                keyId1 = stimulusGroupingTable[stimCodeIndex1].keyIDs[i];

                for (int j = 0; j < maxKeyIdPerGroup; j++)
                {
                    keyId2 = stimulusGroupingTable[stimCodeIndex2].keyIDs[j];

                    // if not reached end of either grouping before hit end of keyIDs[] array
                    if ((keyId1 != 0) & (keyId2 !=0))
                    {
                        // Test if stimulus groupings contain a common keyID value
                        if (keyId1 == keyId2)
                        {
                            interesctingKeyId = keyId1;
                            break;
                        }
                    }
                }

                if (interesctingKeyId != 0)
                {
                    // Exit search when find a contain a common keyID value
                    break;
                }
            }

            return interesctingKeyId;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public IntersectPoint VerifyIntersectionRules(StimGroupEntry[] stimulusGroupingTable, TopScoreEntry[] topAccumScores, 
                                                        int highestTopScores)
        {
            // Default constructor is set to no intersecting point
            IntersectPoint intersectingPoint = new IntersectPoint();

            // Need to make this into a preset value having a value of 3 or 4. Don't need to
            // search for interest among all top 5 scores because intersections between top score 4
            // and top score 5 yield poor results. Limit search to between top 3 or 4 scores.
            // const int HIGHEST_TOP_SCORES = 4;

            const int MATRIX1_ROW_COL_SUM = FlashSequence.HIDDEN1_ROW + FlashSequence.HIDDEN1_COL;    // equals 4
            const int MATRIX2_ROW_COL_SUM = FlashSequence.HIDDEN2_ROW + FlashSequence.HIDDEN2_COL;    // equals 60

            int stimCodeIndex1;
            int stimCodeIndex2;
            int matrixPosCode1;
            int matrixPosCode2;
            int sumTest;

            int interesctingKeyId = 0;

            double stimCodeScore1;
            double stimCodeScore2;

            for (int i = 0; i < highestTopScores; i++)
            {
                stimCodeIndex1 = topAccumScores[i].indexValue;
                stimCodeScore1 = topAccumScores[i].accumScore;

                matrixPosCode1 = stimulusGroupingTable[stimCodeIndex1].matrixPosCode;

                for (int j = (i + 1); j < highestTopScores; j++)
                {
                    stimCodeIndex2 = topAccumScores[j].indexValue;
                    stimCodeScore2 = topAccumScores[j].accumScore;

                    matrixPosCode2 = stimulusGroupingTable[stimCodeIndex2].matrixPosCode;

                    // First, verify key rule that a valid intersecting point is found if one stimulus
                    // code is from the "rows", and the other stimulus code is from the "columns" from
                    // the same hidden matrix.
                    sumTest = matrixPosCode1 + matrixPosCode2;

                    if ((sumTest == MATRIX1_ROW_COL_SUM) | (sumTest == MATRIX2_ROW_COL_SUM))
                    {
                        interesctingKeyId = FindIntersectingKeyId(stimulusGroupingTable, stimCodeIndex1, stimCodeIndex2);

                        if (interesctingKeyId != 0)
                        {
                            // Intersection found
                            intersectingPoint.intersectKeyID = interesctingKeyId;
                            intersectingPoint.stimCodeIndex1 = stimCodeIndex1;
                            intersectingPoint.stimCodeIndex2 = stimCodeIndex2;
                            intersectingPoint.stimCodeScore1 = stimCodeScore1;
                            intersectingPoint.stimCodeScore2 = stimCodeScore2;
                            break;
                        }
                    }
                }

                if (interesctingKeyId !=0)
                {
                    // Done searching
                    break;
                }
            }

            return intersectingPoint;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public PredictionResult StimCodePredictionAlgorithm(StimGroupEntry[] stimulusGroupingTable, 
                                                            TopScoreEntry[] topAccumulatedScores, 
                                                            double predictScoreThreshold, int highestTopScores)
        {
            IntersectPoint intersectPoint = new IntersectPoint();
            PredictionResult predictionResult = new PredictionResult();

            int candidateKeyID;


            // Step #1:
            //   -  Search thru pair combinations of stimulus codes with the highest accumulated scores.
            //   -  One stim code must come from a "row" assignment, and the other stim code from a "column"
            //      assignment from the same hidden matrix the stimulus groupings were created from.
            //   -  Verify that there is a common keyID value for both stim code key groupings.
            //   -  The function returns a non-zero keyID value if an intersecting (common) keyID is found.

            intersectPoint = VerifyIntersectionRules(stimulusGroupingTable, topAccumulatedScores, highestTopScores);

            // Use intersecting point as the candidate keyID for prediction for this flash sequence.
            candidateKeyID = intersectPoint.intersectKeyID;

            if (candidateKeyID != 0)
            {
                //if ((intersectPoint.stimCodeScore1 > predictScoreThreshold) && 
                //    (intersectPoint.stimCodeScore2 > predictScoreThreshold))

                // Quick improvement patch:  02-14-2025, JAZ
                // Prof. Huggins and I observed that the accumulated scores were lower when using a 45 key sized
                // keyboard, or when there were less than 9 active keys. The accumulated score for the second stim
                // code had a hard time reaching and staying above the predictScoreThreshold. Since stim codes 1 and 2
                // have a common intersect point and are both in the top 4 accumulated scores, we let stim code 1
                // help to pull up stim code 2 by averaging them together. Chose this method over lowering the
                // value of predictScoreThreshold. This needs to be examined further when there is more time.
                //
                if (((intersectPoint.stimCodeScore1 + intersectPoint.stimCodeScore2)/2.0) > predictScoreThreshold)
                {
                    // The signal strength is considered sufficiently high enough to contain the evoked repsonse.
                    // Update predicted win counts for candidateKeyID keyID.
                    predictedtWinCount[candidateKeyID] += 1;

                    predictionResult.stimCodeIndex1 = intersectPoint.stimCodeIndex1;
                    predictionResult.stimCodeIndex2 = intersectPoint.stimCodeIndex2;
                    predictionResult.truncatedStimScore1 = (int)intersectPoint.stimCodeScore1;
                    predictionResult.truncatedStimScore2 = (int)intersectPoint.stimCodeScore2;
                    predictionResult.candidatetKeyID = candidateKeyID;      // Intersect point is candidate
                    predictionResult.predictedKeyID = candidateKeyID;       // Candidate meets threshold requirements
                    predictionResult.predictWinCount = predictedtWinCount[candidateKeyID];  // # times keyID predicted as winner
                }
                else
                {
                    // candidate keyID's accumulated scores where not high enough to be confident of correct prediction.
                    // Use keyID = 0 to keep track of number of times the prediction criteria was not met.
                    predictedtWinCount[0] += 1;

                    predictionResult.stimCodeIndex1 = intersectPoint.stimCodeIndex1;
                    predictionResult.stimCodeIndex2 = intersectPoint.stimCodeIndex2;
                    predictionResult.truncatedStimScore1 = (int)intersectPoint.stimCodeScore1;
                    predictionResult.truncatedStimScore2 = (int)intersectPoint.stimCodeScore2;

                    // Indicate that we passed intersect test, but that signal strength does not
                    // have high enough confidence to be true winner yet.
                    predictionResult.candidatetKeyID = candidateKeyID;
                    predictionResult.predictedKeyID = candidateKeyID;
                    predictionResult.predictWinCount = predictedtWinCount[candidateKeyID];  // Report unincremented win count
                }
            }
            else
            {
                // No valid interesction point to even consider as candidate keyID.
                // Return status report indicating that no prediction was made this flash sequence.
                // Use keyID = 0 to keep track of number of times the prediction criteria was not met.
                predictedtWinCount[0] += 1;

                predictionResult.stimCodeIndex1 = 0;
                predictionResult.stimCodeIndex2 = 0;
                predictionResult.truncatedStimScore1 = 0;
                predictionResult.truncatedStimScore2 = 0;
                predictionResult.candidatetKeyID = 0;       // No intersect point
                predictionResult.predictedKeyID = 0;        // No predicted winner
                predictionResult.predictWinCount = predictedtWinCount[0];   // Report # times failed to detect winner
            }

            return predictionResult;
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public PredictionResult KeyIdPredictionAlgorithm(TopScoreEntry[] topAccumulatedScores,
                                                        double predictScoreThreshold)
        {
            // OVERVIEW:
            //
            //   The steps followed in this algorithm attempt to replicate the functionality of the 
            //   original BCI prediction method. At the end of each flash sequence, each key on the
            //   displayed keyboard has been flashed twice, i.e. analagous to randomly flashing each 
            //   row and column of the keyboard once. {Fill in the rest of the details later...}
            //
            // NOTE:
            //   This algorithm *IS NOT* the one used in the PRC product. It serves as a baseline
            //   reference point to illustrate the limitations of the original BCI prediction
            //   method and how they were corrected in the StimCodePredictionAlgorithm.
            //
            // Step #1:
            //   -  Convert the combined weighted sensor scores from each stimulus codes 
            //      into a combined weighted scored for each keyID. This was done in
            //      predictionScoring.ConvertStimCodeToKeyIdScores().
            //

            // Basically the candidate keyID selected by the prediction algorithm is the keyID with
            // the highest score in the topAccumulatedScores[] array. In Fixed Sequence stopping,
            // prediction is halted after a preset number of flash sequenced (10 to 15) has occured.
            // 
            // I have added two simple variations to the algorithm that can be enabled or ignored.
            //

            int candidateKeyID;

            double candidateScore;

            PredictionResult predictionResult = new PredictionResult();

            // Use keyID with top accumulated score as the candidate for prediction for this flash sequence.
            candidateKeyID = topAccumulatedScores[0].indexValue;
            candidateScore = topAccumulatedScores[0].accumScore;

            // The original BCI prediction method did not have a confidence threshold test or kept track of win counts.
            // To disable this test, simply assign a preset value of 0.0 to this threshold so it always passes test.
            if (candidateScore > predictScoreThreshold)
            {
                // Update predicted win counts for candidateKeyID.
                predictedtWinCount[candidateKeyID] += 1;

                // Report top 2 pair of (keyID + accum scores) to watch how it changes over time.
                predictionResult.stimCodeIndex1 = topAccumulatedScores[0].indexValue;
                predictionResult.stimCodeIndex2 = topAccumulatedScores[1].indexValue;
                predictionResult.truncatedStimScore1 = (int)topAccumulatedScores[0].accumScore;
                predictionResult.truncatedStimScore2 = (int)topAccumulatedScores[1].accumScore;
                predictionResult.candidatetKeyID = candidateKeyID;      // Top accum score is candidate
                predictionResult.predictedKeyID = candidateKeyID;       // Candidate meets threshold requirements
                predictionResult.predictWinCount = predictedtWinCount[candidateKeyID];  // # times keyID predicted as winner
            }
            else
            {
                // Return status report indicating that no prediction was made this flash sequence.
                // Use keyID = 0 to keep track of number of times the prediction criteria was not met.
                predictedtWinCount[0] += 1;

                // Report top 2 pair of (keyID + accum scores) to watch how it changes over time.
                predictionResult.stimCodeIndex1 = topAccumulatedScores[0].indexValue;
                predictionResult.stimCodeIndex2 = topAccumulatedScores[1].indexValue;
                predictionResult.truncatedStimScore1 = (int)topAccumulatedScores[0].accumScore;
                predictionResult.truncatedStimScore2 = (int)topAccumulatedScores[1].accumScore;

                predictionResult.candidatetKeyID = candidateKeyID;  // Top accum score is candidate
                predictionResult.predictedKeyID = 0;                // No predicted winner
                predictionResult.predictWinCount = predictedtWinCount[0];   // Report # times failed to detect winner
            }

            return predictionResult;

        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
