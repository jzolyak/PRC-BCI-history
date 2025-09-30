using CoreServices.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using CoreServices.FlashControl;

namespace CoreServices.Stimulus
{
    public struct GroupingParam
    {
        public Method method;
        public KeyOrder keyOrder;
        public BoardDivision boardDivision;
        public Arrangement arrangement;
        public IndexMapping indexMapping;

        GroupingParam(Method m, KeyOrder k, BoardDivision b, Arrangement a, IndexMapping i)
        {
            method = m;
            keyOrder = k;
            boardDivision = b;
            arrangement = a;
            indexMapping = i;
        }
    }

    public enum Method { RowColumn = 0, NewMethod = 1 };
    public enum KeyOrder { KeyID = 0, Switchback = 1, Diagonal = 2, DiagonalSwitchback = 3, AlternateColumns = 4, Manhattan = 5 };
    public enum BoardDivision { FirstSecond = 0, OddEven = 1, Checkerboard = 2 };
    public enum Arrangement { Magic = 0, Knight = 1, LeadDiagonal = 2 };  // GreedyMagic = 3, GreedyKnight = 4, GreedyLeadDiagonal = 5, None = 6
    public enum IndexMapping { NuVoice = 0, GridSize = 1 };

    public class FlashGrouping
    {
        public const int ACTIVE_KEYS_LOWER_LIMIT = 8;

        //int[] randomList1 = new int[ACTIVE_KEYS_LOWER_LIMIT] { 5, 2, 0, 3, 7, 1, 4, 6 };
        //int[] randomList2 = new int[ACTIVE_KEYS_LOWER_LIMIT] { 4, 2, 7, 0, 5, 3, 6, 1 };

        // Two 4x4 hidden matrices have 8 diagonal elements
        //int[] random8KeyList1 = new int[] { 5, 2, 0, 3, 7, 1, 4, 6 };
        //int[] random8KeyList2 = new int[] { 4, 2, 7, 0, 5, 3, 6, 1 };

        // Two 3x3 hidden matrices have 6 diagonal elements
        //int[] random6KeyList1 = new int[] { 4, 0, 3, 1, 5, 2 };
        //int[] random6KeyList2 = new int[] { 3, 5, 2, 0, 4, 1 };       

        public struct InputGrid
        {
            public int numRows;
            public int numCols;
        }

