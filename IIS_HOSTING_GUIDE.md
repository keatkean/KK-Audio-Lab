# KK Audio Lab — Complete IIS Hosting Guide (Windows 11)

This guide explains step by step how to host both the **KK Audio Lab Backend (ASP.NET Core 10 Web API)** and **Frontend (Blazor WebAssembly .NET 10)** in **Internet Information Services (IIS)** on **Windows 11**.

---

## Architecture Overview

In IIS, the two projects run as **two separate IIS Sites** matching the project's default ports:

| Component | Project | Hosting Model | Default IIS URL |
|---|---|---|---|
| **Backend** | `Backend/Backend.csproj` | ASP.NET Core Module v2 (**In-Process** inside `w3wp.exe`) | `http://localhost:5100` |
| **Frontend** | `Frontend/Frontend.csproj` | Static Blazor WebAssembly files + **URL Rewrite** SPA fallback | `http://localhost:5200` |

---

## Prerequisites (Windows 11)

Before starting, ensure you have:

1. **Administrator privileges** on your Windows 11 laptop (needed to enable IIS, install IIS modules, configure sites, and set folder permissions).
2. **.NET 10 SDK** (for running `dotnet publish`; both `Backend/Backend.csproj` and `Frontend/Frontend.csproj` target `net10.0`).
3. **.NET 10 Hosting Bundle (x64)** — **Required even if the .NET 10 SDK is already installed.** The SDK does *not* install the **ASP.NET Core Module v2 (ANCM)** into IIS.
   - Download from Microsoft's official .NET 10 Downloads page under **ASP.NET Core Runtime 10.x → Hosting Bundle** (`dotnet-hosting-10.x.x-win.exe`).
   - **Install this *after* enabling IIS** (Step 2).
4. **IIS URL Rewrite Module 2.1 (x64)** — Required so refreshing Blazor client-side routes (e.g., `/playlists`, `/manage`, `/auth`) does not return an IIS `404`.
   - Download **URL Rewrite Module 2.1** from Microsoft IIS Downloads (`rewrite_amd64_en-US.msi`).
5. **Ports `5100` and `5200` available** — Ensure any dev processes from `run.bat`, `dotnet run`, or Visual Studio are stopped before starting the IIS sites.

---

## Step 1: Enable IIS on Windows 11

Choose either the GUI method or the PowerShell method below.

### Option A: Using Windows Features GUI
1. Press `Win + R`, type `optionalfeatures`, and press **Enter**.
2. Expand **Internet Information Services** and check the following:
   - **Web Management Tools** → **IIS Management Console**
   - **World Wide Web Services** → **Common HTTP Features**:
     - **Static Content**
     - **Default Document**
     - **HTTP Errors**
   - **World Wide Web Services** → **Performance Features**:
     - **Static Content Compression**
   - **World Wide Web Services** → **Security**:
     - **Request Filtering**
3. Click **OK** and wait for Windows to complete the installation.

### Option B: Using PowerShell (Run as Administrator)
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName `
  IIS-WebServerRole, `
  IIS-WebServer, `
  IIS-CommonHttpFeatures, `
  IIS-StaticContent, `
  IIS-DefaultDocument, `
  IIS-HttpErrors, `
  IIS-ManagementConsole, `
  IIS-RequestFiltering, `
  IIS-HttpCompressionStatic -All
```

### Verify IIS
Open a browser and go to `http://localhost`. You should see the default IIS welcome page.

---

## Step 2: Install URL Rewrite & .NET 10 Hosting Bundle

> **Order matters:** Enable IIS first (Step 1), then install these modules so they register cleanly into IIS.

1. Install **IIS URL Rewrite Module 2.1** (`rewrite_amd64_en-US.msi`).
2. Install **.NET 10 Hosting Bundle** (`dotnet-hosting-10.x.x-win.exe`).
3. Open **PowerShell** or **Command Prompt** **as Administrator** and run:
   ```cmd
   iisreset
   ```
4. Verify in IIS Manager:
   - Press `Win + R`, type `inetmgr`, and press **Enter**.
   - Click your computer name in the left **Connections** pane, then double-click **Modules**.
   - Confirm both **`AspNetCoreModuleV2`** and **`RewriteModule`** are listed.

---

## Step 3: Code Adjustments Before Publishing

### 1. Remove `UseUrls` from `Backend/Program.cs` *(Already Applied)*
`builder.WebHost.UseUrls("http://localhost:5100");` has been removed from `Backend/Program.cs` so IIS / ASP.NET Core Module v2 manages the binding and port when hosted in IIS (leaving `UseUrls(...)` in `Program.cs` conflicts with IIS in-process hosting).
- Your local `dotnet run` / Visual Studio development workflow continues using port `5100` via `Backend/Properties/launchSettings.json` (`"applicationUrl": "http://localhost:5100"`).

