using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;

namespace StratisFederationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string passPhrase = null;
        private string dataDir = null;
        private const string DefaulfPassText = "Enter Passphrase Here";

        public MainWindow()
        {
            InitializeComponent();
            dataDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            TextBoxPassphrase.Text = DefaulfPassText;
            TextBoxPassphrase.Foreground = Brushes.Gray;
            TextBoxDir.Text = dataDir;
        }

        private void ButtonQuit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }


        private void ButtonmMinimise_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (passPhrase == null)
            {
                TextBoxPassphrase.Foreground = Brushes.Gray;
                TextBoxPassphrase.Text = DefaulfPassText;
                TextBoxMainOutput.Text = null;
                System.Windows.Forms.MessageBox.Show("Please enter pass phrase", "Warning",  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string sdtOut = null;
            string dropDownText = MainDrop.Text;
            string isMultiSig = dropDownText.StartsWith("Fed") ? "false" : "true";

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", $"/c dotnet netcoreapp2.1/FederationSetup.dll p -passphrase={passPhrase} -datadir={dataDir} -ismultisig={isMultiSig}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();

            sdtOut = proc.StandardOutput.ReadToEnd();

            proc.WaitForExit(3500);

            TextBoxMainOutput.Text = sdtOut;
        }

        private void ButtonGenKeys_ContextMenuClosing(object sender, ContextMenuEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TextBoxPassphrase_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(TextBoxPassphrase.Text) && TextBoxPassphrase.Text != DefaulfPassText)
                passPhrase = TextBoxPassphrase.Text;
            else
                passPhrase = null;
        }

        private void ButtonDirectorySelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            dataDir = dialog.SelectedPath;
            TextBoxDir.Text = dataDir;
        }

        private void TextBoxPassphrase_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TextBoxPassphrase.Text == DefaulfPassText)
            {
                TextBoxPassphrase.Text = null;
                TextBoxPassphrase.Foreground = Brushes.Black;
                passPhrase = null;
            }
        }
    }
}
