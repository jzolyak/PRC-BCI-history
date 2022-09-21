namespace CoreServices.Calibration
{
    public class ButtonElement
    {
        public double Height { get; set; }
        public double Width { get; set; }

        public double Top { get; set; }
        public double Left { get; set; }

        public CustomBrushes BorderBrush { get; set; }
        public string Content { get; set; }
        public CustomBrushes BackgroundBrush { get; set; }
    }
}