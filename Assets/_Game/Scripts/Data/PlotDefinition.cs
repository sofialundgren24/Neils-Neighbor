using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    [CreateAssetMenu(menuName = "NG/Plot", fileName = "PLOT_New")]
    public class PlotDefinition : DefinitionBase
    {
        public Vector2Int gridSize = new Vector2Int(24, 24);
        public RenovatableDefinition house;
        public StyleTag residentTaste;
    }
}
