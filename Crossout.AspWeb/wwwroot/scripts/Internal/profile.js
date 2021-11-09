﻿var Uid = window.location.pathname.split("/").pop();
var overview_totals = null;
var gamemode_overview = [];
var match_history = [];
var active_classification = null;
var active_game_type = null;


Highcharts.setOptions({
    lang: {
        drillUpText: '< Back'
    }
});

$.ajax({
    url: '/data/profile/totals/' + Uid,
    dataType: 'json',
    success: function (json) {
        overview_totals = json;
        populate_overview_totals();
    }
});

$.ajax({
url: '/data/profile/overview_drilldowns/' + Uid,
dataType: 'json',
success: function (json) {
    build_drilldown('gamemode_overview', 'Game Modes', json["gamemode_preference"]);
    build_drilldown('weapons_overview', 'Weapons', json["weapon_preference"]);
    build_drilldown('movement_overview', 'Movement', json["movement_preference"]);
}
});

$.ajax({
    url: '/data/profile/gamemodes/' + Uid,
    dataType: 'json',
    success: function (json) {
        gamemode_overview = json["game_modes"];
        active_classification = "PvP";
        active_game_type = "Total";
        build_classification_list();
        build_game_type_list();
        populate_gamemode_overview();
        $('#gamemode_overview_card').removeClass('d-none');
    }
});

$.ajax({
    url: '/data/profile/match_history/' + Uid,
    dataType: 'json',
    success: function (json) {
        match_history = json["match_history"];
        populate_match_history_table();
        $('#match_history_overview_card').removeClass('d-none');
    }
});

$('#classification_list').on('click', 'a', function (e) {
    e.preventDefault();
    $(this).tab('show');
    active_classification = $(this).text();
    active_game_type = "Total";
    build_game_type_list();
    populate_gamemode_overview();
});

$('#game_type_list').on('click', 'a', function (e) {
    e.preventDefault();
    $(this).tab('show');
    active_game_type = $(this).text();
    populate_gamemode_overview();
});

function populate_overview_totals() {
    $('#total_games_recorded').text(overview_totals["GamesRecorded"]);
    $('#total_time_recorded').text(overview_totals["TimePlayed"]);
    $('#total_win_rate').text(overview_totals["WinRate"]);
    $('#total_kag').text(overview_totals["KPB"]);
    $('#total_mvp_rate').text(overview_totals["MVPRate"]);
}

function populate_match_history_table() {
    for (var i = 0; i < match_history.length; i++) {
        var row = $("<tr>");
        var cols = "";

        cols += '<td>' + match_history[i]["match_type"] + '</td>';
        cols += '<td><a href="/match/' + match_history[i]["match_id"] + '">' + match_history[i]["match_end"] + '</a></td>';
        cols += '<td>' + match_history[i]["map"] + '</td>';
        cols += '<td>' + match_history[i]["power_score"] + '</td>';
        cols += '<td>' + match_history[i]["score"] + '</td>';
        cols += '<td>' + match_history[i]["kills"] + '</td>';
        cols += '<td>' + match_history[i]["assists"] + '</td>';
        cols += '<td>' + (match_history[i]["damage"]).toFixed(0) + '</td>';
        cols += '<td>' + (match_history[i]["damage_rec"]).toFixed(0) + '</td>';
        cols += '<td>' + match_history[i]["result"] + '</td>';

        cols += '<td></td>';

        //cols += '<td>' + match_history[i]["resources"] + '</td>';

        row.append(cols);
        $('#match_history_body').append(row);
    }
}

function populate_gamemode_overview() {
    var games = 0;
    var rounds = 0;
    var wins = 0;
    var time_spent = 0;
    var medals = 0;
    var kills = 0;
    var assists = 0;
    var drone_kills = 0;
    var deaths = 0;
    var damage = 0;
    var damage_rec = 0;
    var score = 0;

    for (var i = 0; i < gamemode_overview.length; i++) {

        if (active_classification != 'Total' && active_classification != gamemode_overview[i]["match_classification"])
            continue;

        if (active_game_type != 'Total' && active_game_type != gamemode_overview[i]["match_type"])
            continue;

        games += gamemode_overview[i]["games"];
        rounds += gamemode_overview[i]["rounds"];
        wins += gamemode_overview[i]["wins"];
        time_spent += gamemode_overview[i]["time_spent"];
        medals += gamemode_overview[i]["medals"];
        kills += gamemode_overview[i]["kills"];
        assists += gamemode_overview[i]["assists"];
        drone_kills += gamemode_overview[i]["drone_kills"];
        deaths += gamemode_overview[i]["deaths"];
        damage += gamemode_overview[i]["damage"];
        damage_rec += gamemode_overview[i]["damage_rec"];
        score += gamemode_overview[i]["score"];
    }

    $('#games_recorded').text(games);
    $('#win_rate').text(((wins / games) * 100).toFixed(1) + '%');
    $('#kills').text(kills);
    $('#assists').text(assists);
    $('#ka_g').text(((kills + assists) / games).toFixed(2));
    $('#medals').text(medals);
    $('#mvp').text(0.0);
    $('#avg_score').text((score / games).toFixed(0));
    $('#avg_kills').text((kills / games).toFixed(2));
    $('#avg_assists').text((assists / games).toFixed(2));
    $('#avg_damage').text((damage / games).toFixed(0));
    $('#avg_damage_rec').text((damage_rec / games).toFixed(0));
}

