# The Path RP Overlay

En WPF-baserad overlay-applikation för rollspel (RP) på The Path, specifikt designad för sjukhus-scenarios. Applikationen tillhandahåller snabbkommandon och anteckningsverktyg som alltid ligger överst på skärmen.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 📋 Funktioner

### 🎮 Overlay-fönster
- **Always-on-top**: Fönstret ligger alltid överst på skärmen
- **Transparent bakgrund**: Ser ut som en del av spelet
- **Dragbar**: Flytta fönstret genom att dra i titelfältet
- **Storleksändringsbar**: Dra i nedre vänstra hörnet för att ändra storlek
- **Keyboard shortcut**: Visa/dölj overlay med F9 (konfigurerbart)

### 📝 Snabbkommandon
- Anpassningsbara kommandoknappar för snabba RP-kommandon
- Ett klick kopierar texten till urklipp
- Perfekt för vanliga `/me` och `/do` kommandon
- Hantera kommandon via inställningsmenyn:
  - Lägg till nya kommandon
  - Redigera befintliga
  - Ta bort kommandon
  - Ändra ordning med pil-knappar

### 📔 Snabbanteckningar
- **Flera flikar**: Tre separata anteckningsområden
  - **Noteringar**: Allmänna anteckningar
  - **Patienter**: Patientinformation
  - **Händelser**: Händelselogg
- **Auto-sparning**: Sparas automatiskt var 30:e sekund
- **Persistent**: Anteckningar bevaras mellan sessioner
- **Scrollbar**: Stöd för långa anteckningar

### 🔧 Inställningar
- Inställningsmeny tillgänglig via system tray
- Dynamisk kommandohantering
- Konfigurerbar hotkey (via config-fil)
- Inställningar sparas i `%APPDATA%\RPOverlay\`

### 🎯 System Tray Integration
- Minimeras till system tray (aktivitetsfältet)
- Högerklicka för snabbmeny:
  - Visa/Dölj overlay
  - Öppna inställningar
  - Avsluta applikation
- Dubbelklicka för att visa/dölja

## 🚀 Installation

### Förutsättningar
- Windows 10/11
- .NET 8.0 Runtime (installeras automatiskt om det saknas)

### Steg 1: Klona repository
```bash
git clone https://github.com/benjibutten/RPOverlay.git
cd RPOverlay
```

### Steg 2: Bygg projektet
```bash
dotnet build --configuration Release
```

### Steg 3: Kör applikationen
```bash
dotnet run --project RPOverlay.WPF
```

Eller navigera till `RPOverlay.WPF\bin\Release\net8.0-windows\` och kör `RPOverlay.WPF.exe`

## ⚙️ Konfiguration

### Konfigurations-fil
Konfigurationsfilen skapas automatiskt vid första körningen:
```
%APPDATA%\RPOverlay\overlay-config.json
```

### Exempel-konfiguration
```json
{
  "Hotkey": "F9",
  "Buttons": [
    {
      "Label": "Ta fram ID",
      "Text": "/me tar fram sitt ID-kort och visar upp det"
    },
    {
      "Label": "Behandlar",
      "Text": "/me påbörjar behandling och kontrollerar puls"
    }
  ]
}
```

### Hotkey-format
Hotkeys kan konfigureras med modifierare:
- `F9` - Enkel tangent
- `Ctrl+F9` - Med Control
- `Shift+Alt+F10` - Flera modifierare
- Giltiga modifierare: `Ctrl`, `Shift`, `Alt`, `Win`

### Anteckningar
Anteckningar sparas automatiskt som YML-filer med metadata i:
```
%APPDATA%\RPOverlay\Notes\
  - Anteckningar.yml
  - MinNote.yml
  - ...
  
%APPDATA%\RPOverlay\Notes\Archive\
  - Stängda anteckningar arkiveras här
```

**Flikar och hantering:**
- Flikar baseras på vilka `.yml` filer som finns i Notes-mappen
- Dubbelklicka på en flik för att döpa om den
- Tryck Enter för att spara, Escape för att ångra
- Stäng en flik (X) för att arkivera den till Archive-mappen
- Första raden blir fliknamn automatiskt (om inget manuellt namn satts)

## 🎨 Anpassning

### Lägg till egen ikon
1. Skapa eller hämta en `.ico`-fil (16x16, 32x32, 48x48, 256x256 px)
2. Placera filen i `RPOverlay.WPF\Resources\app-icon.ico`
3. Se `RPOverlay.WPF\Resources\README.md` för detaljerade instruktioner

### Ändra färgschema
Redigera färgkoder i `MainWindow.xaml`:
- Primärfärg (knappar): `#FF2F9DFF` (ljusblå)
- Bakgrund: `#CC1E1E1E` (mörkgrå)
- Accent: `#22FFFFFF` (vit transparent)

