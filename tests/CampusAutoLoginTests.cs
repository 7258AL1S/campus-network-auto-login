using System;
using CampusAutoLogin;

internal static class CampusAutoLoginTests
{
    private static int failures;

    private static void Main()
    {
        Run("Parses the local Dr.COM status envelope", ParsesStatusEnvelope);
        Run("Builds the portal login request with encoded credentials", BuildsLoginRequest);
        Run("Recognizes portal success responses", RecognizesLoginSuccess);
        Run("Recognizes plain JSON portal responses", RecognizesPlainJsonLoginSuccess);
        Run("Opens the portal only after a successful login", OpensPortalOnlyAfterSuccessfulLogin);
        Run("Parses plaintext config.ini credentials", ParsesIniSettings);

        if (failures != 0)
        {
            Environment.Exit(1);
        }
    }

    private static void ParsesStatusEnvelope()
    {
        const string body = "callback({\"result\":0,\"ss5\":\"10.229.220.238\",\"ss4\":\"0010f384fc27\",\"ss6\":\"10.26.13.2\"})";
        PortalStatus status = PortalResponseParser.ParseStatus(body);

        Equal(false, status.IsOnline);
        Equal("10.229.220.238", status.IpAddress);
        Equal("0010f384fc27", status.MacAddress);
        Equal("10.26.13.2", status.ServerAddress);
    }

    private static void BuildsLoginRequest()
    {
        PortalStatus status = new PortalStatus("10.229.220.238", "0010f384fc27", "10.26.13.2", false);
        Uri request = CampusPortalClient.BuildLoginUri(
            status,
            new CampusCredentials("student+one", "a b&c"),
            "CampusAutoLogin");

        Equal("http", request.Scheme);
        Equal("10.26.13.2", request.Host);
        Equal(801, request.Port);
        Equal("/eportal/portal/login", request.AbsolutePath);
        Contains(request.Query, "login_method=1");
        Contains(request.Query, "user_account=student%2Bone");
        Contains(request.Query, "user_password=a%20b%26c");
        Contains(request.Query, "wlan_user_ip=10.229.220.238");
        Contains(request.Query, "wlan_user_mac=0010f384fc27");
        Contains(request.Query, "terminal_type=1");
        Contains(request.Query, "callback=CampusAutoLogin");
    }

    private static void RecognizesLoginSuccess()
    {
        Equal(true, PortalResponseParser.IsLoginSuccessful("CampusAutoLogin({\"result\":1,\"msg\":\"ok\"})"));
        Equal(true, PortalResponseParser.IsLoginSuccessful("CampusAutoLogin({\"result\":\"ok\"})"));
        Equal(false, PortalResponseParser.IsLoginSuccessful("CampusAutoLogin({\"result\":0,\"msg\":\"bad password\"})"));
    }

    private static void RecognizesPlainJsonLoginSuccess()
    {
        Equal(true, PortalResponseParser.IsLoginSuccessful("{\"result\":1,\"msg\":\"ok\"}"));
        Equal(false, PortalResponseParser.IsLoginSuccessful("{\"result\":0,\"msg\":\"bad password\"}"));
    }

    private static void OpensPortalOnlyAfterSuccessfulLogin()
    {
        Equal("http://10.26.13.2/", PortalNavigation.HomeUrl);
        Equal(true, PortalNavigation.ShouldOpenAfter(new LoginResult(true, "ok")));
        Equal(false, PortalNavigation.ShouldOpenAfter(new LoginResult(false, "bad password")));
    }

    private static void ParsesIniSettings()
    {
        SavedSettings settings = IniSettings.Parse("[CampusAutoLogin]\r\nAccount=student01\r\nPassword=a=b&c\r\n");

        Equal("student01", settings.UserName);
        Equal("a=b&c", settings.Password);
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
            failures++;
            Console.Error.WriteLine("FAIL " + name + ": " + error.Message);
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            throw new InvalidOperationException("Expected '" + expected + "' but got '" + actual + "'.");
        }
    }

    private static void Contains(string text, string expected)
    {
        if (text.IndexOf(expected, StringComparison.Ordinal) < 0)
        {
            throw new InvalidOperationException("Expected '" + text + "' to contain '" + expected + "'.");
        }
    }
}
