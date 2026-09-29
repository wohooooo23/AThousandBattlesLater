using System;
using System.Collections.Generic;

/// <summary>Finite-domain WFC: weighted minimum entropy, observation, arc propagation and bounded backtracking.</summary>
public sealed class WfcConstraintSolver<T>
{
    private readonly List<T>[] options;
    private readonly Func<int, T, int, T, bool> compatible;
    private readonly Func<T, double> weight;
    private readonly int[][] neighbours;
    private readonly Random random;
    private readonly bool[][] allowed;
    private readonly List<(int variable, int option)> trail = new();
    private readonly int budget;
    public int Observations { get; private set; }
    public int Backtracks { get; private set; }
    public int Propagations { get; private set; }

    public WfcConstraintSolver(List<T>[] options, int[][] neighbours,
        Func<int, T, int, T, bool> compatible, Func<T, double> weight, int seed, int budget = 2048)
    {
        this.options = options;
        this.neighbours = neighbours;
        this.compatible = compatible;
        this.weight = weight;
        this.budget = budget;
        random = new Random(seed);
        allowed = new bool[options.Length][];
        for (int i = 0; i < options.Length; i++)
        {
            allowed[i] = new bool[options[i].Count];
            Array.Fill(allowed[i], true);
        }
    }

    public bool Solve(out T[] result)
    {
        Queue<int> pending = new Queue<int>();
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].Count == 0) { result = null; return false; }
            pending.Enqueue(i);
        }
        if (!Propagate(pending) || !Observe()) { result = null; return false; }
        result = new T[options.Length];
        for (int i = 0; i < options.Length; i++)
            for (int j = 0; j < allowed[i].Length; j++)
                if (allowed[i][j]) { result[i] = options[i][j]; break; }
        return true;
    }

    private bool Observe()
    {
        int selected = -1;
        double least = double.PositiveInfinity;
        for (int i = 0; i < options.Length; i++)
        {
            int count = 0;
            double sum = 0, logSum = 0;
            for (int j = 0; j < options[i].Count; j++)
            {
                if (!allowed[i][j]) continue;
                double w = Math.Max(.0001, weight(options[i][j]));
                count++; sum += w; logSum += w * Math.Log(w);
            }
            if (count == 0) return false;
            if (count == 1) continue;
            double entropy = Math.Log(sum) - logSum / sum + random.NextDouble() * .000001;
            if (entropy < least) { least = entropy; selected = i; }
        }
        if (selected < 0) return true;
        if (++Observations > budget) return false;
        List<int> choices = new();
        for (int i = 0; i < allowed[selected].Length; i++) if (allowed[selected][i]) choices.Add(i);
        while (choices.Count > 0 && Observations <= budget)
        {
            double total = 0;
            foreach (int c in choices) total += Math.Max(.0001, weight(options[selected][c]));
            double roll = random.NextDouble() * total;
            int chosenIndex = choices.Count - 1;
            for (int c = 0; c < choices.Count; c++)
            {
                roll -= Math.Max(.0001, weight(options[selected][choices[c]]));
                if (roll <= 0) { chosenIndex = c; break; }
            }
            int chosen = choices[chosenIndex];
            choices.RemoveAt(chosenIndex);
            int checkpoint = trail.Count;
            for (int c = 0; c < allowed[selected].Length; c++)
                if (c != chosen) Ban(selected, c);
            Queue<int> pending = new();
            pending.Enqueue(selected);
            if (Propagate(pending) && Observe()) return true;
            Backtracks++;
            for (int t = trail.Count - 1; t >= checkpoint; t--)
                allowed[trail[t].variable][trail[t].option] = true;
            trail.RemoveRange(checkpoint, trail.Count - checkpoint);
        }
        return false;
    }

    private void Ban(int variable, int option)
    {
        if (!allowed[variable][option]) return;
        allowed[variable][option] = false;
        trail.Add((variable, option));
    }

    private bool Propagate(Queue<int> pending)
    {
        while (pending.Count > 0)
        {
            int from = pending.Dequeue();
            foreach (int to in neighbours[from])
            {
                bool changed = false;
                int remaining = 0;
                for (int b = 0; b < options[to].Count; b++)
                {
                    if (!allowed[to][b]) continue;
                    bool supported = false;
                    for (int a = 0; a < options[from].Count; a++)
                    {
                        if (allowed[from][a] && compatible(from, options[from][a], to, options[to][b]))
                        { supported = true; break; }
                    }
                    if (!supported) { Ban(to, b); changed = true; Propagations++; }
                    else remaining++;
                }
                if (remaining == 0) return false;
                if (changed) pending.Enqueue(to);
            }
        }
        return true;
    }
}
