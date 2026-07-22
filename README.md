# uJump

A minimal, single-purpose WPF launcher for connecting to jumphosts. RDP sessions
are **embedded** using the Microsoft RDP ActiveX control (`mstscax.dll`), which
gives proper live smart-sizing the standalone `mstsc` window handles poorly.

## How it works

- Connections live in `connections.json` next to the executable — edit it by hand
  to add jumphosts. On first run it's seeded from `connections.example.json`.
  `connections.json` is personal and git-ignored; the tracked example is the
  template.
- The launcher is just a **server picker** (defaults to the last-connected one)
  and two buttons:
  - **Windowed** — opens the session in a normal resizable window.
  - **Full screen** — opens borderless, sized to the desktop **work area** so
    your local taskbar stays visible and the remote taskbar stacks above it. No
    visible UI otherwise.
- **Auto-hide control bar** — in a session, move the mouse to the **top edge** and
  a bar drops down with:
  - **Smart resize** — scale the remote desktop to fit the window, *live*, no
    reconnect. Toggle any time.
  - **Full screen / Windowed** — switch modes without dropping the session.
  - **Close** — end the session and return to the launcher.
- Closing or losing the session returns you to the launcher.
- **Credentials manage themselves. No passwords are ever stored by uJump.**
  Sessions use CredSSP/NLA, so Windows automatically offers your current logon
  and any credential you've saved for that host (Windows Credential Manager,
  keyed `TERMSRV/<host>`). You're only prompted when nothing matches — and if you
  tick "remember me" at that prompt, Windows (not uJump) saves it. Only the
  username is pre-filled from `connections.json`.

The last-connected server is remembered in `%APPDATA%\uJump\state.json`.

## Requirements

- Windows with the built-in Remote Desktop client (`mstscax.dll`, present by
  default).
- .NET 8 SDK to build. No COM interop assemblies are generated — the ActiveX
  control is hosted via a custom `AxHost` and driven through its `IDispatch`
  interface, so a plain `dotnet build` works.

## Build & run

```powershell
dotnet build
dotnet run
# or run the built exe:
.\bin\Debug\net8.0-windows\uJump.exe
```

## connections.json

Copy `connections.example.json` to `connections.json` (or just run the app once
to have it seeded automatically), then edit:

```json
{
  "Connections": [
    {
      "Name": "Prod Jumphost",
      "Host": "jump01.example.com",
      "Port": 3389,
      "Username": "DOMAIN\\your.user",
      "Gateway": "gateway.example.com",
      "SmartSizing": true
    }
  ]
}
```

| Field         | Required | Default | Notes                                    |
|---------------|----------|---------|------------------------------------------|
| `Name`        | no       | Host    | Shown in the picker                      |
| `Host`        | yes      | —       | Hostname or IP                           |
| `Port`        | no       | 3389    | RDP port                                 |
| `Username`    | no       | —       | Pre-filled login, e.g. `DOMAIN\user`     |
| `Gateway`     | no       | —       | RD Gateway hostname                      |
| `SmartSizing` | no       | true    | Start with scale-to-fit on               |

Edit the file and restart the launcher (or reopen it) to pick up changes.
