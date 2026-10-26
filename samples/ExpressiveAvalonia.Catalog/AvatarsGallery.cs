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
    private readonly StackPanel _avatarPanel = new() { Spacing = 16 };
    private readonly StackPanel _thumbnailPanel = new() { Spacing = 16 };
    private readonly StackPanel _brandPanel = new() { Spacing = 16 };
    private readonly StackPanel _letterPanel = new() { Spacing = 8 };
    private readonly StackPanel _photoPanel = new() { Spacing = 8 };
    private readonly StackPanel _checkedPanel = new() { Spacing = 8 };
    private readonly StackPanel _badgePanel = new() { Spacing = 8 };
    private readonly TextBlock _avatarSizeCaption = new();
    private readonly TextBlock _thumbCaption = new();
    private readonly TextBlock _cornerCaption = new();
    private readonly TextBlock _brandCaption = new();
    private int _activations;
    public StackPanel Settings { get; } = new() { Spacing = 16 };

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

        _family = Choice("Семейство", new[] { "Avatar", "Thumbnail", "Brand mark" }, 0);

        _avatarPanel.Children.Add(new TextBlock { Text = "Стиль аватара" });
        _avatarKind = new ComboBox
        {
            ItemsSource = Array.ConvertAll(AvatarKinds, kind => kind.ToString()),
            SelectedIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_avatarKind, "Стиль аватара");
        _avatarPanel.Children.Add(_avatarKind);

        _letterPanel.Children.Add(new TextBlock { Text = "Монограмма (Letter)" });
        _letter = new TextBox { Text = "A", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_letter, MaterialTextFieldVariant.Outlined);
        AutomationProperties.SetName(_letter, "Монограмма");
        _letterPanel.Children.Add(_letter);
        _avatarPanel.Children.Add(_letterPanel);

        _photoPanel.Children.Add(new TextBlock { Text = "Фотография (Source)" });
        _photo = new ComboBox
        {
            ItemsSource = MaterialAvatars.All,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_photo, "Фотография");
        _photoPanel.Children.Add(_photo);
        Settings.Children.Add(_photoPanel);

        _avatarSizeCaption.Text = "Размер: 96 dp";
        _avatarPanel.Children.Add(_avatarSizeCaption);
        _avatarSize = new Slider { Minimum = 24, Maximum = 160, Value = 96, TickFrequency = 8, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_avatarSize, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_avatarSize, false);
        AutomationProperties.SetName(_avatarSize, "Размер аватара");
        _avatarPanel.Children.Add(_avatarSize);

        _checkedPanel.Children.Add(new TextBlock { Text = "Галочка (IsChecked)" });
        _checked = new ToggleSwitch { Content = "IsChecked", IsChecked = true };
        _checkedPanel.Children.Add(_checked);
        _avatarPanel.Children.Add(_checkedPanel);

        _badgePanel.Children.Add(new TextBlock { Text = "Значок (MaterialBadge)" });
        _badgeVisible = new ToggleSwitch { Content = "Показывать значок", IsChecked = true };
        _badgePanel.Children.Add(_badgeVisible);
        _badgeText = new TextBox { Text = "3", HorizontalAlignment = HorizontalAlignment.Stretch };
        MaterialTextField.SetVariant(_badgeText, MaterialTextFieldVariant.Outlined);
        AutomationProperties.SetName(_badgeText, "Текст значка");
        _badgePanel.Children.Add(_badgeText);
        _avatarPanel.Children.Add(_badgePanel);

        _interactive = new ToggleSwitch { Content = "Интерактивный (Click, Command)", IsChecked = true };
        Settings.Children.Add(_avatarPanel);
        Settings.Children.Add(_interactive);

        _thumbnailPanel.Children.Add(new TextBlock { Text = "Тип миниатюры" });
        _thumbnailKind = new ComboBox
        {
            ItemsSource = Array.ConvertAll(ThumbnailKinds, kind => kind.ToString()),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_thumbnailKind, "Тип миниатюры");
        _thumbnailPanel.Children.Add(_thumbnailKind);

        _thumbCaption.Text = "Ширина: 160 dp";
        _thumbnailPanel.Children.Add(_thumbCaption);
        _thumbWidth = new Slider { Minimum = 56, Maximum = 240, Value = 160, TickFrequency = 8, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_thumbWidth, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_thumbWidth, false);
        AutomationProperties.SetName(_thumbWidth, "Ширина миниатюры");
        _thumbnailPanel.Children.Add(_thumbWidth);

        _thumbnailPanel.Children.Add(new TextBlock { Text = "Соотношение сторон (AspectRatio)" });
        _ratio = new ComboBox
        {
            ItemsSource = new[] { "Как в источнике", "1:1", "16:9", "4:3" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_ratio, "Соотношение сторон миниатюры");
        _thumbnailPanel.Children.Add(_ratio);

        _cornerCaption.Text = "Скругление: 0 dp";
        _thumbnailPanel.Children.Add(_cornerCaption);
        _corner = new Slider { Minimum = 0, Maximum = 32, Value = 0, TickFrequency = 2, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_corner, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_corner, false);
        AutomationProperties.SetName(_corner, "Скругление миниатюры");
        _thumbnailPanel.Children.Add(_corner);
        _thumbnailPanel.Children.Add(new TextBlock
        {
            Text = "Миниатюра использует фотографию из списка «Фотография (Source)» выше.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });
        Settings.Children.Add(_thumbnailPanel);

        _brandPanel.Children.Add(new TextBlock { Text = "Тип знака" });
        _brandKind = new ComboBox
        {
            ItemsSource = Array.ConvertAll(BrandKinds, kind => kind.ToString()),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_brandKind, "Тип фирменного знака");
        _brandPanel.Children.Add(_brandKind);

        _brandCaption.Text = "Размер: 48 dp";
        _brandPanel.Children.Add(_brandCaption);
        _brandSize = new Slider { Minimum = 18, Maximum = 96, Value = 48, TickFrequency = 6, IsSnapToTickEnabled = true };
        MaterialAssist.SetControlSize(_brandSize, MaterialControlSize.ExtraSmall);
        MaterialSlider.SetShowStops(_brandSize, false);
        AutomationProperties.SetName(_brandSize, "Размер фирменного знака");
        _brandPanel.Children.Add(_brandSize);
        Settings.Children.Add(_brandPanel);

        Settings.Children.Add(new TextBlock
        {
            Text = "Один экземпляр семейства. Avatar: Placeholder/Check/Monogram/Image, Letter, IsChecked и " +
                   "интерактивность; значок — настоящий MaterialBadge поверх аватара. Thumbnail: image/video, " +
                   "AspectRatio и CornerRadius. Brand mark: Colourful logo и Favicon в исходном масштабе 18 dp.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        });

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
        _avatarPanel.IsVisible = family == 0;
        _thumbnailPanel.IsVisible = family == 1;
        _brandPanel.IsVisible = family == 2;

        var avatarKind = AvatarKinds[Math.Clamp(_avatarKind.SelectedIndex, 0, AvatarKinds.Length - 1)];
        var thumbnailKind = ThumbnailKinds[Math.Clamp(_thumbnailKind.SelectedIndex, 0, ThumbnailKinds.Length - 1)];
        var brandKind = BrandKinds[Math.Clamp(_brandKind.SelectedIndex, 0, BrandKinds.Length - 1)];
        IImage? photo = ((family == 0 && avatarKind == MaterialAvatarKind.Image) || family == 1)
                        && _photo.SelectedItem is MaterialAvatarInfo info
            ? info.Image
            : null;

        _letterPanel.IsVisible = avatarKind == MaterialAvatarKind.Monogram;
        _checkedPanel.IsVisible = avatarKind == MaterialAvatarKind.Check;
        _badgePanel.IsVisible = family == 0;
        _interactive.IsVisible = family != 2;

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

        _avatarSizeCaption.Text = $"Размер: {_avatarSize.Value:0} dp";
        _thumbCaption.Text = $"Ширина: {_thumbWidth.Value:0} dp";
        _cornerCaption.Text = $"Скругление: {_corner.Value:0} dp";
        _brandCaption.Text = $"Размер: {_brandSize.Value:0} dp";

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

    private MaterialButtonGroup Choice(string label, string[] options, int selected)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label });
        var group = new MaterialButtonGroup
        {
            Kind = MaterialButtonGroupKind.Connected,
            ButtonShape = MaterialButtonShape.Round,
            ControlSize = MaterialControlSize.Small,
            SelectionMode = MaterialButtonGroupSelectionMode.Single,
            ShowSelectedIcon = true,
        };
        foreach (var option in options)
            group.Items.Add(new MaterialButtonGroupItem { Content = option });
        group.SelectedIndex = selected;
        panel.Children.Add(group);
        Settings.Children.Add(panel);
        return group;
    }
}
