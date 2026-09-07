using UnityEngine;

// Displays short narrative text between chapters/levels.
// No expensive cutscenes needed - just readable text shown briefly.
public class StoryManager : MonoBehaviour
{
    [System.Serializable]
    public class ChapterStory
    {
        public string chapterTitle;
        [TextArea(2, 4)]
        public string narrativeText;
    }

    [Header("Chapter Narratives (in order)")]
    public ChapterStory[] chapters;

    [Header("Display Settings")]
    public float displayDuration = 4f;

    private string currentTitle;
    private string currentText;
    private float displayTimer;
    private bool isShowing;

    // Call this when a new chapter/level begins, e.g. storyManager.ShowChapter(0);
    public void ShowChapter(int chapterIndex)
    {
        if (chapters == null || chapterIndex < 0 || chapterIndex >= chapters.Length) return;

        currentTitle = chapters[chapterIndex].chapterTitle;
        currentText = chapters[chapterIndex].narrativeText;
        displayTimer = displayDuration;
        isShowing = true;
    }

    void Update()
    {
        if (!isShowing) return;

        displayTimer -= Time.deltaTime;
        if (displayTimer <= 0f)
        {
            isShowing = false;
        }
    }

    void OnGUI()
    {
        if (!isShowing) return;

        // Dim background so the story text is readable over gameplay.
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 28;
        titleStyle.normal.textColor = Color.white;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontStyle = FontStyle.Bold;

        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 18;
        textStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.wordWrap = true;

        GUI.Label(new Rect(Screen.width / 2f - 300, Screen.height / 2f - 60, 600, 40), currentTitle, titleStyle);
        GUI.Label(new Rect(Screen.width / 2f - 300, Screen.height / 2f - 10, 600, 80), currentText, textStyle);
    }
}
