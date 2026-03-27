using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static CoreServices.Prediction.PredictionControl;

namespace CoreServices.Prediction
{
    public class PredicitionReport
    {
        // For now, limit the maximum size of the report.
        // Convert static array into dynamic lists later on.
        public const int MAX_NUM_FLASH_SEQ = 15;
        public const int MAX_NUM_PREDICT_TARGETS = 30;

        public struct ReportedResults
        {
            public int predictedKeyID;          // keyID with the highest win total thus far
            public int attendedKeyID;           // Key user is watching during Calib or Valid
            public int selectedKeyID;           // keyID that is selected at and of stopping method
            public int decision;                // Enumerated integer value for EndDecision
            public int accuracy;                // Enumerated integer value for PredictionAccuracy

            public ReportedResults()
            {
                predictedKeyID = 0;
                attendedKeyID = 0;
                selectedKeyID = 0;
                decision = 0;                   // STATUS_UPDATE_MSG
                accuracy = 0;                   // NO_TARGET
            }
        }

        public struct DataSetInfo
        {
            public int currentSetNum;
            public int totalSetNum;
            public int startTargetNum;
            public int endTartgetNum;

            public DataSetInfo()
            {
                currentSetNum = 0;
                totalSetNum = 0;
                startTargetNum = 0;
                endTartgetNum = 0;
            }
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public void GenerateFlashSeqAccuracyReport(ReportedResults[,] reportedResults, int maxNumFlashSeq, int maxNumTargets, StreamWriter sw)
        {
            int keyID;
            int seqNum;
            int target;

            int numAccuratePredictions;

            float combinedFlashSeqAccuracy;

            string attendString;

            // Print column report headers here...
            // Logger.Info("Flash Seq  Correct %    Predicted key ID \n");
            sw.Write("Flash Seq  Correct %    Predicted key ID \n\n");


            for (seqNum = 0; seqNum < maxNumFlashSeq; seqNum++)
            {
                string keyIDString;
                string seqNumString;
                string accuracyString;
                string reportString;

                numAccuratePredictions = 0;
                keyIDString = "";

                for (target = 0; target < maxNumTargets; target++)
                {
                    if ((reportedResults[seqNum, target].accuracy == (int)PredictionAccuracy.GOOD_SELECT) || 
                        (reportedResults[seqNum, target].accuracy == (int)PredictionAccuracy.GOOD_CHOICE))
                    {
                        // Increment number of accurate predictions if predicted keyID matched attended keyID
                        numAccuratePredictions++;
                    }

                    // Construct a string of all the predicted keyIDs at a specific flash sequence across all targets .
                    keyID = reportedResults[seqNum, target].predictedKeyID;
                    keyIDString += string.Format("{0,5:D}", keyID);
                }

                combinedFlashSeqAccuracy = (float)((numAccuratePredictions * 100) / maxNumTargets);
                accuracyString = string.Format("{0,12:F2}", combinedFlashSeqAccuracy);

                seqNumString = string.Format("{0,6:D}", seqNum);
                reportString = seqNumString + accuracyString + "   " + keyIDString;

                // Logger.Info(reportString + "\n");
                sw.Write(reportString + "\n");
            }

            // NOTE:  Don't forget to print the line containing the string containing the list of attended keyIDs.
            seqNum = 0;
            attendString = "";

            for (target = 0; target < maxNumTargets; target++)
            {
                // Construct a string of all the attended keyIDs across all targets.
                keyID = reportedResults[seqNum, target].attendedKeyID;
                attendString += string.Format("{0,5:D}", keyID);
            }

            // Logger.Info("   Attended keyIDs:  " + attendString + "\n");
            sw.Write("\n   Attended keyIDs:  " + attendString + "\n");
        }

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        /*
        public void GenerateDataSetAccurcyReport(ReportedResults[,] reportedResults, int maxNumFlashSeq, int maxNumTargets, StreamWriter sw)
        {
            // Get number of data set *.bin files that were analyzed
            int numDataSets = 5;

            for (int i = 0; i < numDataSets; i++)
            {
                // Print current set number and .bin file name

                // Print report column headers for data set

                for (int j = 0; j < maxNumFlashSeq; j++)
                {


                    for (int k = 0; k < endTargetNum; k++)
                    {
                        // Fetch predicted keyID and decision for each target in the set

                    }
                }

            }
        }
        */

        /* +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

    }
}
