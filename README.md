# CampusAutoLoginWin7

Windows 7 campus-network login helper for the portal detected at `10.26.13.2`.

## What it does

- Reads the account and password from `config.ini` in the same folder as the executable.
- Includes `install-autostart.bat`, which registers automatic startup for the current Windows user. No administrator permission is needed.
- At sign-in, waits for the network and retries the campus login endpoint ten times over about five minutes.
- Reads the portal's current IP and MAC details before it attempts login.

## Build on this Mac

The project targets .NET Framework 4 and has no NuGet or third-party dependency.

```sh
./build-macos.sh
```

The build creates these files in `bin/`:

- `CampusAutoLogin.exe`: the WinForms utility.
- `CampusAutoLoginTests.exe`: protocol parsing and request-building tests.
- `config.ini`: account/password configuration template.
- `install-autostart.bat`: current-user automatic-startup installer.

## Use on the Windows 7 computer

1. Copy `CampusAutoLogin.exe`, `config.ini`, and `install-autostart.bat` together to a folder that the Windows user can keep, such as a folder under Documents. Do not use a temporary download location.
2. Open `config.ini` in Notepad and set the two values:

```ini
[CampusAutoLogin]
Account=your-campus-account
Password=your-campus-password
```

3. Double-click `install-autostart.bat`. It registers the executable under the current user's Windows startup registry key.
4. Run `CampusAutoLogin.exe`, then choose `Save and login now` while connected to the school network to validate the account without rebooting.

`config.ini` stores the password as plaintext. Keep the folder private and do not share or sync this file. The executable can also edit the same INI from its settings window.

## Portal profile verified from this network

- Captive-portal front page: `10.26.13.2`.
- Login service: `10.26.13.2:801/eportal/portal/login`.
- Login type: `login_method=1`.
- Client fields: account/password plus the current portal-provided IP and network adapter MAC address.
- Status probe: `/drcom/chkstatus`.

## Verification boundary

The request shape was extracted from the live portal page and its currently served JavaScript. No real account or password was submitted during investigation. A Windows 7 physical-machine login still needs to be tested after the executable is compiled.
