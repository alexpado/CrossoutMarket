var lang = Cookies.get("language");
if (lang === undefined || !lang.match("en|ru"))
    lang = "en";

var calendarEl = document.getElementById("calendar");
var calendar = new FullCalendar.Calendar(calendarEl, {
    locale: lang,
    height: "auto",
    initialView: "timeGridDay",
    headerToolbar: {
        left: "prev,next today",
        center: "title",
        right: "dayGridMonth,timeGridWeek,timeGridDay",
    },
    eventSources: [{
        url: "/schedules/brawls.ics",
        format: "ics"
    },
    {
        url: "/schedules/clanwars.ics",
        format: "ics",
        color: "#D46722"
    }]
});

calendar.render();
