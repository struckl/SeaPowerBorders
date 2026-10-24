using System;
using System.Collections.Generic;
using System.IO;
using SeaPowerBorders.Places;

namespace SeaPowerBorders.Borders;

/// <summary>Which borders to draw for a date.</summary>
internal enum BorderEra { MissionDate, Today }

/// <summary>
/// The rules of <c>borders/eras.csv</c>: which of today's borders were not borders yet on a mission's date. A
/// <b>union</b> lists countries that were one (the Soviet Union, Yugoslavia, ...): a border between two members is
/// not drawn while both belong to it. A <b>line</b> rule limits one class of border between two countries to some
/// years. Added lines (inner-German border, West Berlin, ...) carry their own years.
/// </summary>
internal sealed class BorderEras
{
    private sealed class Member
    {
        public string Union, Country;
        public DateTime? Since, Until;
    }

    private sealed class LineRule
    {
        public string A, B;
        public BorderClass? Class;
        public DateTime? Since, Until;
    }

    private readonly Dictionary<string, List<Member>> memberships = new Dictionary<string, List<Member>>();
    private readonly List<LineRule> lineRules = new List<LineRule>();

    public static BorderEras Load(string dir)
    {
        var eras = new BorderEras();
        string file = Path.Combine(dir, "eras.csv");
        foreach (string[] f in ModLists.Rows(file, 6, optional: 1))
        {
            if (f[0] == "union")
            {
                var m = new Member { Union = f[1], Country = f[2], Since = ModLists.Date(f[3]), Until = ModLists.Date(f[4]) };
                if (!eras.memberships.TryGetValue(m.Country, out List<Member> list))
                    eras.memberships[m.Country] = list = new List<Member>();
                list.Add(m);
            }
            else if (f[0] == "line")
            {
                BorderClass? cls = null;
                if (f[3] != "*")
                {
                    if (!Enum.TryParse(f[3], true, out BorderClass parsed))
                    {
                        ModLog.Warn($"eras.csv: unknown border class '{f[3]}'");
                        continue;
                    }
                    cls = parsed;
                }
                eras.lineRules.Add(new LineRule { A = f[1], B = f[2], Class = cls, Since = ModLists.Date(f[4]), Until = ModLists.Date(f[5]) });
            }
            else
            {
                ModLog.Warn($"eras.csv: unknown rule '{f[0]}'");
            }
        }
        return eras;
    }

    public bool Drawn(BorderLine line, DateTime date, BorderEra era)
    {
        if (line.Class == BorderClass.Added)
            return era == BorderEra.MissionDate && In(date, line.Since, line.Until);
        if (era == BorderEra.Today)
            return true;
        foreach (LineRule rule in lineRules)
        {
            bool pair = (rule.A == line.Left && rule.B == line.Right) || (rule.A == line.Right && rule.B == line.Left);
            if (pair && (rule.Class == null || rule.Class == line.Class) && !In(date, rule.Since, rule.Until))
                return false;
        }
        // Two members of one union on that date: no border between them yet (or any more).
        if (memberships.TryGetValue(line.Left, out List<Member> left) && memberships.TryGetValue(line.Right, out List<Member> right))
        {
            foreach (Member a in left)
            {
                if (!In(date, a.Since, a.Until))
                    continue;
                foreach (Member b in right)
                {
                    if (b.Union == a.Union && In(date, b.Since, b.Until))
                        return false;
                }
            }
        }
        return true;
    }

    private static bool In(DateTime date, DateTime? since, DateTime? until) =>
        (since == null || date >= since.Value) && (until == null || date < until.Value);
}
