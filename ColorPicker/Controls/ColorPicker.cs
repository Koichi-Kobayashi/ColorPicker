using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorPicker.Controls
{
    [TemplatePart(Name = PartSpectrumImage, Type = typeof(Image))]
    [TemplatePart(Name = PartSpectrumCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PartSpectrumThumb, Type = typeof(Thumb))]
    [TemplatePart(Name = PartHueSlider, Type = typeof(Slider))]
    [TemplatePart(Name = PartAlphaSlider, Type = typeof(Slider))]
    public class ColorPicker : Control
    {
        private const string PartSpectrumImage = "PART_SpectrumImage";
        private const string PartSpectrumCanvas = "PART_SpectrumCanvas";
        private const string PartSpectrumThumb = "PART_SpectrumThumb";
        private const string PartHueSlider = "PART_HueSlider";
        private const string PartAlphaSlider = "PART_AlphaSlider";

        static ColorPicker()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker),
                new FrameworkPropertyMetadata(typeof(ColorPicker)));
        }

        private Image? _spectrumImage;
        private Canvas? _spectrumCanvas;
        private Thumb? _spectrumThumb;
        private Slider? _hueSlider;
        private Slider? _alphaSlider;

        private WriteableBitmap? _spectrumBitmap;
        private int _bmpW;
        private int _bmpH;

        private bool _isTemplateUpdating;

        #region Dependency Properties

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(Colors.Red,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(
                nameof(Hue),
                typeof(double),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(0d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnHSVChanged, CoerceHue));

        public double Hue
        {
            get => (double)GetValue(HueProperty);
            set => SetValue(HueProperty, value);
        }

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register(
                nameof(Saturation),
                typeof(double),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(1d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnHSVChanged, CoerceUnit));

        public double Saturation
        {
            get => (double)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(1d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnHSVChanged, CoerceUnit));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty AlphaProperty =
            DependencyProperty.Register(
                nameof(Alpha),
                typeof(double),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(1d,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnAlphaChanged, CoerceUnit));

        public double Alpha
        {
            get => (double)GetValue(AlphaProperty);
            set => SetValue(AlphaProperty, value);
        }

        private static object CoerceHue(DependencyObject d, object baseValue)
        {
            var v = (double)baseValue;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0d;
            v %= 360d;
            if (v < 0) v += 360d;
            return v;
        }

        private static object CoerceUnit(DependencyObject d, object baseValue)
        {
            var v = (double)baseValue;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0d;
            if (v < 0) return 0d;
            if (v > 1) return 1d;
            return v;
        }

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Detach();

            _spectrumImage = GetTemplateChild(PartSpectrumImage) as Image;
            _spectrumCanvas = GetTemplateChild(PartSpectrumCanvas) as Canvas;
            _spectrumThumb = GetTemplateChild(PartSpectrumThumb) as Thumb;
            _hueSlider = GetTemplateChild(PartHueSlider) as Slider;
            _alphaSlider = GetTemplateChild(PartAlphaSlider) as Slider;

            Attach();

            // 初期同期
            SyncFromSelectedColor(SelectedColor);
            RebuildSpectrumBitmapIfNeeded();
            RenderSpectrum();
            UpdateThumbPosition();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RebuildSpectrumBitmapIfNeeded();
                RenderSpectrum();
                UpdateThumbPosition();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Attach()
        {
            if (_spectrumCanvas != null)
            {
                _spectrumCanvas.SizeChanged += OnSpectrumSizeChanged;
                _spectrumCanvas.MouseLeftButtonDown += OnSpectrumMouseDown;
                _spectrumCanvas.MouseMove += OnSpectrumMouseMove;
                _spectrumCanvas.MouseLeftButtonUp += OnSpectrumMouseUp;
            }

            if (_hueSlider != null)
                _hueSlider.ValueChanged += OnHueSliderChanged;

            if (_alphaSlider != null)
                _alphaSlider.ValueChanged += OnAlphaSliderChanged;

            if (_spectrumThumb != null)
            {
                // Thumb のドラッグ操作も可能に（Canvas上をドラッグしてもOK）
                _spectrumThumb.DragDelta += OnThumbDragDelta;
            }
        }

        private void Detach()
        {
            if (_spectrumCanvas != null)
            {
                _spectrumCanvas.SizeChanged -= OnSpectrumSizeChanged;
                _spectrumCanvas.MouseLeftButtonDown -= OnSpectrumMouseDown;
                _spectrumCanvas.MouseMove -= OnSpectrumMouseMove;
                _spectrumCanvas.MouseLeftButtonUp -= OnSpectrumMouseUp;
            }

            if (_hueSlider != null)
                _hueSlider.ValueChanged -= OnHueSliderChanged;

            if (_alphaSlider != null)
                _alphaSlider.ValueChanged -= OnAlphaSliderChanged;

            if (_spectrumThumb != null)
                _spectrumThumb.DragDelta -= OnThumbDragDelta;
        }

        #region Property change callbacks

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cp = (ColorPicker)d;
            if (cp._isTemplateUpdating) return;

            cp.SyncFromSelectedColor((Color)e.NewValue);
            cp.RenderSpectrum(); // Hueが変わる可能性があるので再描画
            cp.UpdateThumbPosition();
        }

        private static void OnHSVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cp = (ColorPicker)d;
            if (cp._isTemplateUpdating) return;

            cp.UpdateSelectedColorFromHSV();
            // Hueが変わったらスペクトラムも変わる
            if (e.Property == HueProperty)
            {
                cp.RebuildSpectrumBitmapIfNeeded();
                cp.RenderSpectrum();
            }
            cp.UpdateThumbPosition();
        }

        private static void OnAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cp = (ColorPicker)d;
            if (cp._isTemplateUpdating) return;

            cp.UpdateSelectedColorFromHSV();
        }

        #endregion

        #region Input / interactions (Spectrum)

        private bool _capturing;

        private void OnSpectrumMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_spectrumCanvas == null) return;
            _capturing = true;
            _spectrumCanvas.CaptureMouse();
            SetSVFromPoint(e.GetPosition(_spectrumCanvas));
        }

        private void OnSpectrumMouseMove(object sender, MouseEventArgs e)
        {
            if (!_capturing || _spectrumCanvas == null) return;
            SetSVFromPoint(e.GetPosition(_spectrumCanvas));
        }

        private void OnSpectrumMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_spectrumCanvas == null) return;
            _capturing = false;
            _spectrumCanvas.ReleaseMouseCapture();
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_spectrumCanvas == null) return;

            // 現在のThumb位置にdeltaを加え、Canvas座標へ
            var left = Canvas.GetLeft(_spectrumThumb);
            var top = Canvas.GetTop(_spectrumThumb);

            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            // Thumbの中心で計算
            var x = left + (_spectrumThumb?.ActualWidth ?? 0) / 2 + e.HorizontalChange;
            var y = top + (_spectrumThumb?.ActualHeight ?? 0) / 2 + e.VerticalChange;

            SetSVFromPoint(new Point(x, y));
        }

        private void SetSVFromPoint(Point p)
        {
            if (_spectrumCanvas == null) return;

            var w = Math.Max(1, _spectrumCanvas.ActualWidth);
            var h = Math.Max(1, _spectrumCanvas.ActualHeight);

            var tw = _spectrumThumb?.ActualWidth ?? 0;
            var th = _spectrumThumb?.ActualHeight ?? 0;

            var x = Clamp(p.X, tw / 2, w - tw / 2);
            var y = Clamp(p.Y, th / 2, h - th / 2);

            var s = x / w;
            var v = 1.0 - (y / h);

            _isTemplateUpdating = true;
            try
            {
                Saturation = s;
                Value = v;
            }
            finally
            {
                _isTemplateUpdating = false;
            }

            UpdateSelectedColorFromHSV();
            UpdateThumbPosition();
        }

        #endregion

        #region Sliders

        private void OnHueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider(0..360)
            Hue = e.NewValue;
        }

        private void OnAlphaSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider(0..1)
            Alpha = e.NewValue;
        }

        #endregion

        #region Spectrum rendering

        private void OnSpectrumSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RebuildSpectrumBitmapIfNeeded();
            RenderSpectrum();
            UpdateThumbPosition();
        }

        private void RebuildSpectrumBitmapIfNeeded()
        {
            if (_spectrumImage == null || _spectrumCanvas == null) return;

            // Canvasの表示サイズを基準にする（Imageは0になりやすい）
            var w = (int)Math.Max(1, Math.Round(_spectrumCanvas.ActualWidth));
            var h = (int)Math.Max(1, Math.Round(_spectrumCanvas.ActualHeight));

            if (w == _bmpW && h == _bmpH && _spectrumBitmap != null) return;

            _bmpW = w;
            _bmpH = h;

            _spectrumBitmap = new WriteableBitmap(_bmpW, _bmpH, 96, 96, PixelFormats.Bgra32, null);
            _spectrumImage.Source = _spectrumBitmap;
        }


        private void RenderSpectrum()
        {
            if (_spectrumBitmap == null) return;

            // Hue固定で S(横) / V(縦) の2D面を生成（不透明RGB）
            var w = _bmpW;
            var h = _bmpH;
            if (w <= 0 || h <= 0) return;

            var stride = w * 4;
            var pixels = new byte[h * stride];

            var hue = Hue;

            for (int y = 0; y < h; y++)
            {
                double v = 1.0 - (double)y / (h - 1 == 0 ? 1 : (h - 1));
                for (int x = 0; x < w; x++)
                {
                    double s = (double)x / (w - 1 == 0 ? 1 : (w - 1));

                    var rgb = HsvColor.FromHsv(hue, s, v).ToColor(alpha: 1.0);

                    int i = (y * stride) + (x * 4);
                    pixels[i + 0] = rgb.B;
                    pixels[i + 1] = rgb.G;
                    pixels[i + 2] = rgb.R;
                    pixels[i + 3] = 255; // opaque spectrum; alpha is controlled separately
                }
            }

            _spectrumBitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        }

        private void UpdateThumbPosition()
        {
            if (_spectrumCanvas == null || _spectrumThumb == null) return;

            var w = Math.Max(1, _spectrumCanvas.ActualWidth);
            var h = Math.Max(1, _spectrumCanvas.ActualHeight);

            var tw = Math.Max(1, _spectrumThumb.ActualWidth);
            var th = Math.Max(1, _spectrumThumb.ActualHeight);

            // Thumb中心が必ずCanvas内に収まるようにクランプ
            var x = Saturation * w;
            var y = (1.0 - Value) * h;

            x = Clamp(x, tw / 2, w - tw / 2);
            y = Clamp(y, th / 2, h - th / 2);

            Canvas.SetLeft(_spectrumThumb, x - tw / 2);
            Canvas.SetTop(_spectrumThumb, y - th / 2);
        }


        #endregion

        #region Sync helpers

        private void SyncFromSelectedColor(Color c)
        {
            var hsv = HsvColor.FromColor(c);

            _isTemplateUpdating = true;
            try
            {
                Hue = hsv.H;
                Saturation = hsv.S;
                Value = hsv.V;
                Alpha = c.A / 255.0;
            }
            finally
            {
                _isTemplateUpdating = false;
            }
        }

        private void UpdateSelectedColorFromHSV()
        {
            var c = HsvColor.FromHsv(Hue, Saturation, Value).ToColor(Alpha);

            _isTemplateUpdating = true;
            try
            {
                SelectedColor = c;
            }
            finally
            {
                _isTemplateUpdating = false;
            }
        }

        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);

        #endregion
    }
}
