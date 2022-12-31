// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

function getGame(preload, page) {
    const body = document.getElementById("body");
    body.style.backgroundColor = "#000";

    fetch(page)
        .then(response => response.json())
        .then((data) => {
            document.getElementById("gameName").innerText = data.name;
            document.getElementById("gameNameHeader").innerText = data.name;
            document.getElementById("gameDescription").innerHTML = data.about_the_game;
            document.getElementById("steamUrl").href = "steam://run/" + data.steam_appid + "/";

            if (preload) {
                var backgroundImage = new Image();
                backgroundImage.onload = function () {
                    body.style.backgroundImage = 'url(' + data.background + ')';
                }
                backgroundImage.src = data.background;
            } else {
                body.style.backgroundImage = 'url(' + data.background + ')';
            }

            body.style.backgroundSize = "cover";
            body.style.backgroundRepeat = "no-repeat";
            body.style.backgroundAttachment = "fixed";
            body.style.color = "#fff";
        }).catch((error) => {
            console.error('Error:', error);
            document.getElementById("main").innerHTML = `<h1>Something Went Wrong!</h1>`;
        });
}
