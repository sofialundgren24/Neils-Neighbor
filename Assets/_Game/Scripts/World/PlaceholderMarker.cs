using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Märker en prefab som platshållare byggd av NG > Content > Import All. När en riktig modell
    /// (SM_...fbx) importeras ersätts prefaben och märket försvinner; då rör innehållsimporten den inte längre.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaceholderMarker : MonoBehaviour
    {
    }
}