        // Constructor
        public FlashGrouping()
        { }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int[,,] GenerateAlternateGroupingMaxtrix(List<KeyMapping> flashableKeys,
                                                out int hiddenSize1, out int hiddenSize2)
        {

            int i, keyID;
            int index1, index2;
            int numDiagonalKeys;

            // Determine number of active keys on keyboard. It should be <= ACTIVE_KEYS_LOWER_LIMIT.
            int numKeys = flashableKeys.Count;

            if (numKeys <= (ACTIVE_KEYS_LOWER_LIMIT - 2))
            {
                // Six or fewer keys fit into the diagonals of two 3x3 matrices
                hiddenSize1 = 3;
                hiddenSize2 = 3;
                numDiagonalKeys = 6;
            }
            else
            {
                // 7 or 8 keys fit into the diagonals of two 4x4 matrices
                hiddenSize1 = 4;
                hiddenSize2 = 4;
                numDiagonalKeys = 8;
            }

            int[,,] hiddenMatrix = new int[hiddenSize1, hiddenSize2, 2];

            FlashOrder flashOrder = new FlashOrder();

            // Allocate an array of integers having a non duplicate range of values
            // between 0 and num-1, and randomize the array contents. For example, if
            // num = 8, then a possible output for randomList1 = { 5, 2, 0, 3, 7, 1, 4, 6 }
            int[] randomList1 = flashOrder.CreateRandomizedArray(numDiagonalKeys);
            int[] randomList2 = flashOrder.CreateRandomizedArray(numDiagonalKeys);

            // Reserve capacity for storing the maximum number of active keys handled by this algorithm.
            // It essentially creates a zero padded array when there are fewer active keys.
            int[] activeKeys = new int[numDiagonalKeys];

            // Extract keyIndex values from list into separate array.
            for (i = 0; i < numKeys; i++)
            {
                activeKeys[i] = flashableKeys[i].keyID;
            }

            //flashOrder.RandomizeIndexes(ref randomList1);
            //flashOrder.RandomizeIndexes(ref randomList2);

            for (i = 0; i < numDiagonalKeys; i++)
            {
                index1 = randomList1[i];
                index2 = randomList2[i];

                // Use the first random list to select a keyID from the list of active keys
                keyID = activeKeys[index1];

                // Use the the second random list to select location on diagonal of hidden matrix
                if (index2 < hiddenSize1)
                {
                    // Store the keyID on the diagonal of first hidden matrix
                    hiddenMatrix[index2, index2, 0] = keyID;
                }
                else
                {
                    // Store the keyID on the diagonal of second hidden matrix
                    index2 = index2 - hiddenSize1;
                    hiddenMatrix[index2, index2, 1] = keyID;
                }
            }

            return hiddenMatrix;
        }


        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int[,,] GenerateGroupingMaxtrix(GroupingParam inputParam, int[,] keyIDMap, List<KeyMapping> flashableKeys,
                                                GridLayout gridData, int randomValue, out int hiddenSize1, out int hiddenSize2)
        {
            // Identifier mapping applied to keyboard to indicate which key is selected
            int[,] keyOrder;

            List<int> firstKeyOrder = new List<int>();
            List<int> firstKeyIDs = new List<int>();
            List<int> secondKeyOrder = new List<int>();
            List<int> secondKeyIDs = new List<int>();

            int[,,] hiddenMatrix;

            int i, j;
            int key, value;
            int hiddenCapacity1;
            int hiddenCapacity2;
            int length1;
            int length2;
            int placeKey, placeRow, placeCol;

            // Determine number of active keys on keyboard
            int numKeys = flashableKeys.Count;

            // Extract keyIndex values from list into separate array for quicker processing
            int[] activeKeys = new int[numKeys];
            for (i = 0; i < numKeys; i++)
            {
                activeKeys[i] = flashableKeys[i].keyID;
            }

            // Sorting key by their IDs
            Array.Sort(activeKeys);

            InputGrid gridSize;
            gridSize.numRows = gridData.maxKeyRows;
            gridSize.numCols = gridData.maxKeyCols;

            // Determine the order of position in which to place keys in the hidden matrices
            CreatePlaceOrder(inputParam.arrangement, numKeys, randomValue, out int[,] placeOrder1, out int[,] placeOrder2);

            keyOrder = CreateKeyOrder(gridSize, inputParam.keyOrder);

            DivideBoard(inputParam.boardDivision, gridSize, activeKeys, keyIDMap, keyOrder,
                out List<int> firstKeyOrderVector, out List<int> firstKeyIDsVector,
                out List<int> secondKeyOrderVector, out List<int> secondKeyIDsVector);

            // NOTE #1: The capacity of the first hidden matrix output from CreatePlaceOrder()
            // will always be equal to or greater than the capacity of the second hidden matrix.
            hiddenCapacity1 = placeOrder1.GetLength(0);
            hiddenCapacity2 = placeOrder2.GetLength(0);

            hiddenSize1 = (int)Math.Ceiling(Math.Sqrt(hiddenCapacity1));
            hiddenSize2 = (int)Math.Ceiling(Math.Sqrt(hiddenCapacity2));

            hiddenMatrix = new int[hiddenSize1, hiddenSize1, 2];

            // As a result of NOTE #1, I took a divergent path when converting MATLAB code to C#.
            // Only need to worry about when DivideBoard() the assigns more active keys to the
            // second hidden matrix than the first. In that case, need to swap the output of the
            // board division so that the most active keys are mapped to the first hidden matrix.
            length1 = firstKeyIDsVector.Count;
            length2 = secondKeyIDsVector.Count;

            if (length1 < length2)
            {
                firstKeyOrder.AddRange(secondKeyOrderVector);
                firstKeyIDs.AddRange(secondKeyIDsVector);
                secondKeyOrder.AddRange(firstKeyOrderVector);
                secondKeyIDs.AddRange(firstKeyIDsVector);
            }
            else
            {
                firstKeyOrder.AddRange(firstKeyOrderVector);
                firstKeyIDs.AddRange(firstKeyIDsVector);
                secondKeyOrder.AddRange(secondKeyOrderVector);
                secondKeyIDs.AddRange(secondKeyIDsVector);
            }

            // Balance key lists to make sure the hidden matrices have enough space for all keys.
            // Check if balancing is required. If more keys ended up in the first list
            // than the capacity of the first hidden matrix, or if more keys ended up in
            // the second list than the capacity of the second hidden matrix, then call
            // the balancing algorithm to take care of the issue.
            if ((firstKeyIDs.Count > hiddenCapacity1) || (secondKeyIDs.Count > hiddenCapacity2))
            {
                // MAKE CERTAIN THAT firstKeyIDs.Length MATCHES LONGER LIST AFTER SWAP REFERENCES
                BalanceKeySets(hiddenCapacity1, hiddenCapacity2,
                               ref firstKeyOrder, ref firstKeyIDs,
                               ref secondKeyOrder, ref secondKeyIDs);
            }

            // NOTE:  The greedy version of these algorithms are not required for minimum viable product.
            //        (In JAZ's opinion, they are way overkill in finding optimal adjacency placement).
            //
            // if ( (arrangement == Arrangement.GreedyMagic) || (arrangement == Arrangement.GreedyLeadDiagonal))
            //    Create hidden matrices using greedy algorithm placement
            // else
            //    Create hidden matrices without greedy algorithm placement

            // Create vector of ordered pairing of [random, sequential] values.
            length1 = firstKeyOrder.Count;

            int[,] firstKeyOrderPairedValues = new int[length1, 2];

            for (i = 0; i < length1; i++)
            {
                firstKeyOrderPairedValues[i, 0] = firstKeyOrder[i];
                firstKeyOrderPairedValues[i, 1] = i;
            }

            // Sort the random numbers in the first column
            for (i = 1; i < length1; i++)
            {
                key = firstKeyOrderPairedValues[i, 0];
                value = firstKeyOrderPairedValues[i, 1];
                j = i - 1;

                // Move elements of array that are greater than key value to one
                // position ahead of their current location.
                while ((j >= 0) && (firstKeyOrderPairedValues[j, 0] > key))
                {
                    firstKeyOrderPairedValues[j + 1, 0] = firstKeyOrderPairedValues[j, 0];
                    firstKeyOrderPairedValues[j + 1, 1] = firstKeyOrderPairedValues[j, 1];
                    j = j - 1;
                }
                firstKeyOrderPairedValues[j + 1, 0] = key;
                firstKeyOrderPairedValues[j + 1, 1] = value;
            }

            /* ******** */

            // Create vector of ordered pairing of [random, sequential] values.
            length2 = secondKeyOrder.Count;

            int[,] secondKeyOrderPairedValues = new int[length2, 2];

            for (i = 0; i < length2; i++)
            {
                secondKeyOrderPairedValues[i, 0] = secondKeyOrder[i];
                secondKeyOrderPairedValues[i, 1] = i;
            }

            // Sort the random numbers in the first column
            for (i = 1; i < length2; i++)
            {
                key = secondKeyOrderPairedValues[i, 0];
                value = secondKeyOrderPairedValues[i, 1];
                j = i - 1;

                // Move elements of array that are greater than key value to one
                // position ahead of their current location.
                while ((j >= 0) && (secondKeyOrderPairedValues[j, 0] > key))
                {
                    secondKeyOrderPairedValues[j + 1, 0] = secondKeyOrderPairedValues[j, 0];
                    secondKeyOrderPairedValues[j + 1, 1] = secondKeyOrderPairedValues[j, 1];
                    j = j - 1;
                }
                secondKeyOrderPairedValues[j + 1, 0] = key;
                secondKeyOrderPairedValues[j + 1, 1] = value;
            }

            /* ******** */

            // Maintain the order in which the key IDs need to be placed
            // firstKeyOrderOriginalIndices = sort(firstKeyOrder);
            // secondKeyOrderOriginalIndices = sort(secondKeyOrder);
            length1 = firstKeyIDs.Count;

            for (placeKey = 0; placeKey < length1; placeKey++)
            {
                placeRow = placeOrder1[placeKey, 0];
                placeCol = placeOrder1[placeKey, 1];
                hiddenMatrix[placeRow, placeCol, 0] = firstKeyIDs[firstKeyOrderPairedValues[placeKey, 1]];
            }

            length2 = secondKeyIDs.Count;

            for (placeKey = 0; placeKey < length2; placeKey++)
            {
                placeRow = placeOrder2[placeKey, 0];
                placeCol = placeOrder2[placeKey, 1];
                hiddenMatrix[placeRow, placeCol, 1] = secondKeyIDs[secondKeyOrderPairedValues[placeKey, 1]];
            }

            return hiddenMatrix;

        }  // GenerateGroupingMaxtrix()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        void CreatePlaceOrder(Arrangement arrangement, int numKeys, int randomValue, out int[,] placeOrder1List, out int[,] placeOrder2List)
        {
            int halfKeys;
            int hiddenSize1;
            int hiddenSize2;
            int hiddenCapacity1;
            int hiddenCapacity2;

            int row;
            int col;
            int offset;
            int orderValue;
            bool found;

            int[,] placementOrder1;
            int[,] placementOrder2;

            if (arrangement == Arrangement.Magic)  //  or (arrangement == Arrangement.GreedyMagic)
            {
                MagicSquare magic = new MagicSquare();

                // Decide on the max size of the hidden matrices
                // HiddenSize1 is the length of the side of the first hidden square matrix
                // HiddenCapacity1 is the number of keys that can fit in the first hidden matrix
                halfKeys = (int)Math.Ceiling((double)(numKeys / 2.0));
                hiddenSize1 = (int)Math.Ceiling(Math.Sqrt(halfKeys));

                /*
                if ( ((hiddenSize1-1)*(hiddenSize1-1) + hiddenSize1* hiddenSize1) >= numKeys)
                {
                    hiddenSize2 = hiddenSize1 - 1;
                }
                else 
                {
                    hiddenSize2 = hiddenSize1;
                }
                */

                hiddenSize2 = hiddenSize1;

                hiddenCapacity1 = hiddenSize1 * hiddenSize1;
                hiddenCapacity2 = hiddenSize2 * hiddenSize2;

                // Get the magic square table used to fill the hidden matrices
                if (magic.IsValidDimension(hiddenSize1) == false)
                {
                    // Eventually throw an exception, but for now print an error message.
                   Logger.Error(this,"BCI ERROR: Dimension out of range for Magic Square");
                }

                placementOrder1 = magic.GetMagicTable(hiddenSize1);
                placementOrder2 = magic.GetMagicTable(hiddenSize2);

                placeOrder1List = new int[hiddenCapacity1, 2];

                offset = randomValue + 1;

                for (int i = 0; i < hiddenCapacity1; i++)
                {
                    orderValue = i + offset;

                    if (orderValue > hiddenCapacity1)
                    {
                        orderValue = orderValue - hiddenCapacity1;
                    }

                    row = 0;
                    col = 0;
                    found = false;

                    while (found == false)
                    {
                        if (placementOrder1[row, col] == orderValue)
                        {
                            placeOrder1List[i, 0] = row;
                            placeOrder1List[i, 1] = col;
                            found = true;
                        }
                        else
                        {
                            if (++col >= hiddenSize1)
                            {
                                row++;
                                col = 0;
                            }
                        }
                    }
                }

                placeOrder2List = new int[hiddenCapacity2, 2];

                offset = randomValue + 4;

                for (int i = 0; i < hiddenCapacity2; i++)
                {
                    orderValue = i + offset;

                    if (orderValue > hiddenCapacity2)
                    {
                        orderValue = orderValue - hiddenCapacity2;
                    }

                    row = 0;
                    col = 0;
                    found = false;

                    while (found == false)
                    {
                        if (placementOrder2[row, col] == orderValue)
                        {
                            placeOrder2List[i, 0] = row;
                            placeOrder2List[i, 1] = col;
                            found = true;
                        }
                        else
                        {
                            if (++col >= hiddenSize2)
                            {
                                row++;
                                col = 0;
                            }
                        }
                    }
                }
            }

            // else if ((arrangement == Arrangement.Knight) || (arrangement == Arrangement.GreedyKnight))
            // { }

            // else if ((arrangement == Arrangement.LeadDiagonal) || (arrangement == Arrangement.GreedyLeadDiagonal))
            // { }

            else  // (arrangement == Arrangement.None)
            {
                // This is a test mode to be used in conjunction with KeyOrder.KeyID.
                // Keys will end up being assigned to the hidden matrices soley based
                // on boardDivision parameter. No randomization occurs on the order in
                // which the active keys are assigned to the hidden matrices. The following
                // code creates a place order list that maps the top, left active keys from 
                // the keyboard to the top, left (origin) of the each hidden matrix.
                halfKeys = (int)Math.Ceiling((double)(numKeys / 2.0));
                hiddenSize1 = (int)Math.Ceiling(Math.Sqrt(halfKeys));
                hiddenCapacity1 = hiddenSize1 * hiddenSize1;
                hiddenCapacity2 = hiddenCapacity1;

                placeOrder1List = new int[hiddenCapacity1, 2];
                placeOrder2List = new int[hiddenCapacity2, 2];
                int order_value = 0;

                for (row = 0; row < hiddenSize1; row++)
                {
                    for (col = 0; col < hiddenSize1; col++)
                    {
                        placeOrder1List[order_value, 0] = row;
                        placeOrder1List[order_value, 1] = col;
                        placeOrder2List[order_value, 0] = row;
                        placeOrder2List[order_value, 1] = col;
                        order_value++;
                    }
                }
            }

        }  // CreatePlaceOrder()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        int[,] CreateKeyOrder(InputGrid gridSize, KeyOrder order)
        {
            // This function influences when the keys on the grid are flashed relative to time.
            // The output of this function is used to specify the order in which the keys are
            // copied from the original keyboard 1nto the hidden matrices. 

            int[,] keySeq = new int[gridSize.numRows, gridSize.numCols];

            int i, j, k;
            int temp;
            int assignment;

            switch (order)
            {
                case KeyOrder.KeyID:

                    // Example of KeyID order pattern for 3x4 grid:
                    //    1   2   3   4
                    //    5   6   7   8
                    //    9  10  11  12

                    assignment = 1;

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            keySeq[i, j] = assignment++;
                        }
                    }
                    break;

