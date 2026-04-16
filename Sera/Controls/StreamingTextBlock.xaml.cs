using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Sera.Controls;

public sealed partial class StreamingTextBlock : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StreamingTextBlock), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsStreamingProperty =
        DependencyProperty.Register(nameof(IsStreaming), typeof(bool), typeof(StreamingTextBlock), new PropertyMetadata(false, OnIsStreamingChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    public StreamingTextBlock()
    {
        this.InitializeComponent();
    }

    public void AppendText(string token)
    {
        Text += token;
    }

    public void Clear()
    {
        Text = string.Empty;
        IsStreaming = false;
    }

    private static void OnIsStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (StreamingTextBlock)d;
        var isStreaming = (bool)e.NewValue;

        if (isStreaming)
        {
            VisualStateManager.GoToState(control, "Streaming", true);
        }
        else
        {
            VisualStateManager.GoToState(control, "Normal", true);
        }
    }
}
