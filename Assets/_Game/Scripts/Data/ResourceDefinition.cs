using UnityEngine;

namespace NG.Data
{
    /// <summary>En vara i lagret: skörd (vete, äpplen) eller förädlad (bröd, sylt).</summary>
    [CreateAssetMenu(menuName = "NG/Resource", fileName = "RES_New")]
    public class ResourceDefinition : DefinitionBase
    {
        [Min(1)] public int sellPrice = 1;
        [Tooltip("Från denna nivå kan varan dyka upp i beställningar.")]
        [Min(1)] public int unlockLevel = 1;
        [Tooltip("Färg på platshållarikonen tills en riktig ikon finns.")]
        public Color color = Color.white;
    }
}
