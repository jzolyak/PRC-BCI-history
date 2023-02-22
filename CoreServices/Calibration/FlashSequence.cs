using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Calibration
{

    public struct FlashTableEntry
    {
        public FlashTableEntry(int sN, int mP, int t, int[] keys)
        {
            sequenceNum = sN;
            matrixPosition = mP;
            target = t;
            keyIDgrouping = keys;
        }
        public int sequenceNum;
        public int matrixPosition;
        public int target;
        public int[] keyIDgrouping;
    }
    public class FlashSequence
    {
        public const int MAX_KEYS_PER_FLASH = 10;

        public const int NOT_ASSIGNED = 0;
        public const int HIDDEN1_ROW = 1;
        public const int HIDDEN2_ROW = 2;
        public const int HIDDEN1_COL = 3;
        public const int HIDDEN2_COL = 4;

        public const int TARGET_GROUP = 1;
        public const int NONTARGET_GROUP = 0;

       

        public List<FlashTableEntry> GenerateFlashSequenceTable(int[,,] hiddenMatrix, int matrixSize1, int matrixSize2, int targetKeyIDValue)
        {

            int sequenceNum;
            int matrixPosition;
            int target;
            int keyIDvalue;
            int row;
            int col;
            int j;

            List<FlashTableEntry> flashSequenceTable = new List<FlashTableEntry>();

            // BASIC TERMINOLOGY:
            //
            //   Key = Is any selectable GUI element rendered on the display. A button is a selectable element, a textbox is not.
            //         All the GUI elements are fetched and stored in a list, and that list is filtered to extract selectable elements.
            //   KeyID = 
            //   Key Grouping =
            //   Flash Iteration =
            //   Flash Sequence =
            //   targetKeyIDValue = 
            //

            // Create a flash table entry for each row in Hidden matrix 1 consisting of all the nonzero keyIDs in that row.
            sequenceNum = 0;
            matrixPosition = HIDDEN1_ROW;

            for (row = 0; row < matrixSize1; row++)
            {
                // 
                sequenceNum++;
                target = NONTARGET_GROUP;
                int[] keys = new int[MAX_KEYS_PER_FLASH];
                FlashTableEntry entry = new FlashTableEntry(sequenceNum, matrixPosition, target, keys);

                j = 0;
                for (col = 0; col < matrixSize1; col++)
                {
                    keyIDvalue = hiddenMatrix[row, col, 0];

                    if (keyIDvalue != 0)
                    {
                        entry.keyIDgrouping[j++] = keyIDvalue;

                        if (keyIDvalue == targetKeyIDValue)
                        {
                            entry.target = TARGET_GROUP;
                        }
                    }
                }
                flashSequenceTable.Add(entry);
            }

            /* ******** */

            // Create a flash table entry for each row in Hidden matrix 2 consisting of all the nonzero keyIDs in that row.
            sequenceNum = matrixSize1;
            matrixPosition = HIDDEN2_ROW;

            for (row = 0; row < matrixSize2; row++)
            {
                // 
                sequenceNum++;
                target = NONTARGET_GROUP;
                int[] keys = new int[MAX_KEYS_PER_FLASH];
                FlashTableEntry entry = new FlashTableEntry(sequenceNum, matrixPosition, target, keys);

                j = 0;
                for (col = 0; col < matrixSize2; col++)
                {
                    keyIDvalue = hiddenMatrix[row, col, 1];

                    if (keyIDvalue != 0)
                    {
                        entry.keyIDgrouping[j++] = keyIDvalue;

                        if (keyIDvalue == targetKeyIDValue)
                        {
                            entry.target = TARGET_GROUP;
                        }
                    }
                }
                flashSequenceTable.Add(entry);
            }

            /* ******** */

            // Create a flash table entry for each column in Hidden matrix 1 consisting of all the nonzero keyIDs in that column.
            sequenceNum = matrixSize1 + matrixSize2;
            matrixPosition = HIDDEN1_COL;

            for (col = 0; col < matrixSize1; col++)
            {
                // 
                sequenceNum++;
                target = NONTARGET_GROUP;
                int[] keys = new int[MAX_KEYS_PER_FLASH];
                FlashTableEntry entry = new FlashTableEntry(sequenceNum, matrixPosition, target, keys);

                j = 0;
                for (row = 0; row < matrixSize1; row++)
                {
                    keyIDvalue = hiddenMatrix[row, col, 0];

                    if (keyIDvalue != 0)
                    {
                        entry.keyIDgrouping[j++] = keyIDvalue;

                        if (keyIDvalue == targetKeyIDValue)
                        {
                            entry.target = TARGET_GROUP;
                        }
                    }
                }
                flashSequenceTable.Add(entry);
            }

            /* ******** */

            // Create a flash table entry for each column in Hidden matrix 2 consisting of all the nonzero keyIDs in that column.
            sequenceNum = matrixSize1 + matrixSize2 + matrixSize1;
            matrixPosition = HIDDEN2_COL;

            for (col = 0; col < matrixSize2; col++)
            {
                // 
                sequenceNum++;
                target = NONTARGET_GROUP;
                int[] keys = new int[MAX_KEYS_PER_FLASH];
                FlashTableEntry entry = new FlashTableEntry(sequenceNum, matrixPosition, target, keys);

                j = 0;
                for (row = 0; row < matrixSize2; row++)
                {
                    keyIDvalue = hiddenMatrix[row, col, 1];

                    if (keyIDvalue != 0)
                    {
                        entry.keyIDgrouping[j++] = keyIDvalue;

                        if (keyIDvalue == targetKeyIDValue)
                        {
                            entry.target = TARGET_GROUP;
                        }
                    }
                }
                flashSequenceTable.Add(entry);
            }

            return flashSequenceTable;

        }  // UpdateSequence()

    }
}
