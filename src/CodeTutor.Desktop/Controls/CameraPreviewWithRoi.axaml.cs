using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CodeTutor.Application.Abstractions;

namespace CodeTutor.Desktop.Controls;

public partial class CameraPreviewWithRoi : UserControl
{
    private const double MinRegionFraction = 0.02;

    private readonly Canvas _overlayCanvas;
    private readonly Rectangle _selectionRect;
    private Point? _dragStart;
    private bool _isDragging;

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<CameraPreviewWithRoi, IImage?>(nameof(Source));

    public static readonly StyledProperty<NormalizedRectangle?> RegionProperty =
        AvaloniaProperty.Register<CameraPreviewWithRoi, NormalizedRectangle?>(
            nameof(Region),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public CameraPreviewWithRoi()
    {
        var image = new Image { Stretch = Stretch.Uniform };

        _selectionRect = new Rectangle
        {
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(64, 0, 255, 0)),
            IsVisible = false
        };

        _overlayCanvas = new Canvas
        {
            Background = Brushes.Transparent,
            ClipToBounds = true
        };
        _overlayCanvas.Children.Add(_selectionRect);

        _overlayCanvas.PointerPressed += OnPointerPressed;
        _overlayCanvas.PointerMoved += OnPointerMoved;
        _overlayCanvas.PointerReleased += OnPointerReleased;

        var root = new Grid();
        root.Children.Add(image);
        root.Children.Add(_overlayCanvas);

        image.Bind(Image.SourceProperty, this.GetObservable(SourceProperty));

        Content = root;

        SizeChanged += (_, _) => UpdateSelectionVisual();
        RegionProperty.Changed.AddClassHandler<CameraPreviewWithRoi>((c, _) => c.UpdateSelectionVisual());
        SourceProperty.Changed.AddClassHandler<CameraPreviewWithRoi>((c, _) => c.UpdateSelectionVisual());
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public NormalizedRectangle? Region
    {
        get => GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_overlayCanvas);
        if (point.Properties.IsRightButtonPressed)
        {
            ClearRegion();
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
            return;

        _dragStart = e.GetPosition(_overlayCanvas);
        _isDragging = true;
        _selectionRect.IsVisible = true;
        Canvas.SetLeft(_selectionRect, _dragStart.Value.X);
        Canvas.SetTop(_selectionRect, _dragStart.Value.Y);
        _selectionRect.Width = 0;
        _selectionRect.Height = 0;
        e.Pointer.Capture(_overlayCanvas);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _dragStart is null)
            return;

        var current = e.GetPosition(_overlayCanvas);
        var x = Math.Min(_dragStart.Value.X, current.X);
        var y = Math.Min(_dragStart.Value.Y, current.Y);
        var w = Math.Abs(current.X - _dragStart.Value.X);
        var h = Math.Abs(current.Y - _dragStart.Value.Y);

        Canvas.SetLeft(_selectionRect, x);
        Canvas.SetTop(_selectionRect, y);
        _selectionRect.Width = w;
        _selectionRect.Height = h;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging || _dragStart is null)
            return;

        _isDragging = false;
        e.Pointer.Capture(null);

        var current = e.GetPosition(_overlayCanvas);
        var controlRect = NormalizeControlRect(_dragStart.Value, current);
        _dragStart = null;

        if (!TryMapControlRectToRegion(controlRect, out var region))
        {
            ClearRegion();
            e.Handled = true;
            return;
        }

        Region = region;
        UpdateSelectionVisual();
        e.Handled = true;
    }

    private void ClearRegion()
    {
        Region = null;
        _selectionRect.IsVisible = false;
    }

    private void UpdateSelectionVisual()
    {
        if (_isDragging || Region is null)
        {
            if (Region is null)
                _selectionRect.IsVisible = false;
            return;
        }

        if (!TryMapRegionToControlRect(Region, out var controlRect))
        {
            _selectionRect.IsVisible = false;
            return;
        }

        Canvas.SetLeft(_selectionRect, controlRect.X);
        Canvas.SetTop(_selectionRect, controlRect.Y);
        _selectionRect.Width = controlRect.Width;
        _selectionRect.Height = controlRect.Height;
        _selectionRect.IsVisible = true;
    }

    private bool TryGetImageDisplayRect(out Rect displayRect, out Size imageSize)
    {
        displayRect = default;
        imageSize = default;

        if (Source is not Bitmap bitmap)
            return false;

        imageSize = bitmap.PixelSize.ToSize(1);
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return false;

        var scale = Math.Min(Bounds.Width / imageSize.Width, Bounds.Height / imageSize.Height);
        var displayW = imageSize.Width * scale;
        var displayH = imageSize.Height * scale;
        var offsetX = (Bounds.Width - displayW) / 2;
        var offsetY = (Bounds.Height - displayH) / 2;
        displayRect = new Rect(offsetX, offsetY, displayW, displayH);
        return displayW > 0 && displayH > 0;
    }

    private bool TryMapControlRectToRegion(Rect controlRect, out NormalizedRectangle region)
    {
        region = default!;
        if (!TryGetImageDisplayRect(out var displayRect, out _))
            return false;

        var intersection = controlRect.Intersect(displayRect);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return false;

        var nx = (intersection.X - displayRect.X) / displayRect.Width;
        var ny = (intersection.Y - displayRect.Y) / displayRect.Height;
        var nw = intersection.Width / displayRect.Width;
        var nh = intersection.Height / displayRect.Height;

        var candidate = new NormalizedRectangle(nx, ny, nw, nh).Clamp();
        if (!candidate.IsLargeEnough(MinRegionFraction))
            return false;

        region = candidate;
        return true;
    }

    private bool TryMapRegionToControlRect(NormalizedRectangle region, out Rect controlRect)
    {
        controlRect = default;
        if (!TryGetImageDisplayRect(out var displayRect, out _))
            return false;

        var normalized = region.Clamp();
        controlRect = new Rect(
            displayRect.X + normalized.X * displayRect.Width,
            displayRect.Y + normalized.Y * displayRect.Height,
            normalized.Width * displayRect.Width,
            normalized.Height * displayRect.Height);
        return controlRect.Width > 0 && controlRect.Height > 0;
    }

    private static Rect NormalizeControlRect(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        return new Rect(x, y, Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }
}
