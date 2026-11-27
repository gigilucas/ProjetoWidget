<p align="center">
  <img src="docs/logo.png" alt="Widglass logo" width="200">
</p>

# Widglass

Desktop widgets for Windows in the style of macOS widgets: frosted glass, rounded corners and the soft blue → pink → orange colors of the logo.

<p align="center">
  <a href="https://gigi-apps.github.io"><b>Download it at gigi-apps.github.io</b></a>
</p>

## Download

1. Go to **[gigi-apps.github.io](https://gigi-apps.github.io)** and click **Download for Windows**.
2. Open it. That's all: **nothing else to install**, everything works right away.

- Works on Windows 10 (1809 or later) and Windows 11.
- The first time, Windows may say *"Windows protected your PC"*: click **More info** and then **Run anyway**.
- Widglass then sits in the notification area (the **^** arrow next to the clock) and starts with Windows.
- It **keeps itself up to date**: new versions install by themselves, so you always have the latest Widglass.
- Keep the file somewhere it can stay (for example a *Widglass* folder in *Documents*), since Windows opens it from there at startup.

More apps by the same author are on the same site.

<p align="center">
  <img src="docs/screenshots/widget-clock.png" alt="Clock widget" title="Clock" width="19%">
  <img src="docs/screenshots/widget-cpu-ram.png" alt="CPU / RAM widget" title="CPU / RAM" width="19%">
  <img src="docs/screenshots/widget-calendar.png" alt="Calendar widget" title="Calendar" width="19%">
  <img src="docs/screenshots/widget-weather.png" alt="Weather widget" title="Weather" width="19%">
  <img src="docs/screenshots/widget-music.png" alt="Music widget" title="Music" width="19%">
</p>
<p align="center">
  <img src="docs/screenshots/widget-stocks-wide.png" alt="Stocks widget with three stocks" title="Stocks" width="39%">
  <img src="docs/screenshots/widget-currency.png" alt="Currency widget" title="Currency" width="19%">
  <img src="docs/screenshots/widget-photos.png" alt="Photos widget" title="Photos" width="19%">
</p>
<p align="center"><sub>Clock · CPU / RAM · Calendar · Weather · Music · Stocks · Currency · Photos — screenshots of the real app</sub></p>

## How it works

The app lives in the **notification area** (the **^** arrow next to the clock on the taskbar), not on the taskbar like a normal app. **Click its icon** to open the menu; right-click it for *Open Widglass*, *Start with Windows*, *Check for updates* and *Quit*.

The menu lists the eight widgets; scroll the list to see them all.

<p align="center">
  <img src="docs/screenshots/manager.png" alt="Manager window" width="31%">
  <img src="docs/screenshots/settings-clock.png" alt="Clock settings" width="31%">
  <img src="docs/screenshots/settings-weather.png" alt="Weather settings" width="31%">
</p>

- **On/off switch**: shows or hides that widget on the desktop.
- **Click a row** (not the switch): opens that widget's settings. **‹ Widgets** goes back.
- **Pinned to the desktop**: widgets live on the desktop like the wallpaper. They stay behind every other window, even when you click them, and they stay visible on *Show desktop* (Win+D). To reach them, show the desktop or move the windows out of the way.
- **Drag a widget** anywhere on the screen; its position is remembered.
- **Always whole on a screen**: a widget can't go past the screen edges or under the taskbar. With more than one monitor you can drag it from screen to screen; it jumps over when its center crosses, so it never sits half on each. If a monitor is unplugged or the resolution changes, widgets move back onto a screen.
- **Widgets never cover each other**: they can touch, but if you drag one onto another it slides out the shortest way and stays edge to edge. A widget that opens where another one already is moves to the nearest free spot.
- **Dark mode** (under *App*): switches the menu and all the widgets to dark at once. See [Dark mode](#dark-mode).
- **Start with Windows** (under *App*, on from the first time you open Widglass): when the PC starts, the widgets appear on the desktop and the icon goes to the notification area; the menu stays closed until you click the icon. Turn it off there if you'd rather open Widglass yourself.
- **Always up to date**: Widglass checks for a new version when it starts and every few hours, and installs it by itself: it downloads it, swaps it in and restarts in a few seconds, keeping your widgets, positions and settings. The top of the menu shows the progress, and a notification tells you when it's done. You can also right-click the icon and choose *Check for updates*. The version you have is shown at the bottom of the menu.
- The red and yellow buttons tuck the menu away into the notification area; the widgets keep running. To close everything, right-click the icon and choose **Quit**.
- Only one copy runs at a time: opening the app again just brings up its menu.
- Widgets don't show up in the taskbar or in Alt+Tab.
- Your choices are saved in `%AppData%\Widglass\settings.json`, and the widgets that were on come back the next time the app starts.

## Widgets

### Clock

<img src="docs/screenshots/widget-clock.png" alt="Analog clock" width="160"> <img src="docs/screenshots/widget-clock-digital.png" alt="Digital clock" width="160"> <img src="docs/screenshots/dark/widget-clock-digital.png" alt="Digital clock, dark" width="160">

- Time and time zone come from this PC.
- **Style** (in settings): **Analog**, a clock face with hour, minute and second hands, or **Digital**, big numbers with seconds, the weekday and the date. The change applies right away.
- Digital follows the PC's 12/24-hour setting.

### Calendar

- The date comes from this PC and is shown as **dd/mm/yyyy**, with the weekday on top.
- Month view with today highlighted; it rolls over by itself at midnight.
- The week starts on the day set in the PC's region settings.

### Weather

- Uses **this PC's location** (Windows location services).
- Shows the place, the current temperature and conditions with an icon (sun, moon, clouds, rain, snow, storm, fog), today's high/low, and **today plus the next 4 days** (high and low).
- Updates every 15 minutes. Units: Celsius.
- If Windows location is off, the widget says so and its settings have a button that opens the Windows location page.
- Forecast by [Open-Meteo](https://open-meteo.com) and place name by [BigDataCloud](https://www.bigdatacloud.com), both free and without an account. The coordinates are rounded to about 1 km before they leave the PC.

### CPU / RAM

- Two rings with live **CPU usage** and **memory usage**, updated every second.
- Under CPU: the current **speed** (GHz), the same number Task Manager shows. Under RAM: used / total GB.
- The CPU temperature isn't shown: Windows doesn't share it without admin rights or extra drivers.
- Settings show the processor name, threads and total memory.

### Music

<img src="docs/screenshots/settings-music.png" alt="Music settings" width="260">

- Works with **Apple Music**, **YouTube Music** and **Spotify**. All three are always listed in settings, marked *Installed* or *Not installed*.
- When you open one of these apps and play, it **shows up on the widget by itself**. If more than one is playing, the app chosen in settings wins.
- Shows the album art, song, artist, which app it comes from, and a progress bar with elapsed and remaining time.
- Controls: play/pause, previous, next, shuffle and repeat. Buttons the app doesn't support are dimmed.
- **Jump in a song** by clicking or dragging the progress bar, **except in Apple Music**: Apple Music for Windows doesn't let other apps change the song position, so there the bar only shows progress. Spotify and YouTube Music allow it.
- Browsers are ignored (a video playing in Edge never shows up). YouTube Music counts only when it's **installed as an app**: open [music.youtube.com](https://music.youtube.com) in Chrome or Edge and choose *Install YouTube Music*.
- When nothing is playing, the widget shows *Not playing* and a button to open the chosen app, or to get it from the Microsoft Store if it isn't installed.
- It reads and controls the music through the Windows media controls (the same ones the volume flyout uses), so no login is needed.

### Stocks

<img src="docs/screenshots/widget-stocks.png" alt="Stocks widget with one stock" width="160"> <img src="docs/screenshots/widget-stocks-wide.png" alt="Stocks widget with three stocks" width="330">

- **Live prices**, refreshed every 5 seconds while the market is open, with today's chart. The price **flashes green or red** when it moves, like on Yahoo Finance.
- **Show 1, 2 or 3 stocks** (in settings). With 1 the widget is a square with a big chart; with 2 or 3 it becomes a wide rectangle with a row per stock: name, mini chart, price and today's change.
- Pick each one by searching a company, symbol or crypto (Apple, TSLA, Bitcoin, S&P 500...), or from the popular picks.
- The chip says **Live** while the market is trading and **Closed** when it isn't. Crypto trades all the time.
- Prices from Yahoo Finance's public data, without an account. Some exchanges are a few minutes behind.

<img src="docs/screenshots/settings-stocks.png" alt="Stocks settings" width="260">

### Currency

<img src="docs/screenshots/widget-currency.png" alt="Currency widget" width="160"> <img src="docs/screenshots/dark/widget-currency.png" alt="Currency widget, dark" width="160">

- Converts an **amount** from one currency to another at the **live rate**, refreshed every 5 seconds, with today's chart of the rate and its change. The result flashes green or red when the rate moves.
- In settings: type the amount (100, 2.50 or 2,50), click **From** or **To** and pick the currency. The round button swaps them.
- Euro, US Dollar, British Pound, Brazilian Real, Swiss Franc, Japanese Yen, Canadian and Australian Dollar, Chinese Yuan, Indian Rupee, Mexican Peso, Angolan Kwanza, Bitcoin and Ethereum.
- Currencies trade on weekdays (the chip says **Closed** at the weekend); crypto every day. Rates from Yahoo Finance.

<img src="docs/screenshots/settings-currency.png" alt="Currency settings" width="260">

### Photos

<img src="docs/screenshots/widget-photos.png" alt="Photos widget" width="160">

- Your photos take turns filling the widget, with a soft crossfade and a slow zoom.
- **Up to 5 photo frames** on the desktop (*Frames on the desktop* in settings). Each frame has **its own photos and options**: pick a frame under *Editing frame* and it shows its number on the desktop so you know which one it is.
- **Add photos** or **Add folder** (subfolders included). New photos added to a folder join in by themselves.
- **Change every** 5 s, 15 s, 1 min or 5 min; **Square** or **Wide** (for landscape photos); **Shuffle** or by name.
- Phone photos are shown the right way up. The photos never leave the PC.

<img src="docs/screenshots/settings-photos.png" alt="Photos settings" width="260">

## Dark mode

Turn on **Dark mode** in the menu, under *App*. The menu and every widget on the desktop change right away, and the choice is saved.

The widgets become smoked dark glass with white text, and keep the logo colors in the clock hands, rings, today's date, the play button and the progress bar. The green and red of prices stay the same.

<p align="center">
  <img src="docs/screenshots/dark/widget-clock.png" alt="Clock widget, dark" title="Clock" width="19%">
  <img src="docs/screenshots/dark/widget-cpu-ram.png" alt="CPU / RAM widget, dark" title="CPU / RAM" width="19%">
  <img src="docs/screenshots/dark/widget-calendar.png" alt="Calendar widget, dark" title="Calendar" width="19%">
  <img src="docs/screenshots/dark/widget-weather.png" alt="Weather widget, dark" title="Weather" width="19%">
  <img src="docs/screenshots/dark/widget-music.png" alt="Music widget, dark" title="Music" width="19%">
</p>
<p align="center">
  <img src="docs/screenshots/dark/widget-stocks-wide.png" alt="Stocks widget, dark" title="Stocks" width="39%">
  <img src="docs/screenshots/dark/widget-currency.png" alt="Currency widget, dark" title="Currency" width="19%">
  <img src="docs/screenshots/dark/widget-photos.png" alt="Photos widget, dark" title="Photos" width="19%">
</p>

<p align="center">
  <img src="docs/screenshots/dark/manager.png" alt="Manager window, dark" width="31%">
  <img src="docs/screenshots/dark/settings-clock.png" alt="Clock settings, dark" width="31%">
  <img src="docs/screenshots/dark/settings-weather.png" alt="Weather settings, dark" width="31%">
</p>

## Source code

Widglass is written in C# / WPF. To build it yourself or change the code, see [docs/DEVELOPING.md](docs/DEVELOPING.md). To just use the app, you only need the download above.
