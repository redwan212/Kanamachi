using UnityEngine;

// Occasionally triggers a random event that temporarily affects the mood/
// atmosphere. Kept intentionally simple and infrequent, per the spec's
// note not to create excessive random events.
public class RandomEventManager : MonoBehaviour
{
    public enum EventType { Firecracker, Thunder, CrowdNoise, MelaAnnouncement, StrongWind }

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

        Debug.Log($"[RandomEventManager] Event triggered: {currentEvent}");
    }

    private void EndEvent()
    {
        Debug.Log($"[RandomEventManager] Event ended: {currentEvent}");
        eventActive = false;
        ScheduleNextEvent();
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
