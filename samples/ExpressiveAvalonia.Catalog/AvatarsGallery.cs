using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ExpressiveAvalonia.Controls;
using ExpressiveAvalonia.Theming;

namespace ExpressiveAvalonia.Catalog;

/// <summary>One live avatar, thumbnail or brand mark with the family variants in the inspector.</summary>
public static class AvatarsGallery
{
    public static Control Create() => new AvatarsPlayground();
}

internal sealed class AvatarsPlayground : Grid, IComponentPlayground
{
    private static readonly MaterialAvatarKind[] AvatarKinds =
    {
        MaterialAvatarKind.Placeholder,
        MaterialAvatarKind.Check,
        MaterialAvatarKind.Monogram,
        MaterialAvatarKind.Image,
    };

    private static readonly MaterialThumbnailKind[] ThumbnailKinds =
    {
        MaterialThumbnailKind.Image,
        MaterialThumbnailKind.Video,
    };

    private static readonly MaterialBrandMarkKind[] BrandKinds =
    {
        MaterialBrandMarkKind.ColourfulLogo,
        MaterialBrandMarkKind.Favicon,
    };

    private readonly MaterialAvatar _avatar = new() { Size = 96 };
    private readonly MaterialThumbnail _thumbnail = new() { Width = 160 };
    private readonly MaterialBrandMark _brand = new() { Size = 48 };
    private readonly MaterialBadge _badge = new()
    {
        Size = MaterialBadgeSize.Large,
        Text = "3",
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, -4, -4, 0),
    };
    private readonly Grid _avatarHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly ContentControl _host = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _caption = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };
    private readonly TextBlock _readout = new()
    {
        Text = "Активаций: 0",
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };
    private readonly TextBlock _hint = new()
    {
        Text = "Интерактивный аватар и миниатюра активируются кликом, Space и Enter.",
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        MaxWidth = 420,
    };
    private readonly MaterialButtonGroup _family;
    private readonly ComboBox _avatarKind;
    private readonly ComboBox _thumbnailKind;
    private readonly ComboBox _brandKind;
    private readonly ComboBox _photo;
    private readonly ComboBox _ratio;
    private readonly TextBox _letter;
    private readonly Slider _avatarSize;
    private readonly Slider _thumbWidth;
    private readonly Slider _corner;
    private readonly Slider _brandSize;
    private readonly ToggleSwitch _checked;
    private readonly ToggleSwitch _interactive;
    private readonly ToggleSwitch _badgeVisible;
    private readonly TextBox _badgeText;
    private readonly StackPanel _avatarPanel;
    private readonly StackPanel _thumbnailPanel;
    private readonly StackPanel _brandPanel;
    private readonly StackPanel _photoPanel;
    private readonly StackPanel _checkedPanel;
    private readonly StackPanel _badgePanel;
    private readonly StackPanel _behaviorPanel;
    private int _activations;
    public StackPanel Settings { get; } = InspectorLayout.CreatePane();

    internal AvatarsPlayground()
    {
        _avatarHost.Children.Add(_avatar);
        _avatarHost.Children.Add(_badge);
        _host.Content = _avatarHost;

        AutomationProperties.SetName(_avatar, "Аватар профиля");
        AutomationProperties.SetName(_thumbnail, "Миниатюра медиа");
        AutomationProperties.SetName(_brand, "Фирменный знак");
        _avatar.Click += (_, _) => Activated("аватар");
        _thumbnail.Click += (_, _) => Activated("миниатюра");

        var card = new Border
        {
            Width = 520,
            MinHeight = 300,
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _host,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("MaterialSurfaceContainerLowBrush"));

        var stage = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stage.Children.Add(card);
        stage.Children.Add(_caption);
        stage.Children.Add(_hint);
        stage.Children.Add(_readout);
        Children.Add(stage);

        var topSection = InspectorLayout.Section(Settings, divider: false);
        _family = InspectorLayout.Choice(topSection, "Семейство", new[] { "Avatar", "Thumbnail", "Brand mark" }, 0);

        _avatarPanel = InspectorLayout.Section(Settings, "Аватар");
        _avatarKind = InspectorLayout.Combo(
            _avatarPanel,
            "Стиль аватара",
            Array.ConvertAll(AvatarKinds, kind => kind.ToString()),
            2);
        _letter = InspectorLayout.TextField(_avatarPanel, "Монограмма (Letter)", "A");
        AutomationProperties.SetName(_letter, "Монограмма");
        _checkedPanel = InspectorLayout.Switches(_avatarPanel);
        _checked = InspectorLayout.Switch(_checkedPanel, "Галочка (IsChecked)", true);
        _avatarSize = InspectorLayout.Slider(_avatarPanel, "Размер", 24, 160, 96, 8, "dp", "0");
        AutomationProperties.SetName(_avatarSize, "Размер аватара");

        _thumbnailPanel = InspectorLayout.Section(Settings, "Миниатюра");
        _thumbnailKind = InspectorLayout.Combo(
            _thumbnailPanel,
            "Тип миниатюры",
            Array.ConvertAll(ThumbnailKinds, kind => kind.ToString()),
            0);
        _ratio = InspectorLayout.Combo(
            _thumbnailPanel,
            "Соотношение сторон (AspectRatio)",
            new[] { "Как в источнике", "1:1", "16:9", "4:3" },
            0);
        AutomationProperties.SetName(_ratio, "Соотношение сторон миниатюры");
        _thumbWidth = InspectorLayout.Slider(_thumbnailPanel, "Ширина", 56, 240, 160, 8, "dp", "0");
        AutomationProperties.SetName(_thumbWidth, "Ширина миниатюры");
        _corner = InspectorLayout.Slider(_thumbnailPanel, "Скругление", 0, 32, 0, 2, "dp", "0");
        AutomationProperties.SetName(_corner, "Скругление миниатюры");

        _brandPanel = InspectorLayout.Section(Settings, "Фирменный знак");
        _brandKind = InspectorLayout.Combo(
            _brandPanel,
            "Тип знака",
            Array.ConvertAll(BrandKinds, kind => kind.ToString()),
            0);
        AutomationProperties.SetName(_brandKind, "Тип фирменного знака");
        _brandSize = InspectorLayout.Slider(_brandPanel, "Размер", 18, 96, 48, 6, "dp", "0");
        AutomationProperties.SetName(_brandSize, "Размер фирменного знака");

        _photoPanel = InspectorLayout.Section(Settings, "Источник изображения");
        _photo = InspectorLayout.Combo(_photoPanel, "Фотография (Source)", MaterialAvatars.All, 0);
        AutomationProperties.SetName(_photo, "Фотография");

        _badgePanel = InspectorLayout.Section(Settings, "Значок");
        var badgeSwitches = InspectorLayout.Switches(_badgePanel);
        _badgeVisible = InspectorLayout.Switch(badgeSwitches, "Показывать значок", true);
        _badgeText = InspectorLayout.TextField(_badgePanel, "Текст значка", "3");

        _behaviorPanel = InspectorLayout.Section(Settings, "Поведение");
        var behaviorSwitches = InspectorLayout.Switches(_behaviorPanel);
        _interactive = InspectorLayout.Switch(behaviorSwitches, "Интерактивный (Click, Command)", true);

        InspectorLayout.Note(
            Settings,
            "Один экземпляр семейства. Avatar: Placeholder/Check/Monogram/Image, Letter, IsChecked и " +
            "интерактивность; значок — настоящий MaterialBadge поверх аватара. Thumbnail: image/video, " +
            "AspectRatio и CornerRadius. Brand mark: Colourful logo и Favicon в исходном масштабе 18 dp.");

        _family.SelectionChanged += (_, _) => Apply();
        foreach (var combo in new[] { _avatarKind, _thumbnailKind, _brandKind, _photo, _ratio })
            combo.SelectionChanged += (_, _) => Apply();
        foreach (var slider in new[] { _avatarSize, _thumbWidth, _corner, _brandSize })
            slider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(); };
        foreach (var toggle in new[] { _checked, _interactive, _badgeVisible })
            toggle.IsCheckedChanged += (_, _) => Apply();
        _letter.TextChanged += (_, _) => Apply();
        _badgeText.TextChanged += (_, _) => Apply();

        Apply();
    }

    private void Apply()
    {
        var family = Math.Clamp(_family.SelectedIndex, 0, 2);
        var avatarKind = AvatarKinds[Math.Clamp(_avatarKind.SelectedIndex, 0, AvatarKinds.Length - 1)];
        var thumbnailKind = ThumbnailKinds[Math.Clamp(_thumbnailKind.SelectedIndex, 0, ThumbnailKinds.Length - 1)];
        var brandKind = BrandKinds[Math.Clamp(_brandKind.SelectedIndex, 0, BrandKinds.Length - 1)];
        var usesPhoto = (family == 0 && avatarKind == MaterialAvatarKind.Image) || family == 1;
        IImage? photo = usesPhoto && _photo.SelectedItem is MaterialAvatarInfo info
            ? info.Image
            : null;

        _avatarPanel.IsVisible = family == 0;
        _thumbnailPanel.IsVisible = family == 1;
        _brandPanel.IsVisible = family == 2;
        _photoPanel.IsVisible = usesPhoto;
        _letter.IsVisible = avatarKind == MaterialAvatarKind.Monogram;
        _checkedPanel.IsVisible = avatarKind == MaterialAvatarKind.Check;
        _badgePanel.IsVisible = family == 0;
        _behaviorPanel.IsVisible = family != 2;

        _avatar.Kind = avatarKind;
        _avatar.Letter = _letter.Text;
        _avatar.Source = photo;
        _avatar.Size = _avatarSize.Value;
        _avatar.IsChecked = _checked.IsChecked == true;
        _avatar.IsInteractive = _interactive.IsChecked == true;

        _badge.IsVisible = _badgeVisible.IsChecked == true;
        _badge.Text = _badgeText.Text;
        _badge.Size = string.IsNullOrWhiteSpace(_badgeText.Text) ? MaterialBadgeSize.Small : MaterialBadgeSize.Large;

        _thumbnail.Kind = thumbnailKind;
        _thumbnail.Source = photo;
        _thumbnail.Width = _thumbWidth.Value;
        _thumbnail.Height = double.NaN;
        _thumbnail.AspectRatio = _ratio.SelectedIndex switch
        {
            1 => 1d,
            2 => 16d / 9d,
            3 => 4d / 3d,
            _ => double.NaN,
        };
        _thumbnail.CornerRadius = new CornerRadius(_corner.Value);
        _thumbnail.IsInteractive = _interactive.IsChecked == true;

        _brand.Kind = brandKind;
        _brand.Size = _brandSize.Value;

        _host.Content = family == 1 ? _thumbnail : family == 2 ? _brand : (Control)_avatarHost;
        _hint.IsVisible = family != 2;
        _caption.Text = family switch
        {
            1 => $"MaterialThumbnail · {thumbnailKind} · {_thumbWidth.Value:0} dp",
            2 => $"MaterialBrandMark · {brandKind} · {_brandSize.Value:0} dp",
            _ => $"MaterialAvatar · {avatarKind} · {_avatarSize.Value:0} dp",
        };
    }

    private void Activated(string what)
    {
        _activations++;
        _readout.Text = $"Активаций: {_activations} · последний: {what}";
    }
}
