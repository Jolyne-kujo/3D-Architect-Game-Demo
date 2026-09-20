using System;

namespace CoastalTemple.Tutorial
{
    /// <summary>Hint/reward bookkeeping never participates in mechanism permission.</summary>
    public sealed class LessonProgress
    {
        readonly int[] hints;
        readonly bool[] completed;
        public int Count => hints.Length;
        public LessonProgress(int count)
        {
            hints = new int[Math.Max(0, count)];
            completed = new bool[hints.Length];
        }

        public int NextHintIndex(int station, int count)
        {
            if (station < 0 || station >= hints.Length || count <= 0) return -1;
            int index = Math.Min(hints[station], count - 1);
            if (hints[station] < count - 1) hints[station]++;
            return index;
        }

        public bool TryComplete(int station, bool actualCondition)
        {
            if (!actualCondition || station < 0 || station >= completed.Length || completed[station]) return false;
            completed[station] = true;
            return true;
        }
    }
}
