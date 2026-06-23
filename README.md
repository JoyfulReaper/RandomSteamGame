# 🎮 Random Steam Game

**Stop scrolling your library. Start playing your games.**

[Random Steam Game](https://randomsteam.kgivler.com) is a web application designed to help you conquer your backlog. Simply enter your SteamId or Vanity URL, and let the app pick your next gaming adventure.

---


## 🚀 Live Demo

**[Visit the App →](https://randomsteam.kgivler.com)**

## Screenshots
Home Page:
![Home Page](docs/images/RandomSteam_Main.png)

Random Game Results:
![Results](docs/images/RandomSteam_Game.png)
---

## 🛠 Tech Stack

Built with the latest .NET technologies:

* **Frontend:** Blazor WebAssembly
* **Backend:** ASP.NET Core API
* **Caching:** Distributed Cache (SQLite or Sql Server)
* **Authentication:** ASP.NET Identity & JWT (registration and logins currently disabled)

---

## 📂 Project Structure

* **`RandomSteamGameBlazor.Client`**: The Blazor Wasm UI.
* **`RandomSteamGameBlazor.Server`**: The backend API responsible for identity, database access, and serving the client.
* **`SteamApiClient`**: A dedicated library for seamless communication with the Steam Web API and Steam Store API.

---

## 🚧 Roadmap

### 🏗️ In Development

* [ ] **User Accounts:** Registration and Login systems.
* [ ] **Authentication:** JWT Token integration for secure sessions.

### 📋 Planned Features

* [ ] **Favorites:** Save and manage your favorite games.
* [ ] **Exclusions:** Hide specific games you never want the randomizer to pick.
* [ ] **Queue:** A "Play Next" list to track your backlog.
* [ ] **Advanced Filters:** - Filter by play time (e.g., "Played less than 1 hour").
* Filter by completion status.
* Exclude specific genres or tags.

---

## 📝 License

Copyright (c) 2026 Kyle Givler.
Licensed under the [MIT License](https://opensource.org/license/mit).
