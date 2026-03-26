using CoreServices.Common;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;

namespace CoreServices.Stimulus
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
        private double _opacity;
        private string fileName;
        private bool   isPicVisible;
        public double Height
        {
            get { return _height; }
            set { _height = value; OnPropertyChanged(nameof(Height)); }
        }

        public double Opacity
        {
            get { return _opacity; }
            set { _opacity = value; OnPropertyChanged(nameof(Opacity)); }
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
        public string ImagePath => System.IO.Path.Combine("..","Images", FileName);

        public ICommand ClickCmd { get; set; }
        public bool IsEnabled { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        public Action<string> ClickAction { get; set; }
        public string Name { get; set; }
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; OnPropertyChanged(nameof(FileName)); OnPropertyChanged(nameof(ImagePath)); }
        }
        public bool IsPicVisible
        {
            get { return isPicVisible; }
            set { isPicVisible = value; OnPropertyChanged(nameof(IsPicVisible)); }
        }
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
                case CustomBrushes.Blue: return Color.Blue;
                case CustomBrushes.Brown: return Color.Brown;
                case CustomBrushes.Yellow: return Color.Yellow;

            }
            return Color.Transparent;
        }

        public ButtonElement()
        {
            ClickCmd = new RelayCommand(OnClick);
            IsEnabled = false;
            Name=String.Empty;
            Opacity = 1;
            FileName = "default.png";
        }

        private void OnClick(object obj)
        {
           if(ClickAction!=null && !string.IsNullOrEmpty(Name))
            {
                ClickAction(Name);
            }
        }
    }
}