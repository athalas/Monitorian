using System.Windows;

namespace Monitorian.Core.Views
{
    public partial class InputDialog : Window
    {
        public string ResponseText => ResponseTextBox.Text;

        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            this.Title = title;
            PromptText.Text = prompt;
            ResponseTextBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
