using UnityEngine;

namespace NG.Data
{
    /// <summary>
    /// En fasad/färgvariant för ett hus som låses upp på en nivå (t.ex. "Hus 2: Tegelfasad").
    /// Med platshållare och palettmodeller visas den som en färgton; med riktiga modeller kan den bytas
    /// mot en egen textur (T_&lt;Hus&gt;_&lt;Fasad&gt;.png) senare. Skapas från facades.csv.
    /// </summary>
    [CreateAssetMenu(menuName = "NG/Facade", fileName = "FAC_New")]
    public class FacadeDefinition : DefinitionBase
    {
        public RenovatableDefinition house;
        public Color color = Color.white;
        [Min(0)] public int unlockLevel = 1;
        [Min(0)] public int coinCost;
    }
}
