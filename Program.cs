using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CampusAutoLogin
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArgument(args, "--silent"))
            {
                OpenPortalHomeAfterSuccessfulLogin(LoginRunner.RunWithRetries(delegate(string ignored) { }));
                return;
            }

            if (HasArgument(args, "--run-once"))
            {
                LoginResult result = LoginRunner.RunWithRetries(delegate(string message) { Console.WriteLine(message); });
                OpenPortalHomeAfterSuccessfulLogin(result);
                Environment.ExitCode = result.Succeeded ? 0 : 1;
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SettingsForm());
        }

        internal static void OpenPortalHomeAfterSuccessfulLogin(LoginResult result)
        {
            if (!PortalNavigation.ShouldOpenAfter(result)) return;

            try
            {
                Process.Start(PortalNavigation.HomeUrl);
            }
            catch
            {
                // A browser launch failure must not change a successful login result.
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly TextBox userName = new TextBox();
        private readonly TextBox password = new TextBox();
        private readonly CheckBox launchAtLogon = new CheckBox();
        private readonly Button save = new Button();
        private readonly Button loginNow = new Button();
        private readonly Label status = new Label();

        public SettingsForm()
        {
            Text = "Campus Auto Login";
            ClientSize = new Size(430, 230);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            AddLabel("Account", 24, 25);
            userName.SetBounds(120, 22, 270, 24);
            userName.MaxLength = 64;
            Controls.Add(userName);

            AddLabel("Password", 24, 65);
            password.SetBounds(120, 62, 270, 24);
            password.MaxLength = 128;
            password.UseSystemPasswordChar = true;
            Controls.Add(password);

            launchAtLogon.Text = "Start automatically when I sign in";
            launchAtLogon.SetBounds(120, 100, 270, 24);
            Controls.Add(launchAtLogon);

            save.Text = "Save";
            save.SetBounds(120, 136, 120, 30);
            save.Click += SaveClicked;
            Controls.Add(save);

            loginNow.Text = "Save and login now";
            loginNow.SetBounds(250, 136, 140, 30);
            loginNow.Click += LoginNowClicked;
            Controls.Add(loginNow);

            status.SetBounds(24, 180, 366, 36);
            status.AutoEllipsis = true;
            Controls.Add(status);

            LoadSettings();
        }

        private void AddLabel(string text, int left, int top)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(left, top, 85, 24);
            label.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(label);
        }

        private void LoadSettings()
        {
            try
            {
                SavedSettings settings = SettingsStore.Load();
                if (settings != null)
                {
                    userName.Text = settings.UserName;
                    password.Text = settings.Password;
                }

                launchAtLogon.Checked = StartupRegistration.IsEnabled();
            }
            catch (Exception error)
            {
                status.Text = "Could not load saved settings: " + error.Message;
            }
        }

        private bool SaveSettings()
        {
            if (string.IsNullOrWhiteSpace(userName.Text) || string.IsNullOrEmpty(password.Text))
            {
                status.Text = "Enter both an account and a password.";
                return false;
            }

            try
            {
                SettingsStore.Save(new SavedSettings { UserName = userName.Text.Trim(), Password = password.Text });
                StartupRegistration.SetEnabled(launchAtLogon.Checked);
                status.Text = "Saved to config.ini next to the executable.";
                return true;
            }
            catch (Exception error)
            {
                status.Text = "Could not save settings: " + error.Message;
                return false;
            }
        }

        private void SaveClicked(object sender, EventArgs args)
        {
            SaveSettings();
        }

        private void LoginNowClicked(object sender, EventArgs args)
        {
            if (!SaveSettings()) return;

            save.Enabled = false;
            loginNow.Enabled = false;
            status.Text = "Checking the campus portal...";
            CampusCredentials credentials = new CampusCredentials(userName.Text.Trim(), password.Text);
            Thread worker = new Thread(delegate() { LoginOnce(credentials); });
            worker.IsBackground = true;
            worker.Start();
        }

        private void LoginOnce(CampusCredentials credentials)
        {
            LoginResult result = new CampusPortalClient().TryLogin(credentials);
            Program.OpenPortalHomeAfterSuccessfulLogin(result);
            BeginInvoke((MethodInvoker)delegate
            {
                status.Text = result.Message;
                save.Enabled = true;
                loginNow.Enabled = true;
            });
        }
    }
}
