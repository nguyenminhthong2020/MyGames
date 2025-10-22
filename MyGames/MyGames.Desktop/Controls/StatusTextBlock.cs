using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MyGames.Desktop.Controls  // ← Quan trọng!
{
    public class StatusTextBlock : TextBlock
    {
        public static readonly DependencyProperty StatusMessageProperty =
            DependencyProperty.Register(nameof(StatusMessage), typeof(string),
                typeof(StatusTextBlock), new PropertyMetadata("", OnStatusMessageChanged));

        public string StatusMessage
        {
            get => (string)GetValue(StatusMessageProperty);
            set => SetValue(StatusMessageProperty, value);
        }

        private static void OnStatusMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var block = (StatusTextBlock)d;
            block.UpdateColor((string)e.NewValue);
        }

        private void UpdateColor(string message)
        {
            Inlines.Clear();
            var (type, text) = ParseMessage(message);
            var run = new Run(text);

            switch (type)
            {
                case "ERROR": run.Foreground = Brushes.Red; break;
                case "WARN": run.Foreground = Brushes.Orange; break;
                case "OK": run.Foreground = Brushes.Green; break;
                default: run.Foreground = Brushes.Blue; break;
            }

            Inlines.Add(run);
        }

        private (string type, string text) ParseMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return ("", "");
            var parts = message.Split("::", 2);
            return parts.Length == 2 ? (parts[0], parts[1]) : ("", message);
        }
    }
}