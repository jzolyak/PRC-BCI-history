using System;
using System.Collections.Generic;
using System.Text;

namespace CoreServices.Models
{
    public class EventArgs<T>: EventArgs
    {
        public EventArgs(T value)
        {
            _value = value;
        }

        private T _value;

        public T Value
        {
            get { return _value; }
        }
    }
}
