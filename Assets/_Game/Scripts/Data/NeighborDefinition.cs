using UnityEngine;

namespace NG.Data
{
    /// <summary>En granne: personen som bor i ett av husen och ger spelaren uppdrag.</summary>
    [CreateAssetMenu(menuName = "NG/Neighbor", fileName = "NB_New")]
    public class NeighborDefinition : DefinitionBase
    {
        [Tooltip("Huset grannen bor i.")]
        public RenovatableDefinition house;
        [Tooltip("Färg på porträttet tills riktiga porträtt finns.")]
        public Color color = Color.white;
        [TextArea] public string bio;
        [TextArea] public string bioEn;

        public string Bio => NG.Core.Loc.Pick(bio, bioEn);
    }
}
