using System.Collections.Generic;
using UnityEngine;

namespace NG.Core
{
    /// <summary>
    /// Avgör vem som får en tryckning: UI:t, kameran eller världen.
    /// UI:t registrerar sina rutor varje bildruta; kameran och WorldInteraction frågar innan de reagerar.
    /// Skärmkoordinater har origo nere till vänster (som Input System).
    /// </summary>
    public static class InputGate
    {
        static readonly List<Rect> UiRects = new List<Rect>();
        static readonly List<Rect> Building = new List<Rect>();

        /// <summary>Sant medan spelaren drar ett objekt i byggläget. Då panorerar inte kameran.</summary>
        public static bool WorldDragCaptured;

        /// <summary>Sant medan ett fönster täcker skärmen. Då når inga tryck världen.</summary>
        public static bool ModalOpen;

        public static void BeginUiFrame() => Building.Clear();

        public static void EndUiFrame()
        {
            UiRects.Clear();
            UiRects.AddRange(Building);
        }

        /// <summary>Registrerar en UI-ruta i skärmpixlar med origo uppe till vänster (som OnGUI).</summary>
        public static void AddGuiRect(Rect guiRect) =>
            Building.Add(new Rect(guiRect.x, Screen.height - guiRect.yMax, guiRect.width, guiRect.height));

        /// <summary>UI Toolkit-gränssnittet (GameUI) svarar på om en skärmpunkt träffar UI.</summary>
        public static System.Func<Vector2, bool> UiHitTest;

        public static bool IsOverUi(Vector2 screenPos)
        {
            if (ModalOpen) return true;
            if (UiHitTest != null && UiHitTest(screenPos)) return true;
            foreach (var r in UiRects)
                if (r.Contains(screenPos)) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            UiRects.Clear();
            Building.Clear();
            WorldDragCaptured = false;
            ModalOpen = false;
            UiHitTest = null;
        }
    }
}