                case KeyOrder.Switchback:

                    // Example of Switchback order pattern for 3x4 grid:
                    //    1   2   3   4
                    //    8   7   6   5
                    //    9  10  11  12

                    assignment = 1;

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            keySeq[i, j] = assignment++;
                        }
                    }

                    for (i = 1; i < gridSize.numRows; i += 2)
                    {
                        j = 0;
                        k = gridSize.numCols - 1;
                        while (j < k)
                        {
                            temp = keySeq[i, j];
                            keySeq[i, j] = keySeq[i, k];
                            keySeq[i, k] = temp;
                            j++;
                            k--;
                        }
                    }
                    break;

                case KeyOrder.Diagonal:

                    // Example of Diagonal order pattern for 3x4 grid:
                    //    1   2   4   7
                    //    3   5   8  10
                    //    6   9  11  12

                    assignment = 1;
                    keySeq[0, 0] = assignment++;

                    for (i = 1; i < gridSize.numCols; i++)
                    {
                        j = 0;
                        k = i;
                        while ((j < gridSize.numRows) && (k >= 0))
                        {
                            keySeq[j, k] = assignment++;
                            j++;
                            k--;
                        }
                    }

                    for (i = 1; i < gridSize.numRows; i++)
                    {
                        j = i;
                        k = gridSize.numCols - 1;
                        while (j < gridSize.numRows)
                        {
                            keySeq[j, k] = assignment++;
                            j++;
                            k--;
                        }
                    }
                    break;

                    /*
                    case KeyOrder.DiagonalSwitchback:
                        break;
                    case KeyOrder.AlternateColumns:
                        break;
                    case KeyOrder.Manhattan:
                        break;
                    */
            }

            return keySeq;

        }  // CreateKeyOrder()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        void DivideBoard(BoardDivision boardDivision, InputGrid gridSize, int[] activeKeys,
                            int[,] keyIndexMap, int[,] keyOrder,
                            out List<int> firstKeyOrderVector, out List<int> firstKeyIDsVector,
                            out List<int> secondKeyOrderVector, out List<int> secondKeyIDsVector)
        {
            int[,] firstMatKeyOrder = new int[gridSize.numRows, gridSize.numCols];
            int[,] secondMatKeyOrder = new int[gridSize.numRows, gridSize.numCols];
            int[,] firstMatKeyIDs = new int[gridSize.numRows, gridSize.numCols];
            int[,] secondMatKeyIDs = new int[gridSize.numRows, gridSize.numCols];

            int i, j;
            int numkeys, numkey1, numkey2;

            int[] board1;
            int[] board2;

            int[,] mask1;
            int[,] mask2;
            int[,] checkerboard;

            numkeys = activeKeys.Length;
            numkey1 = (int)Math.Ceiling((double)(numkeys / 2.0));
            numkey2 = numkeys - numkey1;

            board1 = new int[numkey1];
            board2 = new int[numkey2];

            switch (boardDivision)
            {
                case BoardDivision.FirstSecond:

                    // Split the active keys in half between the two boards
                    j = 0;
                    for (i = 0; i < numkey1; i++)
                    {
                        board1[i] = activeKeys[j];
                        j++;
                    }
                    j = numkey1;
                    for (i = 0; i < numkey2; i++)
                    {
                        board2[i] = activeKeys[j];
                        j++;
                    }

                    mask1 = isMember(gridSize, keyIndexMap, board1);
                    mask2 = isMember(gridSize, keyIndexMap, board2);

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            firstMatKeyOrder[i, j] = keyOrder[i, j] * mask1[i, j];
                            secondMatKeyOrder[i, j] = keyOrder[i, j] * mask2[i, j];
                        }
                    }
                    break;

                case BoardDivision.OddEven:

                    // Split alternating active keys between the two boards
                    // (Note: It has nothing to do with odd/even key index values)
                    j = 0;
                    for (i = 0; i < numkey1; i++)
                    {
                        board1[i] = activeKeys[j];
                        j += 2;
                    }
                    j = 1;
                    for (i = 0; i < numkey2; i++)
                    {
                        board2[i] = activeKeys[j];
                        j += 2;
                    }

                    mask1 = isMember(gridSize, keyIndexMap, board1);
                    mask2 = isMember(gridSize, keyIndexMap, board2);

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            firstMatKeyOrder[i, j] = keyOrder[i, j] * mask1[i, j];
                            secondMatKeyOrder[i, j] = keyOrder[i, j] * mask2[i, j];
                        }
                    }
                    break;

                case BoardDivision.Checkerboard:

                    // For this case, create a single mask and use a checkboard pattern 
                    // to divide all the keys into two separate boards. This pattern
                    // ensures that there are no vertical or horizontal adjacent keys
                    // next to each other in the hidden matrices.
                    ToeplitzMatrix toeplitz = new ToeplitzMatrix();

                    mask1 = isMember(gridSize, keyIndexMap, activeKeys);

                    // Create a checkerboard of 1's and 0's, with the first square starting at 1
                    if (toeplitz.IsValidSize(gridSize.numRows, gridSize.numCols))
                    {
                        checkerboard = toeplitz.GetMatrixTable(gridSize.numRows, gridSize.numCols, 1);
                    }
                    else
                    {
                        // This code is not designed to handle this many selectable items!!!
                        MessagingService.SendMessage(this,"Failure in FlashGrouping.DivideBoard()",MessageType.Popup);
                        // Create zero value array so that no keys will flash.
                        checkerboard = new int[gridSize.numRows, gridSize.numCols];
                    }

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            firstMatKeyOrder[i, j] = keyOrder[i, j] * checkerboard[i, j] * mask1[i, j];
                        }
                    }

                    // Create a checkerboard with the first square starting at 0
                    if (toeplitz.IsValidSize(gridSize.numRows, gridSize.numCols))
                    {
                        checkerboard = toeplitz.GetMatrixTable(gridSize.numRows, gridSize.numCols, 0);
                    }
                    else
                    {
                        // Same error. Create zero value array so that no keys will flash.
                        checkerboard = new int[gridSize.numRows, gridSize.numCols];
                    }

                    for (i = 0; i < gridSize.numRows; i++)
                    {
                        for (j = 0; j < gridSize.numCols; j++)
                        {
                            secondMatKeyOrder[i, j] = keyOrder[i, j] * checkerboard[i, j] * mask1[i, j];
                        }
                    }
                    break;

            }  // switch()

            // Next ...
            for (i = 0; i < gridSize.numRows; i++)
            {
                for (j = 0; j < gridSize.numCols; j++)
                {
                    if (firstMatKeyOrder[i, j] != 0)
                    {
                        firstMatKeyIDs[i, j] = keyIndexMap[i, j];
                    }
                    if (secondMatKeyOrder[i, j] != 0)
                    {
                        secondMatKeyIDs[i, j] = keyIndexMap[i, j];
                    }
                }
            }

            firstKeyOrderVector = new List<int>();
            firstKeyIDsVector = new List<int>();
            secondKeyOrderVector = new List<int>();
            secondKeyIDsVector = new List<int>();

            // Convert each matrix into a variable length list by extracting the active 
            // (nonzero value) keys and copying them to the list.
            for (i = 0; i < gridSize.numRows; i++)
            {
                for (j = 0; j < gridSize.numCols; j++)
                {
                    if (firstMatKeyOrder[i, j] != 0)
                    {
                        firstKeyOrderVector.Add(firstMatKeyOrder[i, j]);
                    }
                    if (firstMatKeyIDs[i, j] != 0)
                    {
                        firstKeyIDsVector.Add(firstMatKeyIDs[i, j]);
                    }
                    if (secondMatKeyOrder[i, j] != 0)
                    {
                        secondKeyOrderVector.Add(secondMatKeyOrder[i, j]);
                    }
                    if (secondMatKeyIDs[i, j] != 0)
                    {
                        secondKeyIDsVector.Add(secondMatKeyIDs[i, j]);
                    }
                }
            }
        }  // DivideBoard()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        int[,] isMember(InputGrid gridSize, int[,] keyIndexMap, int[] board)
        {
            int i, j, k;
            int numkeys = board.Length;

            int[,] mask = new int[gridSize.numRows, gridSize.numCols];

            for (i = 0; i < gridSize.numRows; i++)
            {
                for (j = 0; j < gridSize.numCols; j++)
                {
                    for (k = 0; k < numkeys; k++)
                    {
                        if (keyIndexMap[i, j] == board[k])
                        {
                            mask[i, j] = 1;
                            break;
                        }
                    }
                }
            }
            return mask;

        }  // isMember()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        void BalanceKeySets(int hiddenCapacity1, int hiddenCapacity2,
                              ref List<int> firstKeyOrder, ref List<int> firstKeyIDs,
                              ref List<int> secondKeyOrder, ref List<int> secondKeyIDs)
        {
            List<int> reducible = new List<int>();
            List<int> reducibleKeyOrd = new List<int>();
            List<int> incremental = new List<int>();
            List<int> incrementalKeyOrd = new List<int>();

            int lastIndex;
            int listValue;

            int length1 = firstKeyOrder.Count;
            int length2 = secondKeyOrder.Count;

            int diff = 0;
            int diff1 = length1 - hiddenCapacity1;
            int diff2 = length2 - hiddenCapacity2;

            if (diff1 > diff2)
            {
                reducible.AddRange(firstKeyIDs);
                reducibleKeyOrd.AddRange(firstKeyOrder);
                incremental.AddRange(secondKeyIDs);
                incrementalKeyOrd.AddRange(secondKeyOrder);
                diff = diff1;
            }
            else
            {
                reducible.AddRange(secondKeyIDs);
                reducibleKeyOrd.AddRange(secondKeyOrder);
                incremental.AddRange(firstKeyIDs);
                incrementalKeyOrd.AddRange(firstKeyOrder);
                diff = diff2;
            }

            // Simple balance
            // Takes the last few keys from the set which has more keys than the hidden
            // matrix capacity and pushes them to the end of the other set.

            // Subtract 1 from length of list to get zero based index of last item.
            lastIndex = reducible.Count - 1;
            for (int i = 0; i < diff; i++)
            {
                listValue = reducible[lastIndex];
                incremental.Add(listValue);
                reducible.RemoveAt(lastIndex);
                lastIndex--;
            }

            lastIndex = reducibleKeyOrd.Count - 1;
            for (int i = 0; i < diff; i++)
            {
                listValue = reducibleKeyOrd[lastIndex];
                incrementalKeyOrd.Add(listValue);
                reducibleKeyOrd.RemoveAt(lastIndex);
                lastIndex--;
            }

            firstKeyIDs.Clear();
            firstKeyOrder.Clear();
            secondKeyIDs.Clear();
            secondKeyOrder.Clear();

            if (incremental.Count > reducible.Count)
            {
                firstKeyIDs.AddRange(incremental);
                firstKeyOrder.AddRange(incrementalKeyOrd);
                secondKeyIDs.AddRange(reducible);
                secondKeyOrder.AddRange(reducibleKeyOrd);
            }
            else
            {
                firstKeyIDs.AddRange(reducible);
                firstKeyOrder.AddRange(reducibleKeyOrd);
                secondKeyIDs.AddRange(incremental);
                secondKeyOrder.AddRange(incrementalKeyOrd);
            }

        }  // BalanceKeySets()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */
    }
}
