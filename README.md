# Wemo
A C# application that sends commands to Wemo devices.

## Prerequisites:
* [.NET Core SDK](https://github.com/dotnet/core) (2.2 or later)

## Build and Run
Clone the repository:
```powershell
> git clone https://github.com/jecassis/wemo.git
> cd wemo
```
Generate `launch.json` and `tasks.json` for Visual Studio Code (already in repository) using:
```powershell
> dotnet new console
```
Alternatively, from the GUI menu: View -> Command Palette... -> .NET: Generate Assets for Build and Debug

Build a debug configuration:
```powershell
> dotnet build
```

To build and run:
```powershell
> dotnet run
```

To clean the repository of build artifacts:
```powershell
> dotnet clean
```
