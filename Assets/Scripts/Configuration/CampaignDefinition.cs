using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Orders stage definitions for one campaign independently of the scene hierarchy.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Campaign")]
    public sealed class CampaignDefinition : ScriptableObject
    {
        /// <summary>Ordered stages; each index must have a matching authored StageBinding in the game scene.</summary>
        public StageDefinition[] Stages;
    }
}
