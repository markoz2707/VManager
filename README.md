# HyperV Agent – full repo with OpenAPI

Projects:
- HyperV.Agent (Web API + Swagger at /swagger, OpenAPI JSON at /swagger/v1/swagger.json)
- HyperV.LocalShell (console TUI for Server Core)
- HyperV.Core.Hcs / Hcn / Wmi / Vhd (separated layers)
- HyperV.Contracts (DTOs with annotations)
- HyperV.Tests (xUnit)

## Run
```
dotnet restore
dotnet build HyperV.sln
dotnet run --project src/HyperV.Agent/HyperV.Agent.csproj
```
Then visit http://127.0.0.1:8743/swagger


## External documentation
Microsoft documentation for api is Here
https://github.com/MicrosoftDocs/win32/tree/docs/desktop-src/HyperV_v2
