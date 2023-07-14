using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Stimulus
{
    class MagicSquare
    {
        // These magic square tables are just one instance of a possible outcome
        // for each dimension. It provides sufficient randomization for scrambling
        // the keyboard key positions, without the complex computations. There are
        // three different algorithms for computing magic squares, depending on if
        // dimension size in an odd value, even value, or double even value. Refer
        // to the Wikipedia page on Magic Squares for all the gory details.

        private readonly int[,] MagicSquare3x3 = new int[3, 3]
            { { 8, 1, 6 },
              { 3, 5, 7 },
              { 4, 9, 2 } };

        private readonly int[,] MagicSquare4x4 = new int[4, 4]
            { { 16, 2,  3, 13 },
              { 5, 11, 10,  8 },
              { 9,  7,  6, 12 },
              { 4, 14, 15,  1 } };

        private readonly int[,] MagicSquare5x5 = new int[5, 5]
            { {17, 24,  1,  8, 15},
              {23,  5,  7, 14, 16},
              {4,   6, 13, 20, 22},
              {10, 12, 19, 21,  3},
              {11, 18, 25,  2,  9} };

        private readonly int[,] MagicSquare6x6 = new int[6, 6]
            { {35,  1,  6, 26, 19, 24},
              { 3, 32,  7, 21, 23, 25},
              {31,  9,  2, 22, 27, 20},
              { 8, 28, 33, 17, 10, 15},
              {30,  5, 34, 12, 14, 16},
              { 4, 36, 29, 13, 18, 11} };

        private readonly int[,] MagicSquare7x7 = new int[7, 7]
            { {30, 39, 48,  1, 10, 19, 28},
              {38, 47,  7,  9, 18, 27, 29},
              {46,  6,  8, 17, 26, 35, 37},
              { 5, 14, 16, 25, 34, 36, 45},
              {13, 15, 24, 33, 42, 44,  4},
              {21, 23, 32, 41, 43,  3, 12},
              {22, 31, 40, 49,  2, 11, 20} };

        private readonly int[,] MagicSquare8x8 = new int[8, 8]
            { {64,   2,   3,  61,  60,   6,   7,  57},
              { 9,  55,  54,  12,  13,  51,  50,  16},
              {17,  47,  46,  20,  21,  43,  42,  24},
              {40,  26,  27,  37,  36,  30,  31,  33},
              {32,  34,  35,  29,  28,  38,  39,  25},
              {41,  23,  22,  44,  45,  19,  18,  48},
              {49,  15,  14,  52,  53,  11,  10,  56},
              { 8,  58,  59,   5,   4,  62,  63,   1} };

        private readonly int[,] MagicSquare9x9 = new int[9, 9]
            { {47,  58,  69,  80,   1,  12,  23,  34,  45},
              {57,  68,  79,   9,  11,  22,  33,  44,  46},
              {67,  78,   8,  10,  21,  32,  43,  54,  56},
              {77,   7,  18,  20,  31,  42,  53,  55,  66},
              { 6,  17,   1,  30,  41,  52,  63,  65,  76},
              {16,  27,  29,  40,  51,  62,  64,  75,   5},
              {26,  28,  39,  50,  61,  72,  74,   4,  15},
              {36,  38,  49,  60,  71,  73,   3,  14,  25},
              {37,  48,  59,  70,  81,   2,  13,  24,  35} };

        private readonly int[,] MagicSquare10x10 = new int[10, 10]
            { {92,  99,   1,   8,  15,  67,  74,  51,  58,  40},
              {98,  80,   7,  14,  16,  73,  55,  57,  64,  41},
              { 4,  81,  88,  20,  22,  54,  56,  63,  70,  47},
              {85,  87,  19,  21,   3,  60,  62,  69,  71,  28},
              {86,  93,  25,   2,   9,  61,  68,  75,  52,  34},
              {17,  24,  76,  83,  90,  42,  49,  26,  33,  65},
              {23,   5,  82,  89,  91,  48,  30,  32,  39,  66},
              {79,   6,  13,  95,  97,  29,  31,  38,  45,  72},
              {10,  12,  94,  96,  78,  35,  37,  44,  46,  53},
              {11,  18, 100,  77,  84,  36,  43,  50,  27,  59} };

        private readonly int[,] MagicSquare11x11 = new int[11, 11]
            { { 68,   81,   94,  107,  120,    1,   14,   27,   40,   53,   66},
              { 80,   93,  106,  119,   11,   13,   26,   39,   52,   65,   67},
              { 92,  105,  118,   10,   12,   25,   38,   51,   64,   77,   79},
              {104,  117,    9,   22,   24,   37,   50,   63,   76,   78,   91},
              {116,    8,   21,   23,   36,   49,   62,   75,   88,   90,  103},
              {  7,   20,   33,   35,   48,   61,   74,   87,   89,  102,  115},
              { 19,   32,   34,   47,   60,   73,   86,   99,  101,  114,    6},
              { 31,   44,   46,   59,   72,   85,   98,  100,  113,    5,   18},
              { 43,   45,   58,   71,   84,   97,  110,  112,    4,   17,   30},
              { 55,   57,   70,   83,   96,  109,  111,    3,   16,   29,   42},
              { 56,   69,   82,   95,  108,  121,    2,   15,   28,   41,   54} };

        private readonly int[,] MagicSquare12x12 = new int[12, 12]
            { {144,    2,    3,  141,  140,    6,    7,  137,  136,   10,   11,  133},
              { 13,  131,  130,   16,   17,  127,  126,   20,   21,  123,  122,   24},
              { 25,  119,  118,   28,   29,  115,  114,   32,   33,  111,  110,   36},
              {108,   38,   39,  105,  104,   42,   43,  101,  100,   46,   47,   97},
              { 96,   50,   51,   93,   92,   54,   55,   89,   88,   58,   59,   85},
              { 61,   83,   82,   64,   65,   79,   78,   68,   69,   75,   74,   72},
              { 73,   71,   70,   76,   77,   67,   66,   80,   81,   63,   62,   84},
              { 60,   86,   87,   57,   56,   90,   91,   53,   52,   94,   95,   49},
              { 48,   98,   99,   45,   44,  102,  103,   41,   40,  106,  107,   37},
              {109,   35,   34,  112,  113,   31,   30,  116,  117,   27,   26,  120},
              {121,   23,   22,  124,  125,   19,   18,  128,  129,   15,   14,  132},
              { 12,  134,  135,    9,    8,  138,  139,    5,    4,  142,  143,    1} };

        private readonly int[,] Error2x2 = new int[2, 2]
            { { 2, 2 }, { 2, 2 } };

        public bool IsValidDimension(int dimension)
        {
            if ((dimension < 3) || (dimension > 12))
                return false;
            else
                return true;
        }

        // 
        public int[,] GetMagicTable(int dimension)
        {
            int[,] table = new int[dimension, dimension];

            switch (dimension)
            {
                case 3:
                    Array.Copy(MagicSquare3x3, table, MagicSquare3x3.Length);
                    break;
                case 4:
                    Array.Copy(MagicSquare4x4, table, MagicSquare4x4.Length);
                    break;
                case 5:
                    Array.Copy(MagicSquare5x5, table, MagicSquare5x5.Length);
                    break;
                case 6:
                    Array.Copy(MagicSquare6x6, table, MagicSquare6x6.Length);
                    break;
                case 7:
                    Array.Copy(MagicSquare7x7, table, MagicSquare7x7.Length);
                    break;
                case 8:
                    Array.Copy(MagicSquare8x8, table, MagicSquare8x8.Length);
                    break;
                case 9:
                    Array.Copy(MagicSquare9x9, table, MagicSquare9x9.Length);
                    break;
                case 10:
                    Array.Copy(MagicSquare10x10, table, MagicSquare10x10.Length);
                    break;
                case 11:
                    Array.Copy(MagicSquare11x11, table, MagicSquare11x11.Length);
                    break;
                case 12:
                    Array.Copy(MagicSquare12x12, table, MagicSquare12x12.Length);
                    break;
                default:
                    Array.Copy(Error2x2, table, Error2x2.Length);
                    break;
            }

            return table;
        }
    }
}
