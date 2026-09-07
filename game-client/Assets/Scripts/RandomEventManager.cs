using UnityEngine;

// Occasionally triggers a random event that temporarily affects the mood/
// atmosphere. Kept intentionally simple and infrequent, per the spec's
// note not to create excessive random events.
public class RandomEventManager : MonoBehaviour
{
    public enum EventType { Firecracker, Thunder, CrowdNoise, MelaAnnouncement, StrongWind }

    [Header("Gameplay Effect")]
    [Tooltip("How much each event scales footstep cue loudness while it is active.")]
    public float firecrackerMultiplier = 1.5f;
    public float thunderMultiplier = 0.4f;
    public float crowdNoiseMultiplier = 0.6f;
    public float melaAnnouncementMultiplier = 0.7f;
    public float strongWindMultiplier = 0.6f;

    [Header("Timing")]
    public float minTimeBetweenEvents = 15f;
    public float maxTimeBetweenEvents = 30f;
    public float eventDuration = 3f;

    private float timer;
    private float remainingEventTime;
    private bool eventActive;
    private EventType currentEvent;

    void Start()
    {
        ScheduleNextEvent();
    }

    void Update()
    {
        if (eventActive)
        {
            remainingEventTime -= Time.deltaTime;
            if (remainingEventTime <= 0f)
            {
                EndEvent();
            }
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            TriggerRandomEvent();
        }
    }

    private void ScheduleNextEvent()
    {
        timer = Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
    }

    private void TriggerRandomEvent()
    {
        System.Array values = System.Enum.GetValues(typeof(EventType));
        currentEvent = (EventType)values.GetValue(Random.Range(0, values.Length));
        eventActive = true;
        remainingEventTime = eventDuration;

        // Events do not just print a message - they temporarily change how
        // well the Kanamachi can hear footsteps (spec Section 27).
        ApplyEventEffect(currentEvent);

        Debug.Log($"[RandomEventManager] Event triggered: {currentEvent} " +
                  $"(cue volume x{GetVolumeMultiplier(currentEvent):F2})");
    }

    private void EndEvent()
    {
        Debug.Log($"[RandomEventManager] Event ended: {currentEvent}");

        // Restore normal hearing.
        if (SoundCueManager.Instance != null)
        {
            SoundCueManager.Instance.EventVolumeMultiplier = 1f;
        }

        eventActive = false;
        ScheduleNextEvent();
    }

    private void ApplyEventEffect(EventType type)
    {
        if (SoundCueManager.Instance == null) return;

        SoundCueManager.Instance.EventVolumeMultiplier = GetVolumeMultiplier(type);
    }

    // Thunder, wind and crowd noise drown footsteps out; a firecracker
    // startles everyone and makes the next few steps easier to hear.
    private float GetVolumeMultiplier(EventType type)
    {
        switch (type)
        {
            case EventType.Firecracker: return firecrackerMultiplier;
            case EventType.Thunder: return thunderMultiplier;
            case EventType.CrowdNoise: return crowdNoiseMultiplier;
            case EventType.MelaAnnouncement: return melaAnnouncementMultiplier;
            case EventType.StrongWind: return strongWindMultiplier;
            default: return 1f;
        }
    }

    void OnGUI()
    {
        if (!eventActive) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.magenta;
        GUI.Label(new Rect(10, 70, 400, 30), $"Event: {currentEvent}", style);
    }
}