### 2. Dynamic LAN IP & CORS Configuration *(Already Applied)*
Both `localhost` and LAN IP access (e.g., from your phone on the same Wi-Fi) work automatically as long as Backend is on port **`5100`** and Frontend is on port **`5200`**:
- `Backend/Program.cs`: `AllowBlazorFrontend` CORS policy allows any `http://` or `https://` origin on port `5200` (`localhost`, LAN IP, or machine hostname).
- `Frontend/Program.cs`: `HttpClient.BaseAddress` is derived dynamically from `builder.HostEnvironment.BaseAddress` with port `5100`.
- `Frontend/Components/AudioPlayerBar.razor` & `Frontend/Pages/Auth.razor`: Use the injected `HttpClient.BaseAddress` for audio streaming, track prefetching, and UI display.

If you decide to use different ports than `5100` and `5200`, update the port numbers in `Backend/Program.cs` (`uri.Port == 5200`) and `Frontend/Program.cs` (`Port = 5100`) before publishing.

---

## Step 4: Publish Both Projects & Copy to IIS

Publish both projects into their local `bin\Release\net10.0\publish\` folders first (this does **not** require running Visual Studio as Administrator), then copy the published files into `C:\inetpub\wwwroot\`.

### Option A: Using Visual Studio 2026
1. **Publish Backend:**
   - In **Solution Explorer**, right-click the **`Backend`** project → click **Publish...**.
   - Select **Target**: **Folder** → **Next** → **Specific target**: **Folder** → **Next**.
   - Set **Folder location** to `bin\Release\net10.0\publish\` → click **Finish**.
   - Verify settings (`Release`, `net10.0`, `Framework-dependent`, `Portable`) and click **Publish**.

2. **Publish Frontend:**
   - In **Solution Explorer**, right-click the **`Frontend`** project → click **Publish...**.
   - Select **Target**: **Folder** → **Next** → **Specific target**: **Folder** → **Next**.
   - Set **Folder location** to `bin\Release\net10.0\browser-wasm\publish\` → click **Finish**.
   - Verify settings (`Release`, `net10.0`) and click **Publish**.

### Option B: Using Command Line (`dotnet publish`)
Open a terminal in the repository root (`C:\SourceCode\KK-Audio-Lab`) and run:

1. **Publish Backend:**
   ```powershell
   dotnet publish Backend\Backend.csproj -c Release -o Backend\bin\Release\net10.0\publish
   ```

2. **Publish Frontend:**
   ```powershell
   dotnet publish Frontend\Frontend.csproj -c Release -o Frontend\bin\Release\net10.0\publish
   ```

### Manually Copy Published Output to `C:\inetpub\wwwroot\`
*(Confirm UAC Administrator prompt if copying in File Explorer, or use an Administrator PowerShell.)*

1. **Copy Backend files:**
   - Copy all contents of `Backend\bin\Release\net10.0\publish\` → `C:\inetpub\wwwroot\kkaudiolab-api\`

2. **Copy Frontend files:**
   - Copy all contents of `Frontend\bin\Release\net10.0\browser-wasm\publish` (including the `wwwroot` subfolder) → `C:\inetpub\wwwroot\kkaudiolab-web\`
   - > **Important for Blazor WebAssembly:** The deployable static website files are inside `C:\inetpub\wwwroot\kkaudiolab-web\wwwroot` (which contains `index.html` and `_framework/`). Point the IIS Frontend site to that `wwwroot` subfolder in Step 6. *(If you instead copy only the inner contents of `publish\wwwroot\` directly into `C:\inetpub\wwwroot\kkaudiolab-web\`, point Step 6 to `C:\inetpub\wwwroot\kkaudiolab-web`.)*

3. **Copy Existing `AudioLibrary` & `kkaudiolab.db` (Optional):**
   `Backend/Backend.csproj` does not mark `AudioLibrary` or `kkaudiolab.db` to copy on publish.
   - To keep your existing tracks and database data in IIS, copy:
     - `Backend\AudioLibrary` → `C:\inetpub\wwwroot\kkaudiolab-api\AudioLibrary`
     - `Backend\kkaudiolab.db` → `C:\inetpub\wwwroot\kkaudiolab-api\kkaudiolab.db`
   - If you skip this, `DbSeeder.Seed(...)` in `Backend/Program.cs` will create a fresh `kkaudiolab.db` and `AudioLibrary` folder on first request.

---

## Step 5: Configure the Backend Site in IIS

### 1. Create the Backend Application Pool
1. Open **IIS Manager** (`inetmgr`).
2. Right-click **Application Pools** → **Add Application Pool...**
   - **Name**: `KKAudioLabApiPool`
   - **.NET CLR version**: **No Managed Code** *(critical for .NET 10)*
   - **Managed pipeline mode**: **Integrated**
3. Select `KKAudioLabApiPool` and click **Advanced Settings...** in the right pane:
   - Under **Recycling**, set **Disable Overlapped Recycle** = **`True`** *(prevents two `w3wp.exe` processes from accessing `kkaudiolab.db` concurrently during an app pool recycle)*.
   - Under **Process Model**, confirm **Maximum Worker Processes** = **`1`**.

### 2. Grant Folder Write Permissions (Critical for LiteDB & Track Uploads)
The Backend writes to `kkaudiolab.db` and `AudioLibrary` using relative paths configured in `Backend/appsettings.json`. Run this in an **Administrator** PowerShell so the IIS App Pool identity has **Modify** access:

```powershell (Run As Administrator)
icacls "C:\inetpub\wwwroot\kkaudiolab-api" /grant "IIS AppPool\KKAudioLabApiPool:(OI)(CI)M" /T
```

### 3. Configure Backend `web.config` (30 MB Upload Support)
`Backend/appsettings.json` allows uploads up to `31457280` bytes (30 MB), while IIS defaults to `30000000` bytes (~28.6 MB).

Open `C:\inetpub\wwwroot\kkaudiolab-api\web.config` (generated by `dotnet publish`) and add `<security><requestFiltering>` inside `<system.webServer>`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\Backend.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
      <security>
        <requestFiltering>
          <!-- Match Backend UploadSettings:MaxFileSizeBytes (30 MB) -->
          <requestLimits maxAllowedContentLength="31457280" />
        </requestFiltering>
      </security>
    </system.webServer>
  </location>
</configuration>
```

