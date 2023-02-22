using System.ComponentModel;
using System.Drawing;

namespace CoreServices.Calibration
{
    public class ButtonElement: INotifyPropertyChanged
    {
        private double _height;
        private double _width;
        private double _top;
        private double _left;
        private CustomBrushes _borderBrush;
        private CustomBrushes _backgroundBrush;
        private string _content;
        public double Height
        {
            get { return _height; }
            set { _height = value; OnPropertyChanged(nameof(Height)); }
        }
        public double Width { get { return _width; } set { _width = value; OnPropertyChanged(nameof(Width)); } }
        public double Top { get { return _top; } set { _top = value; OnPropertyChanged(nameof(Top)); } }
        public double Left { get { return _left; } set { _left = value; OnPropertyChanged(nameof(Left));} }

        public CustomBrushes BorderBrush { get { return _borderBrush; } set { _borderBrush = value; OnPropertyChanged(nameof(BorderBrush)); OnPropertyChanged(nameof(BorderBrushClr)); } }
        public Color BorderBrushClr { get { return GetColor(BorderBrush); } }
        public string Content
        {
            get { return _content; }
            set { _content = value; OnPropertyChanged(nameof(Content)); }
        }
        public CustomBrushes BackgroundBrush
        {
            get { return _backgroundBrush; }
            set { _backgroundBrush = value; OnPropertyChanged(nameof(BackgroundBrush)); OnPropertyChanged(nameof(BackgroundBrushClr)); }
        }
        public Color BackgroundBrushClr { get { return GetColor(BackgroundBrush); } }

        public event PropertyChangedEventHandler? PropertyChanged;


        public void OnPropertyChanged(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Color GetColor(CustomBrushes brushName)
        {
            switch (brushName)
            {
                case CustomBrushes.Transparent: return Color.Transparent;
                case CustomBrushes.Green: return Color.Green;
                case CustomBrushes.CornflowerBlue: return Color.CornflowerBlue;
                case CustomBrushes.Red: return Color.Red;
                case CustomBrushes.Brown: return Color.Brown;
                case CustomBrushes.Yellow: return Color.Yellow;

            }
            return Color.Transparent;
        }
    }
}