using System;
using UnityEngine;

/// <summary>Plays a circular baked schedule into an authored fixed pool. Scaled time follows world pause.</summary>
[DisallowMultipleComponent]
public sealed class AlienGroundAuroraController : MonoBehaviour
{
    [SerializeField] private AlienGroundAuroraSchedule schedule;
    [SerializeField] private AlienGroundAuroraPatch[] patches = Array.Empty<AlienGroundAuroraPatch>();
    private int[] activeEvents;
    private double[] startedAt;
    private double time, cycleStart;
    private int nextEvent;
    public AlienGroundAuroraSchedule Schedule => schedule;
    public int PoolSize => patches.Length;
    public int ActiveCount { get; private set; }
    public double PlaybackTime => time;

    private void Awake()
    {
        if (schedule == null || schedule.events.Length == 0 || schedule.duration <= 0
            || patches.Length != schedule.poolSize)
        {
            Debug.LogError($"{name}: assign a baked Aurora schedule and its complete authored pool.", this);
            enabled = false;
            return;
        }
        activeEvents = new int[patches.Length];
        startedAt = new double[patches.Length];
        for (int i = 0; i < patches.Length; i++)
        {
            if (patches[i] == null || patches[i].Ribbon == null)
            {
                Debug.LogError($"{name}: Aurora pool slot {i} is missing its ribbon.", this);
                enabled = false;
                return;
            }
            patches[i].Initialize();
        }
    }

    private void OnEnable()
    {
        if (activeEvents == null) return;
        time = 0;
        RebuildCursor();
        Present();
    }

    private void Update() => Advance(Time.deltaTime);

    public void Advance(float seconds)
    {
        if (activeEvents == null || !isActiveAndEnabled || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        time += seconds;
        // A long frame/background gap seeks the authored schedule, never inventing missed events.
        if (seconds >= schedule.duration) RebuildCursor();
        else
        {
            while (true)
            {
                if (nextEvent == schedule.events.Length)
                {
                    if (time < cycleStart + schedule.duration) break;
                    cycleStart += schedule.duration;
                    nextEvent = 0;
                }
                var value = schedule.events[nextEvent];
                if (cycleStart + value.startTime > time) break;
                StartEvent(nextEvent, cycleStart + value.startTime);
                nextEvent++;
            }
        }
        Present();
    }

    private void RebuildCursor()
    {
        for (int i = 0; i < patches.Length; i++) { patches[i].Hide(); activeEvents[i] = -1; }
        cycleStart = Math.Floor(time / schedule.duration) * schedule.duration;
        double local = time - cycleStart;
        nextEvent = 0;
        for (int i = 0; i < schedule.events.Length; i++)
        {
            var value = schedule.events[i];
            if (value.startTime <= local) nextEvent = i + 1;
            double start = cycleStart + value.startTime - (value.startTime > local ? schedule.duration : 0);
            if (time - start < value.lifetime) StartEvent(i, start);
        }
    }

    private void StartEvent(int index, double start)
    {
        var value = schedule.events[index];
        activeEvents[value.poolSlot] = index;
        startedAt[value.poolSlot] = start;
        patches[value.poolSlot].Begin(value);
    }

    private void Present()
    {
        ActiveCount = 0;
        for (int i = 0; i < patches.Length; i++)
        {
            int index = activeEvents[i];
            if (index < 0) continue;
            var value = schedule.events[index];
            float age = (float)(time - startedAt[i]);
            if (age >= value.lifetime) { patches[i].Hide(); activeEvents[i] = -1; continue; }
            patches[i].Present(value, age);
            ActiveCount++;
        }
    }

    private void OnDisable()
    {
        foreach (var patch in patches) if (patch != null) patch.Hide();
        ActiveCount = 0;
    }
}