> **Debugging Tip:** If the Backend fails to start in IIS, temporarily change `stdoutLogEnabled="true"`, create `C:\inetpub\wwwroot\kkaudiolab-api\logs`, restart the site, and inspect the generated log file.

### 4. Create the Backend IIS Website
1. Right-click **Sites** → **Add Website...**
   - **Site name**: `KKAudioLab-API`
   - **Application pool**: Click **Select...** → choose **`KKAudioLabApiPool`**
   - **Physical path**: `C:\inetpub\wwwroot\kkaudiolab-api`
   - **Binding**:
     - Type: `http`
     - IP address: `All Unassigned`
     - Port: `5100`
     - Host name: *(leave blank)*
2. Click **OK**.

---

## Step 6: Configure the Frontend Site (Blazor WASM) in IIS

### 1. Create the Frontend Application Pool
1. Right-click **Application Pools** → **Add Application Pool...**
   - **Name**: `KKAudioLabWebPool`
   - **.NET CLR version**: **No Managed Code**
   - **Managed pipeline mode**: **Integrated**

### 2. Create `web.config` Inside `wwwroot\` for SPA Routing & MIME Types
> **Important:** `dotnet publish` generates a default `web.config` in the **parent** folder (`C:\inetpub\wwwroot\kkaudiolab-web\web.config`), **not** inside `wwwroot\`, and it does not contain the Blazor SPA rewrite rule. Because the IIS site's Physical Path points to `C:\inetpub\wwwroot\kkaudiolab-web\wwwroot`, IIS **ignores** any `web.config` in the parent folder.

1. Delete (or ignore) `C:\inetpub\wwwroot\kkaudiolab-web\web.config` if present.
2. Create `C:\inetpub\wwwroot\kkaudiolab-web\wwwroot\web.config` *(make sure File Explorer does **not** save it as `web.config.txt`)* with the following content so IIS serves Blazor WebAssembly assets (`.wasm`, `.dat`, `.json`, `.mjs`, `.woff2`) and rewrites SPA routes (`/auth`, `/playlists`, `/manage`) to `index.html`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension=".wasm" />
      <mimeMap fileExtension=".wasm" mimeType="application/wasm" />
      <remove fileExtension=".dat" />
      <mimeMap fileExtension=".dat" mimeType="application/octet-stream" />
      <remove fileExtension=".json" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <remove fileExtension=".mjs" />
      <mimeMap fileExtension=".mjs" mimeType="application/javascript" />
      <remove fileExtension=".woff2" />
      <mimeMap fileExtension=".woff2" mimeType="font/woff2" />
    </staticContent>

    <rewrite>
      <rules>
        <rule name="Blazor WASM SPA Fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

3. **Verify in IIS Manager:** Click **`KKAudioLab-Web`** → open **URL Rewrite** → confirm **`Blazor WASM SPA Fallback`** appears in the rules list.

### 3. Create the Frontend IIS Website
1. Right-click **Sites** → **Add Website...**
   - **Site name**: `KKAudioLab-Web`
   - **Application pool**: Select **`KKAudioLabWebPool`**
   - **Physical path**: `C:\inetpub\wwwroot\kkaudiolab-web\wwwroot` *(must point to the `wwwroot` subfolder)*
   - **Binding**:
     - Type: `http`
     - IP address: `All Unassigned`
     - Port: `5200`
     - Host name: *(leave blank)*
2. Click **OK**.

> If you deploy to a folder outside `C:\inetpub\wwwroot`, make sure `IIS_IUSRS` has **Read & Execute** permissions on that folder.

---

## Step 7: Test & Verify

1. Confirm no `dotnet run` or Visual Studio instances are occupying ports `5100` or `5200`.
2. In **IIS Manager**, verify both `KKAudioLab-API` and `KKAudioLab-Web` are **Started**.
3. **Test Backend (on laptop):**
   - `http://localhost:5100/` → loads the KK Audio Lab Health & Status Dashboard.
   - `http://localhost:5100/health` and `http://localhost:5100/api/tracks` → return JSON responses.
