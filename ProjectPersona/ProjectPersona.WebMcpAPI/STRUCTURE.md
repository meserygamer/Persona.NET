# ProjectPersona.WebMcpAPI

Parent: [project index](../../docs/project-structure.md)

Web exe (`Microsoft.NET.Sdk.Web`), `net10.0`. HTTP variant of the MCP server for clients that do not start the process themselves (for example Claude Code running in a container, see `AgentInteraction.Claude/.mcp.json`). Dependencies: Application, SourceIndex.Sqlite; `ModelContextProtocol.AspNetCore` 2.2.0, `Serilog.AspNetCore`.

```
ProjectPersona.WebMcpAPI/
├── Startup.cs                     WebApplication (top-level): settings check, http/https URLs, same DI registrations as Stdio, WithHttpTransport, MapMcp("/mcp")
├── WebMcpAppSettings.cs           AppSettings + port (5095) + host (0.0.0.0) + httpsEnable + httpsSettings; IValidatableObject
├── HttpsSettings.cs               httpsSettings section: port (7198), certificatePath, keyPath, certificatePassword
├── HttpsCertificateLoader.cs      certificate from PFX/P12 or PEM (+ key); PEM is rebuilt through PKCS#12 for SChannel
├── IpAddressAttribute.cs          validates host as IPv4/IPv6
├── AppLogging.cs                  Serilog: file in BaseDirectory/logs, stderr only Error+
├── appsettings.json               typeSourceIndexPath, ragSearchEnabled, llama, lanceDb, port, host, httpsEnable, httpsSettings, AllowedHosts, Logging
├── Properties/launchSettings.json  profiles http (localhost:5095) and https (7198)
└── McpTool/                       copies of TypeSourceIndexTool, TextSearchTool and RagSearchTool from Stdio (namespace ProjectPersona.WebMcpAPI.McpTool)
```

## Behaviour

- Listens on `http://{host}:{port}`; with `httpsEnable: true` also on `https://{host}:{httpsSettings.port}`.
- `host` defaults to `0.0.0.0` (all interfaces; use `127.0.0.1` for local only). It must be a valid IP; IPv6 is allowed and is bracketed in the URL.
- Ports must be in 1–65535 and the HTTPS port must differ from the HTTP port. The certificate file (and key file, when set) must exist. Invalid settings stop startup.
- The tool contract is identical to Stdio — see the tool table in [StdioMcpAPI](../ProjectPersona.StdioMcpAPI/STRUCTURE.md). When you change tool attributes, descriptions or parameters, change both projects.
- A client port that differs from the default 5095 (for example `host.docker.internal:55095` in a container `.mcp.json`) implies a port forward or proxy; the server itself listens only on its configured port.

## Related

- Services, startup validation and `ragSearch` states: [Application](../ProjectPersona.Application/STRUCTURE.md)
- Client configuration example: [AgentInteraction.Claude](../../AgentInteraction.Claude/STRUCTURE.md)
