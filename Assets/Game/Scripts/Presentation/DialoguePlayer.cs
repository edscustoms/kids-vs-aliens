using System;
using System.Collections.Generic;
using UnityEngine;

// One scene-owned speech channel. It observes scaled time and never owns input/camera.
[DisallowMultipleComponent, RequireComponent(typeof(AudioEmitter))]
public sealed class DialoguePlayer : MonoBehaviour
{
    [SerializeField, Min(0)] private float voicePadding = .35f;
    [SerializeField, Min(1)] private float charactersPerSecond = 16;
    [SerializeField, Min(1)] private int queueCapacity = 8;
    private struct Request { public DialogueMessage message; public DialogueSpeaker speaker; }
    private readonly Queue<Request> queue = new(8);
    private AudioEmitter voice;
    private DialogueMessage.Variant variant;
    private int lineIndex;
    private float remaining;
    private bool gap;
    public static DialoguePlayer Instance { get; private set; }
    public DialogueMessage CurrentMessage { get; private set; }
    public DialogueSpeaker Speaker { get; private set; }
    public DialogueMessage.Line CurrentLine { get; private set; }
    public event Action Changed;

    private void Awake() => voice = GetComponent<AudioEmitter>();
    private void OnEnable()
    {
        if (Instance != null && Instance != this) { Debug.LogError("Only one DialoguePlayer is supported per gameplay scene.", this); enabled = false; return; }
        Instance = this;
    }
    public bool Play(DialogueMessage message, DialogueSpeaker speaker)
    {
        if (!isActiveAndEnabled || message == null || speaker == null) return false;
        var resolved = message.Resolve(speaker);
        if (resolved == null || resolved.lines == null || resolved.lines.Length == 0) return false;
        if (CurrentMessage != null)
        {
            if (queue.Count >= queueCapacity) return false;
            queue.Enqueue(new Request { message = message, speaker = speaker });
            return true;
        }
        CurrentMessage = message; Speaker = speaker; variant = resolved; lineIndex = 0;
        ShowLine();
        return true;
    }
    private void ShowLine()
    {
        CurrentLine = variant.lines[lineIndex]; gap = false;
        voice.Stop();
        if (CurrentLine == null) { remaining = 0; Changed?.Invoke(); return; }
        bool voiced = CurrentLine.voice != null && voice.Play(CurrentLine.voice, false);
        remaining = CurrentLine.displayDuration > 0 ? CurrentLine.displayDuration
            : voiced ? voice.PlaybackDuration + voicePadding : Mathf.Max(2, (CurrentLine.text?.Length ?? 0) / charactersPerSecond);
        Changed?.Invoke();
    }
    private void Update()
    {
        if (CurrentMessage == null || Time.deltaTime <= 0) return;
        remaining -= Time.deltaTime;
        if (remaining > 0) return;
        if (!gap)
        {
            remaining = CurrentLine != null ? CurrentLine.delayAfter : 0;
            gap = true; CurrentLine = null; voice.Stop(); Changed?.Invoke();
            if (remaining > 0) return;
        }
        if (++lineIndex < variant.lines.Length) { ShowLine(); return; }
        CurrentMessage = null; Speaker = null; variant = null;
        Changed?.Invoke();
        if (queue.Count > 0) { var next = queue.Dequeue(); Play(next.message, next.speaker); }
    }
    public void Stop()
    {
        queue.Clear(); voice?.Stop(); CurrentMessage = null; CurrentLine = null; Speaker = null; variant = null;
        Changed?.Invoke();
    }
    private void OnDisable() { Stop(); if (Instance == this) Instance = null; }
}