## 🏗️ Projektstruktur

```
RPOverlayApp/
├── RPOverlay.Core/              # Kärnlogik och modeller
│   ├── Abstractions/            # Interfaces
│   ├── Models/                  # Datamodeller
│   │   ├── OverlayConfig.cs    # Konfigurationsmodell
│   │   └── HotkeyDefinition.cs # Hotkey-definition
│   ├── Providers/               # Config path providers
│   ├── Services/                # Business logic
│   │   └── OverlayConfigService.cs
│   └── Utilities/               # Hjälpklasser
│       └── HotkeyParser.cs     # Hotkey parsing
│
├── RPOverlay.WPF/               # WPF UI-projekt
│   ├── Interop/                 # Win32 API interop
│   │   └── NativeMethods.cs    # P/Invoke deklarationer
│   ├── Logging/                 # Loggning
│   │   └── DebugLogger.cs      
│   ├── Resources/               # Resurser (ikoner etc.)
│   ├── MainWindow.xaml          # Huvudfönster UI
│   ├── MainWindow.xaml.cs       # Huvudfönster logik
│   ├── SettingsWindow.xaml      # Inställningsfönster UI
│   ├── SettingsWindow.xaml.cs   # Inställningsfönster logik
│   └── App.xaml.cs              # Application entry point
│
└── RPOverlay.Tests/             # Unit tests
    └── HotkeyParserTests.cs     # Hotkey parser tester
```

## 🔍 Arkitektur

### Teknisk Stack
- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Interop**: Win32 API för hotkeys och window management
- **Serialization**: System.Text.Json
- **Testing**: xUnit

### Design Patterns
- **MVVM**: Model-View-ViewModel för UI separation
- **Provider Pattern**: För konfigurationshantering
- **Observer Pattern**: ConfigReloaded events
- **Dependency Injection**: Service-baserad arkitektur

### Key Features Implementation
- **Always-on-top**: `Topmost="True"` + `WS_EX_TOPMOST` extended style
- **No-activate**: `WS_EX_NOACTIVATE` för att inte stjäla fokus
- **Global hotkeys**: `RegisterHotKey` Win32 API
- **Auto-save**: `DispatcherTimer` med 30 sekunders intervall
- **Resize grip**: `Thumb` control med `DragDelta` event

## 🧪 Testning

Kör unit tests:
```bash
dotnet test
```

Kör med coverage:
```bash
dotnet test /p:CollectCoverage=true
```

## 🐛 Felsökning

### Overlay visas inte
- Kontrollera att F9 (eller din konfigurerade hotkey) inte används av andra program
- Kolla Debug-loggar i Output-fönstret i Visual Studio

### Hotkey fungerar inte
- Vissa tangenter kan vara reserverade av systemet
- Prova med en modifierare (t.ex. `Ctrl+F9`)
- Kontrollera att hotkey-formatet är korrekt i config-filen

### Inställningar sparas inte
- Kontrollera att du har skrivbehörighet till `%APPDATA%\RPOverlay\`
- Kör applikationen som administratör om problemet kvarstår

### Anteckningar försvinner
- Auto-sparning sker var 30:e sekund
- Anteckningar sparas också vid stängning av applikationen
- Kontrollera `%APPDATA%\RPOverlay\Notes\` för sparade filer

## 📝 Utveckling

### Krav
- Visual Studio 2022 eller senare
- .NET 8.0 SDK
- Windows 10/11

### Starta utveckling
```bash
# Klona repo
git clone https://github.com/benjibutten/RPOverlay.git

# Öppna solution
cd RPOverlay
start RPOverlayApp.sln

# Eller använd VS Code
code .
```

### Bygg debug-version
```bash
dotnet build
```

### Kör från Visual Studio
1. Öppna `RPOverlayApp.sln`
2. Sätt `RPOverlay.WPF` som startup project
3. Tryck F5 för att köra med debugger

## 🤝 Bidra

Bidrag är välkomna! Följ dessa steg:

1. Forka projektet
2. Skapa en feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit dina ändringar (`git commit -m 'Add some AmazingFeature'`)
4. Push till branchen (`git push origin feature/AmazingFeature`)
5. Öppna en Pull Request

## 📄 Licens

Detta projekt är licensierat under MIT License - se [LICENSE.txt](LICENSE.txt) för detaljer.

## 👤 Författare

**benjibutten**
- GitHub: [@benjibutten](https://github.com/benjibutten)

## 🙏 Erkännanden

- The Path RP community
- WPF Community för inspiration och resurser
- .NET Open Source community

## 📞 Support

För frågor eller support:
- Öppna en [Issue](https://github.com/benjibutten/RPOverlay/issues)
- Kontakta på The Path RP-servern

---

**Gjord med ❤️ för The Path RP community**
