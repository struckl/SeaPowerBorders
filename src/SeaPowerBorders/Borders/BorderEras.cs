using System;
using System.Collections.Generic;
using System.IO;

namespace SeaPowerBorders.Borders;

/// <summary>
/// The rules of <c>borders/eras.csv</c>: which of today's borders were not borders yet on a mission's date. A
/// union lists countries that were one (the Soviet Union, Yugoslavia, ...): a border between two members is
/// not drawn while both belong to it. A line rule limits one class of border between two countries to some
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
        foreach (string[] f in DataFiles.Rows(file, 6, optional: 1))
        {
            if (f[0] == "union")
            {
                var m = new Member { Union = f[1], Country = f[2], Since = DataFiles.Date(f[3]), Until = DataFiles.Date(f[4]) };
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
                eras.lineRules.Add(new LineRule { A = f[1], B = f[2], Class = cls, Since = DataFiles.Date(f[4]), Until = DataFiles.Date(f[5]) });
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
            return era == BorderEra.MissionDate && DataFiles.InRange(date, line.Since, line.Until);
        if (era == BorderEra.Today)
            return true;
        foreach (LineRule rule in lineRules)
        {
            bool pair = (rule.A == line.Left && rule.B == line.Right) || (rule.A == line.Right && rule.B == line.Left);
            if (pair && (rule.Class == null || rule.Class == line.Class) && !DataFiles.InRange(date, rule.Since, rule.Until))
                return false;
        }
        return !OneUnion(line.Left, line.Right, date);
    }

    // Two members of one union on that date: no border between them yet (or any more).
    private bool OneUnion(string a, string b, DateTime date)
    {
        if (!memberships.TryGetValue(a, out List<Member> left) || !memberships.TryGetValue(b, out List<Member> right))
            return false;
        foreach (Member m in left)
        {
            if (!DataFiles.InRange(date, m.Since, m.Until))
                continue;
            foreach (Member n in right)
            {
                if (n.Union == m.Union && DataFiles.InRange(date, n.Since, n.Until))
                    return true;
            }
        }
        return false;
    }
}
