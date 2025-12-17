# dn42bot

A telegram bot for dn42 Network

## Features

- Network utils
  - [x] Ping
  - [ ] TCPing
  - [x] Traceroute
  - [x] Nslookup/dig
  - [ ] Whois
- Looking glass
  - [ ] Route
  - [ ] Path

## Get Started

You can get the latest from the [Releases](https://github.com/LeZi9916/dn42bot/releases),or build it yourself.

- Before proceeding, please ensure you have the dotnet runtime(9.0 or later) installed
- Upon its first run, dn42bot will generate `config.json` in the current directory
- Fill in your bot API token in `config.json`
- If needed, you can also set up a proxy in `config.json`
- Enjoy!

## Build

Please ensure you have the dotnet **SDK**(9.0 or later) installed

**Tip:** default target framework is dotnet 10.0. If you need to use an older framework version, please modify the .csproj file yourself

Clone the repo

```bash
git clone https://github.com/LeZi9916/dn42bot.git
cd dn42bot
```

Restore project

```bash
dotnet restore
```

Compile or publish

```bash
dotnet build -c Release
dotnet publish -c Release
```
