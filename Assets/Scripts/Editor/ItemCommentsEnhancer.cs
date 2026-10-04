#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narzędzie dodające i wzbogacające komentarze / myśli wewnętrzne fryzjera
/// oraz konfigurujące widok 3D Showcase dla ważnych przedmiotów w scenie.
/// </summary>
public static class ItemCommentsEnhancer
{
    [MenuItem("Tools/Cyrulik/Apply Rich Item Comments to Scene", false, 15)]
    public static void ApplyRichItemComments()
    {
        int modifiedCount = 0;

        // 1. Brzytwa (Razor)
        PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
        foreach (var p in pickups)
        {
            string n = p.name.ToLowerInvariant();
            string id = p.ItemId != null ? p.ItemId.ToLowerInvariant() : "";

            if (id == "razor" || n.Contains("razor") || n.Contains("blade") || n.Contains("brzytwa"))
            {
                p.EnableShowcaseOnPickup = true;
                p.ShowcaseTitle = "STRAIGHT RAZOR";
                p.ShowcaseComment = "My father's straight razor. Heavy, cold Swedish steel. Without stropping, it's just a blunt piece of metal.";
                p.ThoughtText = "My father's straight razor. Needs a good stropping before touching any throat.";
                p.ShowcaseModelScale = 1.3f;
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
            else if (id == "cheese" || n.Contains("cheese") || n.Contains("ser"))
            {
                p.EnableShowcaseOnPickup = true;
                p.ShowcaseTitle = "AGED CHEESE";
                p.ShowcaseComment = "Pungent aged cheese. Smells strong enough to lure a starving rat from any hole in the floor.";
                p.ThoughtText = "Pungent aged cheese. Perfect bait for the rat trap.";
                p.ShowcaseModelScale = 1.6f;
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
            else if (id == "scissors" || n.Contains("scissor") || n.Contains("nozyc"))
            {
                p.EnableShowcaseOnPickup = true;
                p.ShowcaseTitle = "SCISSORS";
                p.ShowcaseComment = "Heavy barber scissors. Cold Swedish steel, perfectly balanced. They've snipped thousands of locks... and they're still keen enough for something deeper.";
                p.ThoughtText = "Keen steel scissors. Kept in prime condition.";
                p.ShowcaseModelScale = 1.4f;
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
            else if (id == "dead_mouse" || n.Contains("deadmouse") || n.Contains("dead_mouse"))
            {
                p.EnableShowcaseOnPickup = true;
                p.ShowcaseTitle = "DEAD RAT";
                p.ShowcaseComment = "Cold and limp. Need to toss this filthy creature into the trash bin before any customer spots it.";
                p.ThoughtText = "I must dispose of this in the trash bin immediately.";
                p.ShowcaseModelScale = 1.2f;
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
            else if (id.Contains("pot") || n.Contains("pot"))
            {
                p.ThoughtText = "A cast iron pot. Hot water softens the stubble and eases the customer's mind.";
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
            else if (id.Contains("glass") || n.Contains("glass") || n.Contains("mug"))
            {
                p.ThoughtText = "A clean glass. Jurek mentioned having a parched throat.";
                EditorUtility.SetDirty(p);
                modifiedCount++;
            }
        }

        // 2. Obiekty otoczenia (Piec, Pas do ostrzenia, Zlew, Lustro, Budzik, Kosz, Krzesło)
        SetupOrUpdateInspect(
            objectKeywords: new[] { "strop", "razorstrop", "pas" },
            interactionName: "Examine leather strop",
            thought: "A thick leather strop hanging by the wall. A few smooth passes and the steel will sing at the throat.",
            enableShowcase: true,
            showcaseTitle: "LEATHER STROP",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "stove", "piec" },
            interactionName: "Look at stove",
            thought: "An old cast iron stove. It warms the whole parlor, but keeping the flame alive takes patience.",
            enableShowcase: false,
            showcaseTitle: "IRON STOVE",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "mirror", "lustro" },
            interactionName: "Look in mirror",
            thought: "A tired face staring back from the silvered glass. Don't look too long. There is work to be done.",
            enableShowcase: false,
            showcaseTitle: "BARBER MIRROR",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "clock", "alarm", "budzik", "zegar" },
            interactionName: "Check time",
            thought: "Tick, tack. The clockwork never pauses, never forgives. Every minute brings the next customer closer.",
            enableShowcase: true,
            showcaseTitle: "ALARM CLOCK",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "trash", "bin", "kosz", "smieci" },
            interactionName: "Examine trash bin",
            thought: "Best place for vermin and yesterday's filth. Out of sight, out of mind.",
            enableShowcase: false,
            showcaseTitle: "TRASH BIN",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "chair", "fotel" },
            interactionName: "Examine barber chair",
            thought: "Worn burgundy leather. Many heads have tilted back here. Some never opened their eyes again.",
            enableShowcase: false,
            showcaseTitle: "BARBER CHAIR",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "sink", "zlew", "kran" },
            interactionName: "Inspect sink",
            thought: "Stained porcelain and rusty pipes. Water runs icy cold, but it's pure enough.",
            enableShowcase: false,
            showcaseTitle: "ENAMEL SINK",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "radio" },
            interactionName: "Examine radio",
            thought: "Crackling static and forgotten melodies. The only thing keeping the silence from swallowing this shop.",
            enableShowcase: false,
            showcaseTitle: "VINTAGE RADIO",
            ref modifiedCount
        );

        SetupOrUpdateInspect(
            objectKeywords: new[] { "mousetrap", "trap", "pułapka", "pulapka" },
            interactionName: "Inspect rat trap",
            thought: "A rusty spring, but still deadly fast. Just needs fresh bait and a dark corner.",
            enableShowcase: true,
            showcaseTitle: "RAT TRAP",
            ref modifiedCount
        );

        EditorUtility.DisplayDialog("Sukces!", $"Wzbogacono {modifiedCount} obiektów w scenie o klimatyczne myśli i inspekcję 3D!", "Świetnie");
        Debug.Log($"[ItemCommentsEnhancer] Pomyślnie zaktualizowano {modifiedCount} obiektów o nowe komentarze i showcase!");
    }

    private static void SetupOrUpdateInspect(
        string[] objectKeywords,
        string interactionName,
        string thought,
        bool enableShowcase,
        string showcaseTitle,
        ref int counter)
    {
        GameObject targetGo = null;

        // Szukamy po nazwach
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allObjects)
        {
            string n = go.name.ToLowerInvariant();
            foreach (var kw in objectKeywords)
            {
                if (n.Contains(kw))
                {
                    targetGo = go;
                    break;
                }
            }
            if (targetGo != null) break;
        }

        if (targetGo == null) return;

        // Jeśli obiekt ma już PickupItem (np. podnoszona pułapka), dodajemy komentarz do PickupItem
        if (targetGo.TryGetComponent<PickupItem>(out var pickup))
        {
            pickup.ThoughtText = thought;
            if (enableShowcase)
            {
                pickup.EnableShowcaseOnPickup = true;
                pickup.ShowcaseTitle = showcaseTitle;
                pickup.ShowcaseComment = thought;
            }
            EditorUtility.SetDirty(pickup);
            counter++;
            return;
        }

        // Jeśli nie ma PickupItem, dodajemy lub konfigurujemy InspectThoughtInteractable
        InspectThoughtInteractable inspect = targetGo.GetComponent<InspectThoughtInteractable>();
        if (inspect == null)
        {
            inspect = targetGo.AddComponent<InspectThoughtInteractable>();
        }

        inspect.ThoughtText = thought;
        inspect.EnableShowcase = enableShowcase;
        inspect.ShowcaseTitle = showcaseTitle;

        // Ustaw collider jeśli brak
        if (targetGo.GetComponent<Collider>() == null)
        {
            BoxCollider col = targetGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 0.5f, 0.5f);
        }

        EditorUtility.SetDirty(inspect);
        counter++;
    }
}
#endif
