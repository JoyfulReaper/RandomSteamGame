# Random Steam Game

A fun application to choose a random steam game to play!  
See a live preview at https://randomsteam.kgivler.com

The Solution file contains the following projects:
- RandomSteamGame: My initial project, an MVP (Minimum Viable Product) that I do plan to refactor and improve. The background does not always behave correctly when changing games. Otherwise it does work correctly.
- RandomSteamGameBlazor.Client: Blazor Wasm UI. Enter your SteamId or Steam Vanity URL and you will be rewarded with a random game from your Steam library.
- RandomSteamGameBlazor.Server: Backed API for the Blazor Wasm client. Utilizies the SteamApiClient to return a random game.
  - Uses MediatR
- SteamApiClient: Used by both the Blazor and MVC projects to communicate with the Steam API.
  - Uses IDistributedCache to cache the results of the calls to the SteamAPI