4. **Test Frontend (on laptop):**
   - Open `http://localhost:5200/` → Blazor WebAssembly initializes.
   - Navigate to `http://localhost:5200/playlists` and press **F5** (refresh) → confirms URL Rewrite SPA fallback works.
   - Sign in on `/auth` with the seeded student credentials (`student@kkaudiolab.sg` / `Password123!`), play a track, and scrub the progress bar → confirms CORS headers (`Accept-Ranges`, `Content-Range`, `Content-Length`) and HTTP 206 byte-range audio streaming work in IIS.

---

## Step 8: Access from Phone or Other Devices on the Same Wi-Fi (Optional)

Because the code resolves the API host dynamically from the browser URL, you only need to allow ports `5100` and `5200` through **Windows Defender Firewall** and verify IIS bindings:

1. **Open Firewall Ports (Run PowerShell as Administrator):**
   ```powershell
   New-NetFirewallRule -DisplayName "KK Audio Lab IIS (5100, 5200)" -Direction Inbound -Protocol TCP -LocalPort 5100,5200 -Action Allow
   ```
2. **Verify IIS Bindings:**
   In IIS Manager, check **Bindings...** on both `KKAudioLab-API` (`5100`) and `KKAudioLab-Web` (`5200`):
   - **IP address:** `All Unassigned` (not `127.0.0.1`)
   - **Host name:** *(blank)*
3. **Find Your Laptop's Wi-Fi IP Address:**
   Run `ipconfig` and look for your Wi-Fi adapter's **IPv4 Address** (e.g., `192.168.1.50`).
4. **Open on Your Phone:**
   - Make sure your phone is on the same Wi-Fi network, then visit `http://<YOUR-LAPTOP-IP>:5200` (using `http://`, not `https://`).
   - *(Note: If `http://<YOUR-LAPTOP-IP>:5100/health` still times out on your phone after adding the firewall rule, your Wi-Fi router or campus/guest network likely has **AP / Client Isolation** enabled.)*

---

## Troubleshooting

| Symptom / Error | Likely Cause & Fix |
|---|---|
| **Site fails to start ("process cannot access the file because it is being used")** | Port `5100` or `5200` is already in use by `dotnet run` or Visual Studio. Close dev terminals and start the site again in IIS Manager. |
| **HTTP 500.19 (`0x8007000d` or `0x80070021`)** | **URL Rewrite** or **.NET 10 Hosting Bundle** is missing, or `iisreset` was not run after installation. |
| **HTTP 500.30 (In-Process startup failure)** | `UseUrls(...)` is still in `Backend/Program.cs`, or `IIS AppPool\KKAudioLabApiPool` lacks **Modify** permission on `C:\inetpub\wwwroot\kkaudiolab-api`. Enable `stdoutLogEnabled="true"` in `web.config` to inspect startup logs. |
| **HTTP 404 when refreshing `/playlists` or `/manage`** | `web.config` is in the parent `kkaudiolab-web\` folder instead of `kkaudiolab-web\wwwroot\web.config`, was saved as `web.config.txt`, is missing the `<rewrite>` rule, or URL Rewrite Module 2.1 is not installed. Verify **`Blazor WASM SPA Fallback`** appears under IIS Manager → `KKAudioLab-Web` → **URL Rewrite**. |
| **HTTP 404.13 when uploading a ~30 MB track** | `<requestLimits maxAllowedContentLength="31457280" />` is missing in Backend `web.config`. |
| **CORS blocked in browser DevTools** | Frontend is not running on port `5200` (see `uri.Port == 5200` in `Backend/Program.cs`), or an older build without the updated CORS rule is deployed. |
| **Phone / LAN device cannot connect to `http://<IP>:5200` or `:5100`** | Allow inbound TCP ports `5100` and `5200` in **Windows Defender Firewall** (Step 8), confirm IIS bindings use `All Unassigned` and a blank Host name, and check that Wi-Fi **AP/Client Isolation** is not enabled. |

