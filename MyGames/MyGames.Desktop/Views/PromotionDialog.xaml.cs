using MyGames.Desktop.Controls;
using System.Windows;
using System.Windows.Controls;

namespace ChessApp.Views
{
    public partial class PromotionDialog : Window
    {

        //// ký tự UCI cho promotion: q, r, b, n
        public char SelectedPiece { get; private set; }

        public PromotionDialog(bool isWhite)
        {
            InitializeComponent();

            var innerButtonPanel = this.FindName("InnerButtonPanel") as StackPanel;
            if (innerButtonPanel == null)
            {
                MessageBox.Show("InnerButtonPanel not found!");
                return;
            }

            string prefix = isWhite ? "w" : "b";
            foreach (var child in innerButtonPanel.Children.OfType<Button>())
            {
                if (child.Tag is string tag)
                {
                    var content = child.Content;
                    if (content is SvgSkiaView skiaView)
                    {
                        try
                        {
                            var uri = new Uri($"pack://application:,,,/Resources/Pieces/{prefix}_{PieceName(tag)}.svg");
                            skiaView.Source = uri;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error loading SVG for {tag}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private string PieceName(string tag) => tag switch
        {
            "q" => "queen",
            "r" => "rook",
            "b" => "bishop",
            "n" => "knight",
            _ => "queen"
        };

        private void OnPieceClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                SelectedPiece = tag[0];
                DialogResult = true;
            }
        }
    }
}
