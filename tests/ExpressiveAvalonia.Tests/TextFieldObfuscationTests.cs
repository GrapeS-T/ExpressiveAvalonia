using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExpressiveAvalonia.Theming;
using Xunit;

namespace ExpressiveAvalonia.Tests;

/// <summary>
/// Source BasicSecureTextField/TextObfuscationMode behavior on the native Material TextBox:
/// the real editing state stays in Text, while the template presenter shows the obfuscated
/// display text and at most one revealed insertion.
/// </summary>
public class TextFieldObfuscationTests
{
    private static Window Show(Control control)
    {
        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static string? PresentedText(TextBox field) =>
        field.GetVisualDescendants().OfType<TextPresenter>().Single().Text;

    [AvaloniaFact]
    public void DefaultModeKeepsNativePasswordMasking()
    {
        var field = new TextBox { Text = "secret", PasswordChar = '\u25cf' };
        var plain = new TextBox { Text = "plain" };
        var window = Show(new StackPanel { Children = { field, plain } });
        try
        {
            Assert.Equal(TextObfuscationMode.System, MaterialTextField.GetTextObfuscationMode(field));
            Assert.Equal("secret", field.Text);
            Assert.Equal("secret", MaterialTextField.GetDisplayText(field));
            Assert.Equal('\u25cf', MaterialTextField.GetEffectivePasswordChar(field));

            // A non-password field keeps the previous visible behavior under the default mode.
            Assert.Equal("plain", MaterialTextField.GetDisplayText(plain));
            Assert.Equal('\0', MaterialTextField.GetEffectivePasswordChar(plain));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ModeSwitchUpdatesTheDisplayWithoutTouchingText()
    {
        var field = new TextBox { Text = "secret", PasswordChar = '\u25cf' };
        var window = Show(field);
        try
        {
            MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.Visible);
            Assert.Equal("secret", MaterialTextField.GetDisplayText(field));
            Assert.Equal('\0', MaterialTextField.GetEffectivePasswordChar(field));

            MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.Hidden);
            Assert.Equal("secret", MaterialTextField.GetDisplayText(field));
            Assert.Equal('\u25cf', MaterialTextField.GetEffectivePasswordChar(field));
            Assert.Equal("secret", field.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RevealLastTypedShowsOnlyTheInsertionAndHidesOnFocusLoss()
    {
        var field = new TextBox { Text = "abc", PasswordChar = '\u25cf' };
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var other = new Button { Content = "Other" };
        var window = Show(new StackPanel { Children = { field, other } });
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;
            window.KeyTextInput("d");

            // Native editing keeps the real text and the reveal is display-only.
            Assert.Equal("abcd", field.Text);
            Assert.Equal("\u25cf\u25cf\u25cfd", MaterialTextField.GetDisplayText(field));
            Assert.Equal("\u25cf\u25cf\u25cfd", PresentedText(field));

            // A second insertion moves the single reveal to the new character.
            window.KeyTextInput("e");
            Assert.Equal("abcde", field.Text);
            Assert.Equal("\u25cf\u25cf\u25cf\u25cfe", MaterialTextField.GetDisplayText(field));

            other.Focus();
            Assert.Equal("abcde", field.Text);
            Assert.Equal("\u25cf\u25cf\u25cf\u25cf\u25cf", MaterialTextField.GetDisplayText(field));
            Assert.Equal("\u25cf\u25cf\u25cf\u25cf\u25cf", PresentedText(field));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task RevealLastTypedHidesAfterTheSourceTimer()
    {
        var field = new TextBox { Text = "ab", PasswordChar = '\u25cf' };
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var window = Show(field);
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;
            window.KeyTextInput("c");
            Assert.Equal("\u25cf\u25cfc", MaterialTextField.GetDisplayText(field));

            // Yield the UI thread so the dispatcher can promote and execute its timed job.
            // RunJobs alone only drains work already queued; sleeping blocks timer promotion.
            await Task.Delay(TimeSpan.FromMilliseconds(1750));

            Assert.Equal("\u25cf\u25cf\u25cf", MaterialTextField.GetDisplayText(field));
            Assert.Equal("abc", field.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RevealLastTypedKeepsTheTextBindingLive()
    {
        var model = new TextModel { Value = "ab" };
        var field = new TextBox { PasswordChar = '\u25cf' };
        field.Bind(TextBox.TextProperty,
            new Binding(nameof(TextModel.Value)) { Source = model, Mode = BindingMode.TwoWay });
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var window = Show(field);
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;
            window.KeyTextInput("c");

            Assert.Equal("abc", model.Value);
            Assert.Equal("abc", field.Text);
            Assert.Equal("\u25cf\u25cfc", MaterialTextField.GetDisplayText(field));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RevealLastTypedIgnoresProgrammaticTextChanges()
    {
        var field = new TextBox { Text = "ab", PasswordChar = '\u25cf' };
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var window = Show(field);
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;

            // A focused, single-character update from application code is not typing.
            field.Text = "abc";
            Assert.Equal("\u25cf\u25cf\u25cf", MaterialTextField.GetDisplayText(field));

            field.Text = "z";
            Assert.Equal("\u25cf", MaterialTextField.GetDisplayText(field));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RevealLastTypedIgnoresMultiCharacterInsertions()
    {
        var field = new TextBox { Text = "ab", PasswordChar = '\u25cf' };
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var window = Show(field);
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;

            // Paste enters through the native edit path as one text block, like this setter.
            field.SelectedText = "cd";
            Assert.Equal("abcd", field.Text);
            Assert.Equal("\u25cf\u25cf\u25cf\u25cf", MaterialTextField.GetDisplayText(field));

            // A multi-character input event is not a single typed character either.
            window.KeyTextInput("ef");
            Assert.Equal("abcdef", field.Text);
            Assert.Equal("\u25cf\u25cf\u25cf\u25cf\u25cf\u25cf",
                MaterialTextField.GetDisplayText(field));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RevealLastTypedHidesOnSettledExpandedSelection()
    {
        var field = new TextBox { Text = "ab", PasswordChar = '\u25cf' };
        MaterialTextField.SetTextObfuscationMode(field, TextObfuscationMode.RevealLastTyped);
        var window = Show(field);
        try
        {
            field.Focus();
            field.CaretIndex = field.Text!.Length;
            window.KeyTextInput("c");
            Assert.Equal("\u25cf\u25cfc", MaterialTextField.GetDisplayText(field));

            // TextBox assigns SelectionStart and SelectionEnd in sequence, so the caret move
            // above must not hide the reveal; a settled expanded selection does.
            field.SelectionStart = 0;
            field.SelectionEnd = 3;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("\u25cf\u25cf\u25cf", MaterialTextField.GetDisplayText(field));
        }
        finally { window.Close(); }
    }

    private sealed class TextModel
    {
        public string? Value { get; set; }
    }
}