namespace dump;

public interface IDumper {
    public Task<List<string>> summary(string userPrincipalName, DateTime fromDate, DateTime toDate, bool satweek=false);
}
public class Dump: IDumper {

    private task.IReader task;

    public static IDumper New(task.IReader task) {
        return new Dump(task);
    }

    public Dump(task.IReader task) {
        this.task = task;
    }
/*
// return sequence of mondays that contain days from the same month as `from' 
let mondays (from: DateTime) =
    let dow = from.DayOfWeek                // sunday = 0
    let dec = (dow.value__ + 6) % 7         // sunday -> 6, monday -> 0, tuesday -> 1
    let mon1 = from.AddDays(float(-dec))    // monday of the first week containing days in the month
    [for w in 0 .. 5 do
        let mon = mon1.AddDays(float(w*7))
        if mon.Month <> from.Month + 1 then yield mon     // don't walk into the following month but allow last week in previous (eg. dec=12)
    ]
*/

private IEnumerable<DateTime> mondays(DateTime from, DateTime to) {
    DayOfWeek dow = from.DayOfWeek;             // sunday = 0
    int dec = (int)(dow + 6) % 7;               // sunday -> 6, monday -> 0, tuesday -> 1
    DateTime mon1 = from.AddDays(-dec);         // monday of the first week containing days in the month
    
    for (int w = 0;; w++) {
        DateTime mon = mon1.AddDays(w * 7);
        if (mon > to)
            break;
        yield return mon;                   // don't walk into the following month but allow last week in previous (e.g., dec=12)
    }
}

/*
// return sequence of saturdays that contain days from the same month as `from' 
let saturdays (from: DateTime) =
    let dow = from.DayOfWeek                // sunday = 0
    let dec = (dow.value__ + 1) % 7         // friday -> 6, saturday -> 0, sunday -> 1, monday -> 2, tuesday -> 3
    let sat1 = from.AddDays(float(-dec))    // saturday of the first week containing days in the month
    [for w in 0 .. 5 do
        let sat = sat1.AddDays(float(w*7))
        if sat.Month <> from.Month + 1 then yield sat     // don't walk into the following month but allow last week in previous (eg. dec=12)
    ]
*/

private IEnumerable<DateTime> saturdays(DateTime from, DateTime to) {
    DayOfWeek dow = from.DayOfWeek;                      // sunday = 0
    int dec = (int)(dow + 1) % 7;                        // friday -> 6, saturday -> 0, sunday -> 1, monday -> 2, tuesday -> 3
    DateTime sat1 = from.AddDays(-dec);                  // saturday of the first week containing days in the month
    
    for (int w = 0; w <= 5; w++) {
        DateTime sat = sat1.AddDays(w * 7);
        if (sat > to)
            break;
        yield return sat;                             // don't walk into the following month but allow last week in previous (e.g., dec=12)
    }
}

/*
let weekdays (monday: DateTime) =
    [for d in 0 .. 6 -> monday.AddDays(float d)]
*/

private IEnumerable<DateTime> weekdays(DateTime monday) {
    for (int d = 0; d <= 6; d++) {
        yield return monday.AddDays(d);
    }
}

/*
let fmtLongTime (ts: TimeSpan) =
    let hrs = ts.Days*24 + ts.Hours
    let min = ts.Minutes
    match hrs,min with
    | 0,0 -> "0"
    | 0,m -> sprintf "%d min" m
    | h,0 -> sprintf "%d hr" h 
    | h,m -> sprintf "%d hr %d min" h m
*/

private string fmtLongTime(TimeSpan ts) {
    int hrs = ts.Days * 24 + ts.Hours;
    int min = ts.Minutes;

    if (hrs == 0 && min == 0)
        return "0";
    else if (hrs == 0)
        return $"{min} min";
    else if (min == 0)
        return $"{hrs} hr";
    else
        return $"{hrs} hr {min} min";
}

/*
let fmtTime (ts: TimeSpan) =
    let hrs = ts.Days*24 + ts.Hours
    let min = ts.Minutes
    match hrs,min with
    | 0,0 -> "0"
    | h,0 -> sprintf "%d" h
    | h,m -> sprintf "%d:%d" h m
*/

private string fmtTime(TimeSpan ts) {
    int hrs = ts.Days * 24 + ts.Hours;
    int min = ts.Minutes;

    if (hrs == 0 && min == 0)
        return "0";
    else if (min == 0)
        return $"{hrs}";
    else
        return $"{hrs}:{min}";
}

/*
let fmtDate (dt: DateTime) =
    let day = dt.Day
    let mon = dt.Month
    let yr = dt.Year
    let thisyr = DateTime.Today.Year
    match day,mon,yr with
    | d,m,y when y = thisyr -> sprintf "%02d/%02d" day mon
    | d,m,y -> sprintf "%02d/%02d/%d" day mon yr
*/
private string fmtDate(DateTime dt) {
    int day = dt.Day;
    int mon = dt.Month;
    int yr = dt.Year;
    int thisyr = DateTime.Today.Year;

    if (yr == thisyr)
        return $"{day:00}/{mon:00}";
    else
        return $"{day:00}/{mon:00}/{yr}";
}

/*
// Dump the timesheet    
let Dump (from_date: DateTime) (to_date: DateTime) (satweek: bool) =
    let out = new ResizeArray<string>()
    let time (tl: Task list) = tl |> List.fold (fun acc t -> acc + t.duration) (new TimeSpan(0,0,0))
    let daytime (tl: Task list) (day: DateTime) = tl |> List.filter (fun t -> t.date.Date = day.Date) |> time
    sprintf "For the Dates %s  -  %s" (fmtDate from_date) (fmtDate to_date) |> out.Add
    "" |> out.Add
    Projects from_date to_date |>
    List.iter (fun proj -> 
        sprintf "%s = %s" proj.name (fmtLongTime(time proj.tasks)) |> out.Add
        let startdays =
            match satweek with
            | true -> saturdays
            | false -> mondays
        startdays from_date |> List.iter 
            (fun d ->
                let times = [for dow in weekdays d -> daytime proj.tasks dow]
                let eqn = times |> List.map (fun t -> fmtTime t) |> String.concat " + "
                let sum = times |> List.fold (fun acc t -> acc + t) (new TimeSpan(0,0,0)) |> fmtTime
                sprintf "w/b %s  -  %s = %s" (fmtDate d) eqn sum |> out.Add)
        "" |> out.Add
        proj.groups.Values |> Seq.iter
            ( fun g -> sprintf "- %s (%s)" g.group (fmtLongTime g.duration) |> out.Add)
        "" |> out.Add
        proj.summary.Values |> Seq.iter
            ( fun s -> sprintf "- %s %s (%s)" s.group s.desc (fmtLongTime s.duration) |> out.Add)
        "" |> out.Add
    )
    out |> List.ofSeq

*/

public async Task<List<string>> summary(string userPrincipalName, DateTime fromDate, DateTime toDate, bool satweek=false) {
    List<string> output = new List<string>();

    TimeSpan Time(List<task.Task> tl) => tl.Aggregate(new TimeSpan(0, 0, 0), (acc, t) => acc + t.duration);
    TimeSpan DayTime(List<task.Task> tl, DateTime day) => tl.Where(t => t.start.Date == day.Date).Aggregate(new TimeSpan(0, 0, 0), (acc, t) => acc + t.duration);

    output.Add($"For the Dates {fmtDate(fromDate)} - {fmtDate(toDate)}");
    output.Add("");

    foreach (var proj in await task.Read(userPrincipalName, fromDate, toDate))
    {
        output.Add($"{proj.name} = {fmtLongTime(Time(proj.tasks))}");

        IEnumerable<DateTime> startdays = satweek ? saturdays(fromDate, toDate) : mondays(fromDate, toDate);
        foreach (var d in startdays)
        {
            var times = weekdays(d).Select(day => DayTime(proj.tasks, day));
            var eqn = string.Join(" + ", times.Select(t => fmtTime(t)));
            var sum = times.Aggregate(new TimeSpan(0, 0, 0), (acc, t) => acc + t);
            output.Add($"w/b {fmtDate(d)} - {eqn} = {fmtTime(sum)}");
        }

        output.Add("");
        foreach (var g in proj.groups.Values)
        {
            output.Add($"- {g.group} ({fmtLongTime(g.duration)})");
        }

        output.Add("");
        foreach (var s in proj.summary.Values)
        {
            output.Add($"- {s.group} {s.desc} ({fmtLongTime(s.duration)})");
        }

        output.Add("");
    }

    return output;
}



}