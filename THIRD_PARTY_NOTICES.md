# Third-party components

The Windows x64 portable ZIP includes the following Microsoft components. The `licenses/` directory in the ZIP contains their license and notice files. Dependency terms apply to those components independently of the project license.

| Component | Version | Terms and notices |
| --- | --- | --- |
| .NET Runtime, `Microsoft.NETCore.App.Runtime.win-x64` | 10.0.15 | MIT and bundled third-party notices. `dotnet-runtime-license.txt`, `dotnet-runtime-third-party-notices.txt`. |
| Windows Desktop Runtime, `Microsoft.WindowsDesktop.App.Runtime.win-x64`, including WPF | 10.0.15 | MIT and Windows Desktop third-party notices. `windows-desktop-license.txt`, `windows-desktop-third-party-notices.txt`. |
| WebView2 SDK managed assemblies and native loader, `Microsoft.Web.WebView2` | 1.0.3967.48 | Microsoft copyright and BSD 3-Clause redistribution terms. `webview2-sdk-license.txt`, `webview2-sdk-notice.txt`. |

The launcher uses the machine's separately installed Microsoft Edge Evergreen WebView2 Runtime. The ZIP bundles the SDK components and loader. Refer to the package license for the exact terms. [.NET runtime](https://github.com/dotnet/runtime) · [WPF](https://github.com/dotnet/wpf) · [WebView2 SDK](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3967.48) · [Evergreen distribution](https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution)

The launcher is independent of and unaffiliated with OpenAI or Microsoft. Product names identify the relevant software and its compatibility.
