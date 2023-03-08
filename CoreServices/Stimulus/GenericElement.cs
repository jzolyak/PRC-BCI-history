using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Stimulus
{
    public struct ElementProperties
    {
        public int elementIndex;
        //public int buttonIndex;
        public int left;
        public int top;
        public int right;
        public int bottom;

        public ElementProperties(int i, int l, int t, int r, int b)
        {
            elementIndex = i;   // Index into original, unfiltered fetched list of displayed elements
                                //buttonIndex = 0;    // Assign value when create screen overlay
            left = l;
            top = t;
            right = r;
            bottom = b;
        }
    }
    public class GenericElement
    {
       
    }
}