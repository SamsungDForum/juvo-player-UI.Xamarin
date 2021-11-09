# JuvoPlayer UI.Xamarin

## Introduction
Xamarin UI for
[JuvoPlayer component](https://github.com/SamsungDForum/JuvoPlayer/tree/master-v2 "JuvoPlayer 2.x component") derived from an integrated
[JuvoPlayer application](https://github.com/SamsungDForum/JuvoPlayer "JuvoPlayer application")


## Prerequisites
- [Tizen Studio](https://developer.tizen.org/development/tizen-studio/download)
- [Tizen Extensions for Visual Studio Family](https://developer.tizen.org/development/tizen-extensions-visual-studio-family)

  Needed for Visual Studio family IDEs only.

- [TV Extension for Tizen SDK](https://developer.samsung.com/smarttv/develop/tools/tv-extension/archive.html)

  Version depends on target device in use.


## Build requirements
Building JuvoPlayer UI.Xamarin requires reference to following nuget repositories.

Repository       | Source
---------------- | -------------
nuget.org        | https://api.nuget.org/v3/index.json
tizen.myget.org  | https://tizen.myget.org/F/dotnet/api/v2

## Build configurations

- ```Debug```

  Debug build configuration

- ```Debug HotReload```

  Debug build configuration with [Xamarin Forms hot reload](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/xaml/hot-reload) enabled.

- ```Release```

  Release configuration

## dlog debug channels:

- ```JuvoUI``` - application logs.
- ```JuvoPlayer``` - JuvoPlayer component logs.

## Startup Projects
Startup projects differ for Tizen 5/Tizen 6 devices. Such approach was chosen to overcome limitations of Tizen Tools for Visual Studio, which silently ignores
target framework selection in multitargeted project.

## Features
- Streaming and DRM support as specified in [JuvoPlayer component](https://github.com/SamsungDForum/JuvoPlayer/releases "JuvoPlayer 2.x component").
- [SkiaSharp](https://docs.tizen.org/application/dotnet/guides/libraries/skiasharp/) library used for preview tiles.
- [Multitasking](https://developer.samsung.com/smarttv/develop/guides/fundamentals/multitasking.html) support.
- [SmartHub preview](https://developer.samsung.com/smarttv/develop/guides/smart-hub-preview.html) support.
- [TV Emulator](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-emulator.html) support.

## JuvoPlayer UI-Xamarin and JuvoPlayer integrated application differences
- No subtitle support.
- No UDP logging.
