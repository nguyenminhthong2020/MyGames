using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Svg.Skia;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace MyGames.Desktop.Controls
{
    public class SvgSkiaView : UserControl
    {
        private readonly SKElement _skElement;
        private SKSvg _svg;
        private SKPicture _picture;

        // parsed size from SVG xml (viewBox or width/height)
        private double _svgWidthFromXml;
        private double _svgHeightFromXml;

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(Uri), typeof(SvgSkiaView),
                new PropertyMetadata(null, OnSourceChanged));

        public Uri Source
        {
            get => (Uri)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(SvgSkiaView),
                new PropertyMetadata(Stretch.Uniform, (d, e) => ((SvgSkiaView)d)._skElement.InvalidateVisual()));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public SvgSkiaView()
        {
            _skElement = new SKElement();
            _skElement.PaintSurface += OnPaintSurface;
            Content = _skElement;
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (SvgSkiaView)d;
            v.LoadSvg();
        }

        private void LoadSvg()
        {
            _svg = new SKSvg();
            _picture = null;
            _svgWidthFromXml = 0;
            _svgHeightFromXml = 0;

            if (Source == null)
            {
                _skElement.InvalidateVisual();
                return;
            }

            Stream stream = null;
            try
            {
                // Resolve stream for various URI types (pack resource, relative resource, file)
                if (Source.IsAbsoluteUri && Source.Scheme.StartsWith("pack"))
                {
                    var info = Application.GetResourceStream(Source);
                    if (info != null) stream = info.Stream;
                }
                else if (!Source.IsAbsoluteUri)
                {
                    // try as relative resource first
                    try
                    {
                        var info = Application.GetResourceStream(new Uri(Source.ToString(), UriKind.Relative));
                        if (info != null) stream = info.Stream;
                    }
                    catch { /* ignore */ }

                    if (stream == null)
                    {
                        var path = Source.ToString();
                        if (File.Exists(path)) stream = File.OpenRead(path);
                    }
                }
                else
                {
                    if (Source.IsFile)
                        stream = File.OpenRead(Source.LocalPath);
                }

                if (stream == null)
                {
                    _skElement.InvalidateVisual();
                    return;
                }

                // We need the raw bytes because we'll parse XML first then load into SKSvg
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    var bytes = ms.ToArray();

                    // Parse XML to extract viewBox or width/height (fallback)
                    try
                    {
                        using (var xmlStream = new MemoryStream(bytes))
                        {
                            var doc = XDocument.Load(xmlStream);
                            var svgRoot = doc.Root;
                            if (svgRoot != null && svgRoot.Name.LocalName == "svg")
                            {
                                var vb = svgRoot.Attribute("viewBox")?.Value;
                                if (!string.IsNullOrWhiteSpace(vb))
                                {
                                    var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 4 &&
                                        double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w) &&
                                        double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h))
                                    {
                                        _svgWidthFromXml = w;
                                        _svgHeightFromXml = h;
                                    }
                                }

                                if ((_svgWidthFromXml <= 0 || _svgHeightFromXml <= 0))
                                {
                                    var wAttr = svgRoot.Attribute("width")?.Value;
                                    var hAttr = svgRoot.Attribute("height")?.Value;
                                    if (!string.IsNullOrWhiteSpace(wAttr) && !string.IsNullOrWhiteSpace(hAttr))
                                    {
                                        string Clean(string v) =>
                                            new string(v.TakeWhile(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

                                        if (double.TryParse(Clean(wAttr), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double w2) &&
                                            double.TryParse(Clean(hAttr), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h2))
                                        {
                                            _svgWidthFromXml = w2;
                                            _svgHeightFromXml = h2;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore XML parse errors, continue to attempt loading into SKSvg
                        _svgWidthFromXml = 0;
                        _svgHeightFromXml = 0;
                    }

                    // Load into SKSvg from bytes
                    using (var svgStream = new MemoryStream(bytes))
                    {
                        try
                        {
                            _svg.Load(svgStream);
                            _picture = _svg.Picture;
                        }
                        catch
                        {
                            _picture = null;
                        }
                    }
                }
            }
            catch
            {
                _picture = null;
            }
            finally
            {
                try { stream?.Dispose(); } catch { }
                _skElement.InvalidateVisual();
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            if (_picture == null) return;

            // 1) try SKPicture.CullRect
            var picRect = _picture.CullRect;
            float svgWidth = picRect.Width;
            float svgHeight = picRect.Height;

            // 2) fallback to parsed xml viewBox/width/height
            if (svgWidth <= 0 || svgHeight <= 0)
            {
                if (_svgWidthFromXml > 0 && _svgHeightFromXml > 0)
                {
                    svgWidth = (float)_svgWidthFromXml;
                    svgHeight = (float)_svgHeightFromXml;
                }
            }

            // 3) final fallback
            if (svgWidth <= 0 || svgHeight <= 0)
            {
                // pick a safe default
                svgWidth = 100;
                svgHeight = 100;
            }

            float viewW = e.Info.Width;
            float viewH = e.Info.Height;

            float scaleX = viewW / svgWidth;
            float scaleY = viewH / svgHeight;

            canvas.Save();

            if (Stretch == Stretch.Fill)
            {
                // non-uniform fill
                canvas.Scale(scaleX, scaleY);
                canvas.DrawPicture(_picture);
            }
            else
            {
                float scale = (Stretch == Stretch.UniformToFill) ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
                float tx = (viewW - svgWidth * scale) / 2f;
                float ty = (viewH - svgHeight * scale) / 2f;
                canvas.Translate(tx, ty);
                canvas.Scale(scale);
                canvas.DrawPicture(_picture);
            }

            canvas.Restore();
        }
    }
}