function build_classification_list() {
    var li = document.createElement('li');
    var match_classification = [];

    document.getElementById("classification_list").innerHTML = "";
    
    for (var i = 0; i < gamemode_overview.length; i++) {
        if (!match_classification.includes(gamemode_overview[i]["match_classification"]))
            match_classification.push(gamemode_overview[i]["match_classification"]);
    }

    li.classList.add('nav-item');
    li.innerHTML = '<a class="nav-link" id="total-tab" data-toggle="pill" href="#pills-total" role="tab" aria-controls="total" aria-selected="false">Total</a>';
    document.getElementById('classification_list').appendChild(li);

    for (var i = 0; i < match_classification.length; i++) {
        li = document.createElement('li');
        li.classList.add('nav-item');
        if (match_classification[i] == 'PvP') {
            li.innerHTML = '<a class="nav-link active" id="' + match_classification[i] + '-tab" data-toggle="pill" href="#pills-' + match_classification[i] + '" role="tab" aria-controls="pills-' + match_classification[i]+'" aria-selected="true">' + match_classification[i] + '</a>';
        }
        else {
            li.innerHTML = '<a class="nav-link" id="' + match_classification[i] + '-tab" data-toggle="pill" href="#pills-' + match_classification[i] + '" role="tab" aria-controls="pills-' + match_classification[i] +'" aria-selected="false">' + match_classification[i] + '</a>';
        } 

        document.getElementById('classification_list').appendChild(li);
    }
}

function build_game_type_list() {
    var li = document.createElement('li');
    var game_types = [];

    document.getElementById("game_type_list").innerHTML = "";
    
    for (var i = 0; i < gamemode_overview.length; i++) {
        if (!game_types.includes(gamemode_overview[i]["match_type"]) && gamemode_overview[i]["match_classification"] == active_classification)
            game_types.push(gamemode_overview[i]["match_type"]);
    }

    li.classList.add('nav-item');
    li.innerHTML = '<a class="nav-link active" id="total-tab" data-toggle="pill" href="#total" role="tab" aria-controls="total" aria-selected="true">Total</a>';
    document.getElementById('game_type_list').appendChild(li);

    if (game_types.length <= 1)
        return;

    for (var i = 0; i < game_types.length; i++) {
        li = document.createElement('li');
        li.classList.add('nav-item');
        li.innerHTML = '<a class="nav-link" id="' + game_types[i] + '-tab" data-toggle="tab" href="#' + game_types[i] + '" role="tab" aria-controls="' + game_types[i] + '" aria-selected="false">' + game_types[i] + '</a>';

        document.getElementById('game_type_list').appendChild(li);
    }
}



function build_drilldown(id, title, drilldown_data) {

    var series_data = populate_series_data(drilldown_data);
    var drilldown_series_data = populate_drilldown_data(drilldown_data);
    var favorite = series_data.reduce((a, b) => a.y > b.y ? a : b);

    Highcharts.chart(id, {
        chart: {
            type: 'pie'
        },
        title: {
            text: title
        },
        credits: {
            enabled: false
        },
        subtitle: {
            text: 'Preference ' + favorite.name + ' (' + Math.round(favorite.y, 2) + '%)'
        },
        accessibility: {
            announceNewData: {
                enabled: true
            },
            point: {
                valueSuffix: '%'
            }
        },
        plotOptions: {
            pie: {
                dataLabels: {
                    enabled: true,
                    distance: -50,
                    style: {
                        fontWeight: 'bold',
                        color: 'white'
                    },
                    formatter: function () {

                        if (this.point.isNull)
                            return void 0;

                        if (this.point.y < 10)
                            return void 0;

                        return this.point.name;
                    }
                }
            }
        },
        tooltip: {
            headerFormat: '<span style="font-size:11px">{point.name}</span><br>',
            pointFormat: '<span style="color:{point.color}">{point.name}</span>: <b>{point.y:.1f}%</b> of total<br/>'
        },
        series: [
            {
                name: title,
                colorByPoint: true,
                data: series_data
            }
        ],
        drilldown: {
            series: drilldown_series_data
        }
    });
}

function populate_series_data(drilldown_data) {
    var data = [];
    var total = 0;

    for (let i = 0; i < drilldown_data.length; i++) {
        total += drilldown_data[i]["count"];
    }

    for (let i = 0; i < drilldown_data.length; i++) {
        if (data.filter(function (e) { return e.name === drilldown_data[i]["type"]; }).length > 0) {
            data[data.findIndex(x => x.name === drilldown_data[i]["type"])].y += (drilldown_data[i]["count"] / total) * 100;
        }
        else {
            data.push({
                name: drilldown_data[i]["type"],
                y: (drilldown_data[i]["count"] / total) * 100,
                drilldown: drilldown_data[i]["type"]
            });
        }
    }
    return data;
}

function populate_drilldown_data(drilldown_data) {
    var data = [];
    var total = 0;

    for (let i = 0; i < drilldown_data.length; i++) {
        total += drilldown_data[i]["count"];
    }

    for (let i = 0; i < drilldown_data.length; i++) {
        var found_series = false;

        for (let j = 0; j < data.length; j++) {
            if (data[j].name == drilldown_data[i]["type"]) {
                data[j].data.push(
                    [drilldown_data[i]["name"], (drilldown_data[i]["count"] / total) * 100]
                );
                found_series = true;
                break;
            }
        }

        if (!found_series) {
            data.push({
                'name': drilldown_data[i]["type"],
                'id': drilldown_data[i]["type"],
                'data': [[drilldown_data[i]["name"], (drilldown_data[i]["count"] / total) * 100]]
            });
        }
    }

    return data;
}