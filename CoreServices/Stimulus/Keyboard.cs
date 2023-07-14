using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CoreServices.Stimulus
{
    public struct GridLayout
    {
        public int minLeftCoord;
        public int minTopCoord;
        public int maxRightCoord;
        public int maxBottomCoord;
        public int minKeyHeight;
        public int minKeyWidth;
        public int maxKeyRows;
        public int maxKeyCols;
        public int tolerance;
        public int margins;
    }

    public struct KeyMapping
    {
        public int keyID;
        public int elementIndex;    // Index into original list of displayed elements. Need to click on element. 
        public int buttonIndex;     // Index into filtered list of elements. There is one overlay button per filtered element.
        public int left;
        public int top;
        public int right;
        public int bottom;

        public KeyMapping(int k, int e, int btn, int l, int t, int r, int b)
        {
            keyID = k;
            elementIndex = e;
            buttonIndex = btn;
            left = l;
            top = t;
            right = r;
            bottom = b;
        }
    }
    public class Keyboard
    {
        public const int PRCDeviceMaxNumRows = 9;
        public const int PRCDeviceMaxNumCols = 16;

        

        // Constructor
        public Keyboard()
        { }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public GridLayout ExtractGridData(List<ElementProperties> elementList)
        {
            int minLeftCoord;
            int minTopCoord;
            int maxRightCoord;
            int maxBottomCoord;
            int minKeyHeight;
            int minKeyWidth;
            int numElements;

            GridLayout gridData = new GridLayout();

            minLeftCoord = elementList[0].left;
            minTopCoord = elementList[0].top;
            maxRightCoord = elementList[0].right;
            maxBottomCoord = elementList[0].bottom;

            minKeyHeight = elementList[0].bottom - elementList[0].top;
            minKeyWidth = elementList[0].right - elementList[0].left;

            numElements = elementList.Count;

            for (int i = 1; i < numElements; i++)
            {
                if (minLeftCoord > elementList[i].left)
                    minLeftCoord = elementList[i].left;

                if (minTopCoord > elementList[i].top)
                    minTopCoord = elementList[i].top;

                if (maxRightCoord < elementList[i].right)
                    maxRightCoord = elementList[i].right;

                if (maxBottomCoord < elementList[i].bottom)
                    maxBottomCoord = elementList[i].bottom;

                if (minKeyHeight > (elementList[i].bottom - elementList[i].top))
                    minKeyHeight = elementList[i].bottom - elementList[i].top;

                if (minKeyWidth > (elementList[i].right - elementList[i].left))
                    minKeyWidth = elementList[i].right - elementList[i].left;
            }

            gridData.minLeftCoord = minLeftCoord;
            gridData.minTopCoord = minTopCoord;
            gridData.maxRightCoord = maxRightCoord;
            gridData.maxBottomCoord = maxBottomCoord;
            gridData.minKeyHeight = minKeyHeight;
            gridData.minKeyWidth = minKeyWidth;

            gridData.maxKeyRows = 0;    // Assign after construct listOfRows
            gridData.maxKeyCols = 0;    // Assign after construct listOfCols

            // Starting guess until collect real display data
            gridData.tolerance = 2;     // pixels
            gridData.margins = 20;      // pixels between two adjacent key columns
                                        // (right / left) or key rows (top / bot)
            return gridData;

        }  // ExtractGridData()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public List<List<int>> CreateListOfRows(List<ElementProperties> elementList)
        {
            bool foundRowMatch;

            int numElements;
            int numRows;
            int rowIndex;
            int indexValue;
            int currentIndex;

            List<List<int>> listOfRows = new List<List<int>>();

            List<int> row = new List<int>();

            currentIndex = 0;

            // Put first index value to elementList as first value in first row
            row.Add(currentIndex);
            listOfRows.Add(row);

            numElements = elementList.Count;

            for (currentIndex = 1; currentIndex < numElements; currentIndex++)
            {
                rowIndex = 0;

                numRows = listOfRows.Count;

                // Locate index value of first element found in the current row
                indexValue = listOfRows[rowIndex][0];

                // Compare the top boundary of the current element to see if matches top of current row
                if (elementList[currentIndex].top == elementList[indexValue].top)
                {
                    listOfRows[rowIndex].Add(currentIndex);
                }
                else
                {
                    rowIndex += 1;
                    foundRowMatch = false;

                    // Search thru other rows found in listOfRows to see which row the element belongs
                    while ((rowIndex < numRows) & (foundRowMatch == false))
                    {
                        indexValue = listOfRows[rowIndex][0];

                        if (elementList[currentIndex].top == elementList[indexValue].top)
                        {
                            listOfRows[rowIndex].Add(currentIndex);
                            foundRowMatch = true;
                        }
                        else
                        {
                            rowIndex += 1;
                        }
                    }

                    // If found no matching row, create a new one and insert index into the list
                    if (foundRowMatch == false)
                    {
                        List<int> newRow = new List<int>();

                        newRow.Add(currentIndex);
                        listOfRows.Add(newRow);
                    }
                }
            }

            return listOfRows;

        }  // CreateListOfRows()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public List<List<int>> CreateListOfColumns(List<ElementProperties> elementList)
        {
            bool foundColumnMatch;

            int numElements;
            int numCols;
            int columnIndex;
            int indexValue;
            int currentIndex;

            List<List<int>> listOfColumns = new List<List<int>>();

            List<int> column = new List<int>();

            currentIndex = 0;

            // Put first index value to elementList as first value in first column
            column.Add(currentIndex);
            listOfColumns.Add(column);

            numElements = elementList.Count;

            for (currentIndex = 1; currentIndex < numElements; currentIndex++)
            {
                columnIndex = 0;

                numCols = listOfColumns.Count;

                // Locate index value of first element found in the current column
                indexValue = listOfColumns[columnIndex][0];

                // Compare the left boundary of the current element to see if matches left side of current column
                if (elementList[currentIndex].left == elementList[indexValue].left)
                {
                    listOfColumns[columnIndex].Add(currentIndex);
                }
                else
                {
                    columnIndex += 1;
                    foundColumnMatch = false;

                    // Search thru other columns found in listOfColumns to see which column the element belongs
                    while ((columnIndex < numCols) & (foundColumnMatch == false))
                    {
                        indexValue = listOfColumns[columnIndex][0];

                        if (elementList[currentIndex].left == elementList[indexValue].left)
                        {
                            listOfColumns[columnIndex].Add(currentIndex);
                            foundColumnMatch = true;
                        }
                        else
                        {
                            columnIndex += 1;
                        }
                    }

                    // If found no matching column, create a new one and insert index into the list
                    if (foundColumnMatch == false)
                    {
                        List<int> newColumn = new List<int>();

                        newColumn.Add(currentIndex);
                        listOfColumns.Add(newColumn);
                    }
                }
            }

            return listOfColumns;

        }  // CreateListOfColumns()

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        public int[,] CreateKeyIDMap(GridLayout gridData, bool PRC_mode)
        {
            int maxNumberofRows;
            int maxColPerRow;

            if (PRC_mode == true)
            {
                maxNumberofRows = PRCDeviceMaxNumRows;
                maxColPerRow = PRCDeviceMaxNumCols;
            }
            else
            {
                maxNumberofRows = gridData.maxKeyRows;
                maxColPerRow = gridData.maxKeyCols;
            }

            int[,] keyIDMap = new int[maxNumberofRows, maxColPerRow];

            // IMPORTANT NOTE:
            //    A keyID value of zero has special significance in FlashGrouping and FlashSequence algorithms.
            //    A grid location with a keyID = 0 means that the location is not occupied by a selectable key (element).
            //    The number of keys to be flashed is completely variable for each individual flash sequence.
            //    A keyID value = 0 means you reached the end of the key list for the flash sequence.
            //    As a result, the first key mapping grid location starts off at a value of keyID = 1.

            int offset = 1;

            for (int i = 0; i < maxNumberofRows; i++)
            {
                for (int j = 0; j < maxColPerRow; j++)
                {
                    keyIDMap[i, j] = offset + j + (i * maxColPerRow);
                }
            }
            return keyIDMap;
        }

        /* ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++ */

        //public List<KeyMapping> AssignKeyMapping(List<ElementProperties> elementList, GridLayout gridData,
        //                                         int[,] keyIDMap, out int[] crossReferenceTable)

        public List<KeyMapping> AssignKeyMapping(List<ElementProperties> elementList, GridLayout gridData, int[,] keyIDMap)
        {
            int keyID;
            int originalListElementIndex;
            int numFilteredElements;
            int buttonIndex;
            //int keyIDvalue;
            //int maxkeyIDvalue;
            //int numEntries;

            int minLeft = gridData.minLeftCoord;
            int minTop = gridData.minTopCoord;
            int maxRight = gridData.maxRightCoord;
            int maxBottom = gridData.maxBottomCoord;
            int maxKeyRows = gridData.maxKeyRows;
            int maxKeyCols = gridData.maxKeyCols;
            int rowWidth = gridData.maxRightCoord - gridData.minLeftCoord;
            int colHeight = gridData.maxBottomCoord - gridData.minTopCoord;

            int rowPosition = 0;
            int colPosition = 0;

            numFilteredElements = elementList.Count;

            List<KeyMapping> keyMapping = new List<KeyMapping>();

            for (int i = 0; i < numFilteredElements; i++)
            {
                colPosition = (int)Math.Round((double)((elementList[i].left - minLeft) * maxKeyCols) / (double)rowWidth);

                rowPosition = (int)Math.Round((double)((elementList[i].top - minTop) * maxKeyRows) / (double)colHeight);

                keyID = keyIDMap[rowPosition, colPosition];

                originalListElementIndex = elementList[i].elementIndex;

                // buttonList created from filtered element list, hence buttonIndex has same value as index i.
                buttonIndex = i;

                keyMapping.Add(new KeyMapping(keyID, originalListElementIndex, buttonIndex, elementList[i].left,
                                                elementList[i].top, elementList[i].right, elementList[i].bottom));
            }

            /*
            numEntries = keyMapping.Count;

            // Largest keyID value is located at end of the list
            maxkeyIDvalue = keyMapping[numEntries - 1].keyID;

            // There are generally more locations on keyboard grid than actual number of selectable keys.
            // Create direct lookup table between keyID value and button index instead of searching for match.
            crossReferenceTable = new int[maxkeyIDvalue + 1];

            for (int i = 0; i < maxkeyIDvalue; i++)
            {
                crossReferenceTable[i] = 0;
            }

            for (int i = 0; i < numEntries; i++)
            {
                keyIDvalue = keyMapping[i].keyID;
                crossReferenceTable[keyIDvalue] = keyMapping[i].buttonIndex;
            }
            */

            return keyMapping;

        }  // AssignKeyIDMapping()

    }
}
