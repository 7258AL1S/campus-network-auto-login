using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CampusAutoLogin
{
    public sealed class CampusCredentials
    {
        public CampusCredentials(string userName, string password)
        {
            UserName = userName ?? string.Empty;
            Password = password ?? string.Empty;
        }

        public string UserName { get; private set; }
        public string Password { get; private set; }
    }

    public sealed class PortalStatus
    {
        public PortalStatus(string ipAddress, string macAddress, string serverAddress, bool isOnline)
        {
            IpAddress = ipAddress ?? string.Empty;
            MacAddress = NormalizeMac(macAddress);
            ServerAddress = serverAddress ?? string.Empty;
            IsOnline = isOnline;
        }

        public string IpAddress { get; private set; }
        public string MacAddress { get; private set; }
        public string ServerAddress { get; private set; }
        public bool IsOnline { get; private set; }

        public static string NormalizeMac(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return "000000000000";
            return mac.Replace(":", string.Empty).Replace("-", string.Empty);
        }
    }

    public static class PortalResponseParser
    {
        public static PortalStatus ParseStatus(string response)
        {
            IDictionary<string, object> json = ParseJsonp(response);
            return new PortalStatus(
                GetString(json, "ss5", "v46ip", "v4ip"),
                GetString(json, "ss4", "olmac"),
                GetString(json, "ss6", "v4serip"),
                IsResultSuccess(GetString(json, "result")));
        }

        public static bool IsLoginSuccessful(string response)
        {
            return IsResultSuccess(GetString(ParseJsonp(response), "result"));
        }

        private static bool IsResultSuccess(string result)
        {
            return result == "1" || string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }

        private static IDictionary<string, object> ParseJsonp(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidDataException("The portal response was empty.");
            }

            string payload = response.Trim().TrimStart('\uFEFF');
            int start = payload.IndexOf('(');
            int end = payload.LastIndexOf(')');
            if (start >= 0 && end > start)
            {
                payload = payload.Substring(start + 1, end - start - 1);
            }

            object value;
            try
            {
                value = new JavaScriptSerializer().DeserializeObject(payload.Trim().TrimEnd(';'));
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException("The portal response was not valid JSON or JSONP.", error);
            }

            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null)
            {
                throw new InvalidDataException("The portal response did not contain an object.");
            }

            return result;
        }

        private static string GetString(IDictionary<string, object> values, params string[] names)
        {
            foreach (string name in names)
            {
                object value;
                if (values.TryGetValue(name, out value) && value != null)
                {
                    return Convert.ToString(value);
                }
            }

            return string.Empty;
        }
    }

    public sealed class LoginResult
    {
        public LoginResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
    }

    public static class PortalNavigation
    {
        public const string HomeUrl = "http://10.26.13.2/";

        public static bool ShouldOpenAfter(LoginResult result)
        {
            return result != null && result.Succeeded;
        }
    }

    public sealed class CampusPortalClient
    {
        public const string DefaultServerAddress = "10.26.13.2";
        private const int PortalPort = 801;
        private const string CallbackName = "CampusAutoLogin";

        public LoginResult TryLogin(CampusCredentials credentials)
        {
            try
            {
                PortalStatus status = ProbeStatus();
                if (status.IsOnline)
                {
                    return new LoginResult(true, "Already online.");
                }

                if (string.IsNullOrEmpty(status.IpAddress) || status.IpAddress == "000.000.000.000")
                {
                    return new LoginResult(false, "The portal did not provide a usable campus IP address.");
                }

                if (IsUnknownMac(status.MacAddress))
                {
                    status = new PortalStatus(status.IpAddress, MacResolver.FindForIp(status.IpAddress), status.ServerAddress, false);
                }

                string response = Get(BuildLoginUri(status, credentials, CallbackName));
                return PortalResponseParser.IsLoginSuccessful(response)
                    ? new LoginResult(true, "Portal login succeeded.")
                    : new LoginResult(false, "The portal rejected the login. Check the account, password, or campus-network policy.");
            }
            catch (Exception error)
            {
                return new LoginResult(false, error.Message);
            }
        }

        public PortalStatus ProbeStatus()
        {
            string response = Get(new Uri("http://" + DefaultServerAddress + "/drcom/chkstatus?callback=" + CallbackName + "&jsVersion=4.X"));
            PortalStatus status = PortalResponseParser.ParseStatus(response);
            if (string.IsNullOrEmpty(status.ServerAddress))
            {
                return new PortalStatus(status.IpAddress, status.MacAddress, DefaultServerAddress, status.IsOnline);
            }

            return status;
        }

        public static Uri BuildLoginUri(PortalStatus status, CampusCredentials credentials, string callback)
        {
            string server = string.IsNullOrEmpty(status.ServerAddress) ? DefaultServerAddress : status.ServerAddress;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("login_method", "1");
            parameters.Add("user_account", credentials.UserName);
            parameters.Add("user_password", credentials.Password);
            parameters.Add("wlan_user_ip", status.IpAddress);
            parameters.Add("wlan_user_ipv6", string.Empty);
            parameters.Add("wlan_user_mac", status.MacAddress);
            parameters.Add("wlan_ac_ip", string.Empty);
            parameters.Add("wlan_ac_name", string.Empty);
            parameters.Add("terminal_type", "1");
            parameters.Add("lang", "zh");
            parameters.Add("jsVersion", "4.X");
            parameters.Add("callback", callback);
            return new Uri("http://" + server + ":" + PortalPort + "/eportal/portal/login?" + ToQuery(parameters));
        }

        private static bool IsUnknownMac(string mac)
        {
            return string.IsNullOrEmpty(mac) || mac == "000000000000" || mac == "111111111111";
        }

        private static string ToQuery(Dictionary<string, string> values)
        {
            StringBuilder query = new StringBuilder();
            foreach (KeyValuePair<string, string> item in values)
            {
                if (query.Length > 0) query.Append('&');
                query.Append(Uri.EscapeDataString(item.Key));
                query.Append('=');
                query.Append(Uri.EscapeDataString(item.Value ?? string.Empty));
            }

            return query.ToString();
        }

        private static string Get(Uri requestUri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = "GET";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.UserAgent = "CampusAutoLoginWin7/1.0";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, ResolveEncoding(response)))
            {
                return reader.ReadToEnd();
            }
        }

        private static Encoding ResolveEncoding(HttpWebResponse response)
        {
            try
            {
                return string.IsNullOrEmpty(response.CharacterSet) ? Encoding.UTF8 : Encoding.GetEncoding(response.CharacterSet);
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8;
            }
        }
    }

    internal static class MacResolver
    {
        public static string FindForIp(string ipAddress)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && address.Address.ToString() == ipAddress)
                    {
                        return PortalStatus.NormalizeMac(adapter.GetPhysicalAddress().ToString());
                    }
                }
            }

            return "000000000000";
        }
    }

    public sealed class SavedSettings
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public static class IniSettings
    {
        public static SavedSettings Parse(string content)
        {
            SavedSettings settings = new SavedSettings();
            if (content == null) return settings;

            bool hasSection = false;
            bool inCampusAutoLoginSection = true;
            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        hasSection = true;
                        inCampusAutoLoginSection = string.Equals(
                            trimmed.Substring(1, trimmed.Length - 2),
                            "CampusAutoLogin",
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (hasSection && !inCampusAutoLoginSection) continue;
                    int separator = line.IndexOf('=');
                    if (separator < 0) continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1);
                    if (string.Equals(key, "Account", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.UserName = value.Trim();
                    }
                    else if (string.Equals(key, "Password", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.Password = value;
                    }
                }
            }

            return settings;
        }

        public static string Serialize(SavedSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            ValidateValue(settings.UserName, "Account");
            ValidateValue(settings.Password, "Password");
            return "[CampusAutoLogin]\r\nAccount=" + (settings.UserName ?? string.Empty) + "\r\nPassword=" + (settings.Password ?? string.Empty) + "\r\n";
        }

        private static void ValidateValue(string value, string name)
        {
            if (value != null && (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0))
            {
                throw new InvalidDataException(name + " cannot contain a line break.");
            }
        }
    }

    public static class SettingsStore
    {
        public static readonly string ConfigFilePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "config.ini");

        public static SavedSettings Load()
        {
            if (!File.Exists(ConfigFilePath)) return null;
            return IniSettings.Parse(File.ReadAllText(ConfigFilePath, Encoding.UTF8));
        }

        public static void Save(SavedSettings settings)
        {
            File.WriteAllText(ConfigFilePath, IniSettings.Serialize(settings), Encoding.UTF8);
        }
    }

    public static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CampusAutoLoginWin7";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                {
                    string executable = Assembly.GetExecutingAssembly().Location;
                    key.SetValue(ValueName, "\"" + executable + "\" --silent");
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    public static class LoginRunner
    {
        private static readonly int[] RetryDelaysSeconds = { 0, 5, 10, 20, 30, 60, 60, 60, 60, 60 };

        public static LoginResult RunWithRetries(Action<string> report)
        {
            SavedSettings settings = SettingsStore.Load();
            if (settings == null || string.IsNullOrEmpty(settings.UserName) || string.IsNullOrEmpty(settings.Password))
            {
                return new LoginResult(false, "No saved account is configured.");
            }

            LoginResult last = null;
            for (int attempt = 0; attempt < RetryDelaysSeconds.Length; attempt++)
            {
                if (RetryDelaysSeconds[attempt] > 0) Thread.Sleep(RetryDelaysSeconds[attempt] * 1000);
                report("Login attempt " + (attempt + 1) + " of " + RetryDelaysSeconds.Length + ".");
                last = new CampusPortalClient().TryLogin(new CampusCredentials(settings.UserName, settings.Password));
                if (last.Succeeded) return last;
                report(last.Message);
            }

            return last ?? new LoginResult(false, "No login attempt was made.");
        }
    }
}
