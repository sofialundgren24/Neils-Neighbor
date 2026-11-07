using UnityEngine;

namespace NG.World
{
    /// <summary>Kopplar ett objekt i scenen till dess PlacedObjectState (för tryck och markering).</summary>
    [DisallowMultipleComponent]
    public class WorldObject : MonoBehaviour
    {
        public string InstanceId;
    }
}
