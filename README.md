# 校园网自动登录工具（Windows 7）

适用于 `10.26.13.2` 校园网认证页面的 Windows 7 自动登录工具。

## 功能

- 从程序同目录的 `config.ini` 读取校园网账号和密码。
- 提供 `install-autostart.bat`，双击一次即可为当前 Windows 用户设置登录后自动启动，无需管理员权限。
- Windows 登录后等待网络可用，若未认证会在约五分钟内重试登录十次。
- 登录前自动读取认证服务器返回的 IP 信息，并尝试匹配本机网卡 MAC 地址。

## 在 macOS 上构建和测试

项目面向 .NET Framework 4，不依赖 NuGet 或其他第三方库。macOS 上安装 Mono 后执行：

```sh
./build-macos.sh
```

构建结果在 `bin/` 目录：

- `CampusAutoLogin.exe`：可在 Windows 7 上运行的图形界面程序。
- `CampusAutoLoginTests.exe`：协议解析、请求构造和配置文件解析测试。
- `config.ini`：账号密码配置模板。
- `install-autostart.bat`：当前 Windows 用户的开机自启动安装脚本。

运行测试：

```sh
mono bin/CampusAutoLoginTests.exe
```

## Windows 7 使用方法

1. 将 `CampusAutoLogin.exe`、`config.ini`、`install-autostart.bat` 一起复制到不会移动的目录，例如“文档”下的专用文件夹。不要放在临时下载目录。
2. 使用记事本编辑 `config.ini`，填入账号与密码：

```ini
[CampusAutoLogin]
Account=你的校园网账号
Password=你的校园网密码
```

3. 双击运行 `install-autostart.bat`。脚本会在当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 中注册程序。
4. 连接学校网络后，可先手动运行 `CampusAutoLogin.exe`，点击“保存并立即登录”验证账号；成功后无需重启即可确认配置。

自启动会在 Windows 用户登录桌面后执行，不是在 Windows 登录界面之前执行。

## 安全说明

`config.ini` 中的密码为明文。请不要共享、同步或上传填有真实密码的配置文件，并限制该目录只由本人访问。

## 已确认的认证页面参数

- 认证首页：`10.26.13.2`
- 登录服务：`10.26.13.2:801/eportal/portal/login`
- 登录方式：`login_method=1`
- 登录参数：账号、密码，以及认证页返回的当前 IP 和本机网卡 MAC 地址
- 登录状态查询：`/drcom/chkstatus`

## 验证范围

登录请求格式来自对当前认证页面和其 JavaScript 的分析；分析期间未提交真实账号密码。macOS 已完成构建和自动化逻辑测试，仍需在实际 Windows 7 电脑及校园网环境中进行一次真实登录验证。
